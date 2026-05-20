// ============================================================
// TOPIC: Senior/Staff Depth Topics
// ============================================================
// Covers: Generics (constraints, variance), Span<T>/Memory<T>,
//         GC & IDisposable, string internals, Reflection,
//         Dependency Injection internals, Extension methods,
//         Value types vs Reference types, Boxing/Unboxing,
//         Immutability patterns, Expression trees
// ============================================================

namespace Practice.SeniorDepthTopics;

// ============================================================
// 1. Generics — constraints, covariance, contravariance
//
// Q: What are the generic constraints available in C#?
//    where T : class         — reference type
//    where T : struct        — value type (non-nullable)
//    where T : new()         — public parameterless constructor
//    where T : IFoo          — must implement interface
//    where T : Base          — must derive from Base
//    where T : notnull       — non-nullable (with NRT)
//    where T : unmanaged     — unmanaged type (int, struct with no refs)
// Q: What is covariance/contravariance on generic interfaces?
//    out T (covariant):   IEnumerable<Dog> assignable to IEnumerable<Animal>
//    in T (contravariant): IComparer<Animal> assignable to IComparer<Dog>
// ============================================================

public interface IRepository<T> where T : class, new()  // must be reference type with ctor
{
    T? FindById(int id);
    void Add(T entity);
    IEnumerable<T> GetAll();
}

public class InMemoryRepository<T> : IRepository<T> where T : class, new()
{
    private readonly List<T> _store = [];

    public T? FindById(int id) => _store.ElementAtOrDefault(id);
    public void Add(T entity) => _store.Add(entity);
    public IEnumerable<T> GetAll() => _store;
}

// Local Animal/Dog for this namespace (avoids cross-namespace collision)
public class Animal { }
public class Dog : Animal { }

// Covariance: IProducer<Dog> can be used as IProducer<Animal>
public interface IProducer<out T> { T Produce(); }
// Contravariance: IConsumer<Animal> can be used as IConsumer<Dog>
public interface IConsumer<in T>  { void Consume(T item); }

public class AnimalProducer : IProducer<Dog>
{
    public Dog Produce() => new();
}

public static class GenericsDemo
{
    // Generic method with constraint: T must be IComparable for comparison
    public static T Max<T>(T a, T b) where T : IComparable<T> =>
        a.CompareTo(b) >= 0 ? a : b;

    // Generic method returning new instance of T
    public static T CreateDefault<T>() where T : new() => new T();

    public static void Demonstrate()
    {
        Console.WriteLine("=== Generics ===\n");

        Console.WriteLine($"Max(3,7): {Max(3, 7)}");
        Console.WriteLine($"Max(\"apple\",\"banana\"): {Max("apple", "banana")}");

        // Covariance in action
        IProducer<Dog> dogProducer = new AnimalProducer();
        IProducer<Animal> animalProducer = dogProducer; // covariant assignment
        Console.WriteLine($"Covariant: {animalProducer.Produce().GetType().Name}");

        // Unmanaged sizes via Marshal (no unsafe needed for the demo)
        Console.WriteLine($"sizeof int={System.Runtime.InteropServices.Marshal.SizeOf<int>()}, " +
                          $"double={System.Runtime.InteropServices.Marshal.SizeOf<double>()}");
    }
}

// ============================================================
// 2. Span<T> and Memory<T> — zero-copy, stack-allocated slices
//
// Q: What is Span<T>?
//    Stack-only ref struct — a view over a contiguous block of memory (array, stack, native).
//    Zero allocation — no heap copy when slicing a string or array.
// Q: What is Memory<T> vs Span<T>?
//    Memory<T> can live on the heap — can be stored in fields, used across async awaits.
//    Span<T> CANNOT cross await boundaries (ref struct restriction).
// Q: When is Span<T> useful?
//    High-performance parsing, slicing strings without allocations, buffer operations.
// ============================================================

public static class SpanMemoryDemo
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Span<T> / Memory<T> ===\n");

        // Slice a string without allocating a substring
        ReadOnlySpan<char> input = "Hello, World!".AsSpan();
        ReadOnlySpan<char> greeting = input[..5];  // "Hello" — NO new string allocation
        ReadOnlySpan<char> name     = input[7..12]; // "World"
        Console.WriteLine($"greeting={greeting}, name={name}");

        // Stack-allocated array via stackalloc
        Span<int> stackBuffer = stackalloc int[8]; // on the stack — no GC pressure
        for (int i = 0; i < stackBuffer.Length; i++) stackBuffer[i] = i * i;
        Console.WriteLine($"Stack buffer: [{string.Join(", ", stackBuffer.ToArray())}]");

        // Parsing integers from span without string allocation
        ReadOnlySpan<char> numStr = "12345".AsSpan();
        if (int.TryParse(numStr, out int parsed))
            Console.WriteLine($"Parsed from span: {parsed}");

        // Memory<T> — can be stored, passed to async methods
        Memory<byte> buffer = new byte[1024];
        Memory<byte> slice  = buffer.Slice(256, 128); // slices without copy
        Console.WriteLine($"Memory slice length: {slice.Length}");

        // Real-world: parse CSV row without allocating sub-strings
        Console.WriteLine("\nParsing CSV with Span (no allocations):");
        ParseCsvRow("Alice,30,Engineer,London".AsSpan());
    }

    private static void ParseCsvRow(ReadOnlySpan<char> row)
    {
        int col = 0;
        while (row.Length > 0)
        {
            int comma = row.IndexOf(',');
            var field = comma == -1 ? row : row[..comma];
            Console.WriteLine($"  Field {col++}: {field}");
            if (comma == -1) break;
            row = row[(comma + 1)..];
        }
    }
}

// ============================================================
// 3. GC, IDisposable, finalizers — memory management
//
// Q: What are the GC generations?
//    Gen0: short-lived objects (most die here). Collected most frequently, cheapest.
//    Gen1: survived Gen0 — buffer between Gen0 and Gen2.
//    Gen2: long-lived objects (static, singletons). Expensive to collect (full GC).
//    LOH (Large Object Heap): objects >= 85,000 bytes. Gen2-linked, rarely compacted.
// Q: When does a finalizer run?
//    Non-deterministic — GC thread, after the object is unreachable. At least one extra
//    GC cycle needed. Do NOT rely on it for resource cleanup.
// Q: Difference between Dispose and finalizer?
//    Dispose: deterministic, called by using block or by caller. Frees resources immediately.
//    Finalizer: safety net — runs if Dispose was not called. Slows GC.
// ============================================================

public class UnmanagedResource : IDisposable
{
    private IntPtr _handle; // pretend native handle
    private bool _disposed;

    public UnmanagedResource()
    {
        _handle = new IntPtr(42); // simulate acquiring native resource
        Console.WriteLine($"  [Resource] Acquired handle {_handle}");
    }

    // Called by consumer code or using statement
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this); // no need for GC to call finalizer
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Release managed resources (other IDisposable objects)
        }
        // Release unmanaged regardless of how Dispose was called
        if (_handle != IntPtr.Zero)
        {
            Console.WriteLine($"  [Resource] Releasing handle {_handle}");
            _handle = IntPtr.Zero;
        }
        _disposed = true;
    }

    // Finalizer: safety net if caller forgets Dispose
    ~UnmanagedResource()
    {
        Console.WriteLine("  [Resource] Finalizer called (Dispose was NOT called — memory leak pattern)");
        Dispose(disposing: false);
    }
}

public static class GCDemo
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== GC / IDisposable ===\n");

        // Correct: using ensures Dispose always called
        using (var res = new UnmanagedResource())
        {
            Console.WriteLine("  Working with resource...");
        } // Dispose called here

        // WeakReference — cache that doesn't prevent GC
        var data = new byte[1000];
        var weakRef = new WeakReference<byte[]>(data);
        data = null!; // release strong reference
        GC.Collect();

        if (weakRef.TryGetTarget(out var retrieved))
            Console.WriteLine("  WeakRef: still alive (GC hasn't collected yet)");
        else
            Console.WriteLine("  WeakRef: collected");

        // GC.Collect explicitly — almost never do this in production
        Console.WriteLine($"  Gen0 collections: {GC.CollectionCount(0)}");
        Console.WriteLine($"  Gen1 collections: {GC.CollectionCount(1)}");
        Console.WriteLine($"  Gen2 collections: {GC.CollectionCount(2)}");
        Console.WriteLine($"  Total memory: {GC.GetTotalMemory(false):N0} bytes");
    }
}

// ============================================================
// 4. Value types vs Reference types — boxing/unboxing
//
// Q: What is boxing?
//    Wrapping a value type in a heap-allocated object — implicit when assigning to object/interface.
// Q: Why is boxing expensive?
//    Heap allocation + GC pressure. In hot paths (tight loops, LINQ on List<int> vs List<object>)
//    this adds up.
// Q: How do you avoid boxing?
//    Use generics (List<int> not ArrayList), constrained generic methods, Span<T>.
// ============================================================

public interface IValue { int Get(); }
public struct MyValue : IValue
{
    private int _v;
    public MyValue(int v) => _v = v;
    public int Get() => _v;
}

public static class BoxingDemo
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Boxing / Unboxing ===\n");

        int x = 42;

        // Boxing: value type -> object (heap allocation)
        object boxed = x;
        Console.WriteLine($"boxed type: {boxed.GetType().Name}, value: {boxed}");

        // Unboxing: must cast to exact type
        int unboxed = (int)boxed;
        // double wrong = (double)boxed; // InvalidCastException at runtime!

        // Interface dispatch on struct causes boxing
        IValue v = new MyValue(99); // BOXES the struct
        Console.WriteLine($"Interface dispatch (boxed): {v.Get()}");

        // Avoid boxing with generic constraint
        static int GetValue<T>(T item) where T : IValue => item.Get(); // NO boxing
        Console.WriteLine($"Generic constrained (no box): {GetValue(new MyValue(99))}");

        // ArrayList (object-based) vs List<int> — boxing comparison
        var boxingList = new System.Collections.ArrayList();
        for (int i = 0; i < 100; i++) boxingList.Add(i); // 100 boxing allocations!

        var genericList = new List<int>();
        for (int i = 0; i < 100; i++) genericList.Add(i); // NO boxing
    }
}

// ============================================================
// 5. String internals — interning, StringBuilder
//
// Q: What is string interning?
//    The runtime maintains a pool of literal strings. Same literals share one object.
//    string.Intern() manually adds to pool. string.IsInterned() checks.
// Q: When should you use StringBuilder over string concatenation?
//    When concatenating in a loop — string is immutable, each + creates a new string.
//    StringBuilder amortises allocation (doubles capacity when full).
// Q: What is the complexity of string + in a loop vs StringBuilder?
//    string +: O(n²) — each concatenation copies all previous characters.
//    StringBuilder: O(n) amortised.
// ============================================================

public static class StringInternals
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== String internals ===\n");

        // Interning
        string a = "hello";
        string b = "hello";      // same literal — interned — same reference
        string c = new string(new[] { 'h', 'e', 'l', 'l', 'o' }); // NOT interned

        Console.WriteLine($"a == b (value): {a == b}");           // true
        Console.WriteLine($"ReferenceEquals(a,b): {ReferenceEquals(a, b)}");  // true (literals)
        Console.WriteLine($"ReferenceEquals(a,c): {ReferenceEquals(a, c)}");  // false

        string interned = string.Intern(c);
        Console.WriteLine($"ReferenceEquals(a, Intern(c)): {ReferenceEquals(a, interned)}"); // true

        // StringBuilder — O(n) concatenation
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sb = new System.Text.StringBuilder(capacity: 1024);
        for (int i = 0; i < 10_000; i++) sb.Append(i).Append(',');
        string result = sb.ToString();
        sw.Stop();
        Console.WriteLine($"StringBuilder 10k appends: {sw.Elapsed.TotalMilliseconds:F3}ms, len={result.Length}");

        // String.Format vs interpolation — both compile to same IL (in modern C#)
        // but interpolation has DefaultInterpolatedStringHandler optimization in .NET 6+
        int n = 42;
        string fmt   = string.Format("Value={0}", n);
        string interp = $"Value={n}"; // uses DefaultInterpolatedStringHandler — may avoid alloc
        Console.WriteLine($"Both: '{fmt}' == '{interp}': {fmt == interp}");
    }
}

// ============================================================
// 6. Reflection — inspect types at runtime
//
// Q: What is reflection and what are its costs?
//    Reflection: inspect metadata (types, methods, properties) and invoke at runtime.
//    Cost: slow (no JIT optimisation), bypasses type safety. Cache MethodInfo/PropertyInfo.
// Q: What are typical uses of reflection?
//    Serialisation/deserialisation, DI containers, ORM (EF Core), test frameworks,
//    plugin systems, attribute-based routing.
// ============================================================

[AttributeUsage(AttributeTargets.Property)]
public class RequiredFieldAttribute : Attribute
{
    public string? ErrorMessage { get; init; }
}

public class UserDto
{
    [RequiredField(ErrorMessage = "Name is required")]
    public string? Name { get; set; }

    [RequiredField(ErrorMessage = "Email is required")]
    public string? Email { get; set; }

    public int Age { get; set; }
}

public static class ReflectionDemo
{
    // Validation using reflection — reads custom attributes at runtime
    public static List<string> Validate(object obj)
    {
        var errors = new List<string>();
        var type = obj.GetType();

        foreach (var prop in type.GetProperties())
        {
            var attr = prop.GetCustomAttributes(typeof(RequiredFieldAttribute), inherit: false)
                          .FirstOrDefault() as RequiredFieldAttribute;
            if (attr == null) continue;

            var value = prop.GetValue(obj);
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                errors.Add(attr.ErrorMessage ?? $"{prop.Name} is required");
        }
        return errors;
    }

    // Create instance dynamically + set properties — how DI containers work internally
    public static T CreateAndPopulate<T>(Dictionary<string, object> values) where T : new()
    {
        var instance = new T();
        var props = typeof(T).GetProperties();
        foreach (var (key, value) in values)
        {
            var prop = props.FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            prop?.SetValue(instance, Convert.ChangeType(value, prop.PropertyType));
        }
        return instance;
    }

    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Reflection ===\n");

        // Custom attribute validation
        var dto = new UserDto { Name = null, Email = "alice@example.com", Age = 30 };
        var errors = Validate(dto);
        Console.WriteLine($"Validation errors: {string.Join(", ", errors)}");

        var valid = new UserDto { Name = "Alice", Email = "alice@example.com", Age = 30 };
        Console.WriteLine($"Valid dto errors: {Validate(valid).Count} (expected 0)");

        // Dynamic creation
        var user = CreateAndPopulate<UserDto>(new() { { "Name", "Bob" }, { "Age", "25" } });
        Console.WriteLine($"Created dynamically: Name={user.Name}, Age={user.Age}");

        // Inspect methods
        var methods = typeof(string).GetMethods(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("To"))
            .Select(m => m.Name)
            .Distinct();
        Console.WriteLine($"string.To*() methods: {string.Join(", ", methods.Take(5))}...");
    }
}

// ============================================================
// 7. Extension methods — rules and patterns
//
// Q: What are the rules for extension methods?
//    Static class, static method, first param is `this T`.
//    Cannot override existing instance methods. Resolved at compile time (not virtual).
// Q: Can you extend interfaces?
//    YES — and this is the correct way to add shared behaviour to interfaces.
// ============================================================

public static class EnumerableExtensions
{
    // Null-safe ForEach
    public static void ForEach<T>(this IEnumerable<T>? source, Action<T> action)
    {
        if (source == null) return;
        foreach (var item in source) action(item);
    }

    // Batch/chunk (pre .NET 6 implementation)
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
    {
        var batch = new List<T>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size) { yield return batch; batch = new List<T>(size); }
        }
        if (batch.Count > 0) yield return batch;
    }

    // Fluent null-check
    public static T? OrDefault<T>(this T? value, T? fallback) where T : class =>
        value ?? fallback;
}

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrEmpty(s);
    public static string Truncate(this string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "…";
    public static string ToSnakeCase(this string s) =>
        string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
}

public static class ExtensionMethodsDemo
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Extension Methods ===\n");

        var nums = Enumerable.Range(1, 10);
        foreach (var batch in nums.Batch(3))
            Console.Write($"[{string.Join(",", batch)}] ");
        Console.WriteLine();

        Console.WriteLine("Hello, World!".Truncate(8));
        Console.WriteLine("MyPropertyName".ToSnakeCase());

        string? maybe = null;
        Console.WriteLine(maybe.OrDefault("default value"));
    }
}

// ---------- Demo ----------
public static class SeniorDepthDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Senior Depth Topics Demo ===\n");
        GenericsDemo.Demonstrate();
        SpanMemoryDemo.Demonstrate();
        GCDemo.Demonstrate();
        BoxingDemo.Demonstrate();
        StringInternals.Demonstrate();
        ReflectionDemo.Demonstrate();
        ExtensionMethodsDemo.Demonstrate();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is the difference between struct and class for performance?
//      struct: value type, stack-allocated (usually), no GC pressure, copied on assignment.
//      class: reference type, heap-allocated, GC-managed.
//      Large structs can be SLOWER due to copy cost — use in/ref to avoid copies.
//   2. What is a ref struct and why can't it go on the heap?
//      ref struct (Span<T>, ReadOnlySpan<T>): can only live on stack. Cannot be boxed,
//      stored in fields, or used across async awaits. Enables safe stack-only operations.
//   3. What is the Large Object Heap (LOH) and why is it special?
//      Objects >= 85K bytes. Not compacted by default (fragmentation risk).
//      GC.Collect with LOHCompactionMode.CompactOnce to compact manually.
//   4. How do you diagnose memory leaks in .NET?
//      Event handler subscriptions not unsubscribed, static references, Dispose not called.
//      Tools: dotMemory, PerfView, `dotnet-dump analyze`, Visual Studio Diagnostic Tools.
//   5. What is source generators vs Reflection?
//      Source generators: compile-time code generation — zero runtime cost.
//      Used in System.Text.Json serialisation, EF Core compiled models, Regex.
//   6. What is the difference between `is` type check and `as`?
//      is: bool result, does NOT throw. as: returns null on failure (only ref/nullable types).
//      Prefer `is Type variable` pattern — combines check and cast in one step.
//   7. What is ArrayPool<T> and when do you use it?
//      Rent large arrays from a shared pool — avoids LOH allocation on every call.
//      Critical for high-throughput scenarios (ASP.NET request pipelines, IO buffers).
// ============================================================
