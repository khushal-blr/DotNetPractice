// ============================================================
// TOPIC: Tricky C# Keywords — depth questions for senior interviews
// ============================================================
// Covers: ref/out/in, readonly/const, virtual/override/new (hiding),
//         sealed, yield, using, checked/unchecked, params, default,
//         nameof/typeof/GetType, is/as, pattern matching,
//         record/init/required, nullable reference types
// ============================================================

namespace Practice.TrickyKeywords;

// ============================================================
// 1. ref vs out vs in parameters
//
// Q: What is the difference between ref and out?
//    ref: caller MUST initialise before passing; callee can read AND write.
//    out: caller does NOT need to initialise; callee MUST write before returning.
// Q: What is `in`?
//    Pass by reference but READ-ONLY — zero-copy for large structs, no accidental mutation.
// Q: When would you prefer out over returning a value?
//    When the method already has a return value (e.g. bool + value), like TryParse.
// ============================================================

public struct HeavyStruct
{
    public double X, Y, Z, W;
    public HeavyStruct(double x, double y, double z, double w) { X=x; Y=y; Z=z; W=w; }
}

public static class RefOutIn
{
    // out: no need to initialise result before passing; MUST assign inside
    public static bool TryDivide(int a, int b, out double result)
    {
        if (b == 0) { result = 0; return false; }   // must assign even on failure path
        result = (double)a / b;
        return true;
    }

    // ref: value MUST exist before call; callee reads the initial value
    public static void DoubleIt(ref int value) => value *= 2;

    // in: read-only by-reference; avoids copying large structs on every call
    public static double Magnitude(in HeavyStruct s) =>
        Math.Sqrt(s.X * s.X + s.Y * s.Y + s.Z * s.Z + s.W * s.W);

    // TRICKY: ref return — method returns a reference to an element in an array
    // Caller can modify the original array element through the reference
    public static ref int GetLargest(int[] arr)
    {
        ref int largest = ref arr[0];
        for (int i = 1; i < arr.Length; i++)
            if (arr[i] > largest) largest = ref arr[i];
        return ref largest;
    }

    public static void Demonstrate()
    {
        Console.WriteLine("--- ref / out / in ---");

        if (TryDivide(10, 3, out double r)) Console.WriteLine($"10/3 = {r:F4}");
        if (!TryDivide(10, 0, out _)) Console.WriteLine("Division by zero caught");

        int n = 5;
        DoubleIt(ref n);
        Console.WriteLine($"DoubleIt(5) = {n}");  // 10

        var s = new HeavyStruct(1, 2, 3, 4);
        Console.WriteLine($"Magnitude = {Magnitude(in s):F4}");  // in: no copy

        int[] arr = { 3, 7, 2, 9, 1 };
        ref int max = ref GetLargest(arr);
        max = 99;  // modifies arr[3] in place
        Console.WriteLine($"arr after ref return modify: [{string.Join(", ", arr)}]"); // 99 at index 3
    }
}

// ============================================================
// 2. readonly vs const
//
// Q: What is the key difference?
//    const: compile-time constant, baked into calling assembly IL.
//          If you change a const in a library, callers must recompile.
//    readonly: runtime constant, evaluated once (field initialiser or constructor).
//              Safe to change without requiring callers to recompile.
// Q: What is a readonly struct?
//    All fields are readonly, defensive copy is skipped by JIT, can be used with in.
// Q: What is readonly ref return?
//    Returns a reference but callee cannot modify through it.
// ============================================================

public class ConstVsReadonly          // NOT static — has instance state
{
    public const    double Pi    = 3.14159;       // compile-time literal — baked into IL at call site
    public readonly double Euler;                  // set in constructor — runtime

    public ConstVsReadonly() => Euler = Math.E;   // can assign readonly in constructor

    // readonly struct: all instance fields implicitly readonly, better perf with in param
    public readonly struct Point
    {
        public readonly double X, Y;
        public Point(double x, double y) { X = x; Y = y; }
        public double DistanceTo(in Point other) =>
            Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    }

    public static void Demonstrate()
    {
        Console.WriteLine("\n--- const vs readonly ---");
        Console.WriteLine($"Pi (const)  = {Pi}");
        var instance = new ConstVsReadonly();
        Console.WriteLine($"Euler (readonly) = {instance.Euler:F5}");

        var p1 = new Point(0, 0);
        var p2 = new Point(3, 4);
        Console.WriteLine($"Distance = {p1.DistanceTo(in p2)}"); // 5.0
    }
}

// ============================================================
// 3. virtual / override / new — method hiding (VERY tricky in interviews)
//
// Q: What is the difference between override and new?
//    override: polymorphic — base class reference calls the derived implementation.
//    new: hides the base method — base class reference still calls BASE implementation.
// Q: When would you use new keyword on a method?
//    When you intentionally want to hide a base member without polymorphism.
//    (Rare — usually a design smell.)
// ============================================================

public class Animal
{
    public virtual  string Speak()  => "...";      // virtual: subclass can override
    public          string Breathe() => "Animal breathes"; // non-virtual
}

public class Dog : Animal
{
    public override string Speak()   => "Woof";    // polymorphic override
    public new      string Breathe() => "Dog breathes"; // hides — NOT polymorphic
}

public class Cat : Animal
{
    public override string Speak() => "Meow";
}

public static class VirtualOverrideNew
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- virtual / override / new (method hiding) ---");

        Animal a1 = new Dog();
        Animal a2 = new Cat();
        Dog    d  = new Dog();

        Console.WriteLine(a1.Speak());     // "Woof"  — override: polymorphic dispatch
        Console.WriteLine(a2.Speak());     // "Meow"

        Console.WriteLine(a1.Breathe());   // "Animal breathes" — non-virtual, base called!
        Console.WriteLine(d.Breathe());    // "Dog breathes"    — direct Dog reference uses hiding

        // GOTCHA: cast doesn't change dispatch for virtual methods
        Console.WriteLine(((Animal)d).Speak());   // "Woof"  — still Dog (virtual)
        Console.WriteLine(((Animal)d).Breathe()); // "Animal breathes" — base (non-virtual)
    }
}

// ============================================================
// 4. sealed — prevent inheritance or further override
//
// Q: What does sealing a class do?
//    Prevents any class from inheriting it — also allows JIT to devirtualise calls (perf).
// Q: What does sealed on a method mean?
//    Derived class that overrode this method cannot be further overridden in subclasses.
// ============================================================

public class Shape { public virtual string Kind() => "shape"; }
public class Circle : Shape { public sealed override string Kind() => "circle"; } // no further override
// public class FancyCircle : Circle { public override string Kind() => "fancy"; } // ERROR — sealed

public sealed class ImmutableConfig   // nobody can extend this
{
    public string ConnectionString { get; } = "Server=.;Database=prod";
}

// ============================================================
// 5. yield return / yield break — lazy iterator state machine
//
// Q: What does yield return generate under the hood?
//    Compiler generates a state machine class implementing IEnumerable<T> and IEnumerator<T>.
//    Values produced on demand — nothing computed until iterated.
// Q: What is yield break?
//    Terminates the sequence (like return in a normal method).
// Q: What is the pitfall of multiple enumeration with yield?
//    Each iteration restarts the state machine from scratch — expensive if source is a DB call.
// ============================================================

public static class YieldExamples
{
    // Lazy range — no list allocation
    public static IEnumerable<int> Range(int from, int to, int step = 1)
    {
        for (int i = from; i <= to; i += step)
            yield return i;
    }

    // yield break: stop early based on condition
    public static IEnumerable<int> TakeUntilNegative(IEnumerable<int> source)
    {
        foreach (var item in source)
        {
            if (item < 0) yield break;  // stop the sequence
            yield return item;
        }
    }

    // Pipeline of lazy transforms — nothing evaluates until terminal .ToList()
    public static IEnumerable<string> ProcessLog(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var trimmed = line.Trim();
            if (trimmed.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                yield return $"[ALERT] {trimmed}";
        }
    }

    public static void Demonstrate()
    {
        Console.WriteLine("\n--- yield ---");

        Console.Write("Range(1,10,2): ");
        foreach (var n in Range(1, 10, 2)) Console.Write(n + " ");
        Console.WriteLine();

        Console.Write("TakeUntilNegative: ");
        foreach (var n in TakeUntilNegative(new[] { 3, 1, 4, -1, 5, 9 })) Console.Write(n + " ");
        Console.WriteLine();

        var logs = new[] { "INFO start", "ERROR disk full", "  ", "ERROR null ref" };
        foreach (var alert in ProcessLog(logs)) Console.WriteLine(alert);
    }
}

// ============================================================
// 6. using — three meanings
//
// Q: What are the three uses of `using` in C#?
//    1. Namespace import: using System;
//    2. using statement: guaranteed Dispose() in try/finally block.
//    3. using declaration (C#8+): Dispose() at end of enclosing scope.
// Q: What does IDisposable do vs IAsyncDisposable?
//    IDisposable.Dispose(): synchronous cleanup. IAsyncDisposable: async cleanup (e.g., flush stream).
// Q: When should you implement a finalizer alongside Dispose?
//    When the class holds unmanaged resources (native handles). The finalizer is a safety net
//    if the caller forgets to call Dispose. Implement full dispose pattern.
// ============================================================

public class DatabaseConnection : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private readonly string _name;

    public DatabaseConnection(string name)
    {
        _name = name;
        Console.WriteLine($"  [DB] Opening connection: {_name}");
    }

    public void ExecuteQuery(string sql) =>
        Console.WriteLine($"  [DB] Executing on {_name}: {sql}");

    // Full Dispose pattern
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this); // tell GC: no need to call finalizer
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
            Console.WriteLine($"  [DB] Closing managed resources: {_name}");
        // free unmanaged resources here regardless of `disposing`
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Delay(10); // simulate async flush
        Console.WriteLine($"  [DB] Async dispose: {_name}");
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    ~DatabaseConnection() => Dispose(disposing: false); // finalizer safety net
}

public static class UsingExamples
{
    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- using ---");

        // using statement — Dispose() called at closing brace
        using (var conn1 = new DatabaseConnection("conn1"))
            conn1.ExecuteQuery("SELECT 1");

        // using declaration (C#8+) — Dispose() at end of method/block scope
        using var conn2 = new DatabaseConnection("conn2");
        conn2.ExecuteQuery("SELECT 2");
        // conn2.Dispose() called here when method ends

        // await using — calls DisposeAsync
        await using var conn3 = new DatabaseConnection("conn3-async");
        conn3.ExecuteQuery("SELECT 3");
    }
}

// ============================================================
// 7. checked / unchecked — integer overflow
//
// Q: What happens on integer overflow in C# by default?
//    UNCHECKED — value wraps silently (no exception). Very subtle bug.
// Q: When would you use checked?
//    Financial calculations, array index arithmetic — anywhere silent overflow is dangerous.
// ============================================================

public static class CheckedExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- checked / unchecked ---");

        int max = int.MaxValue; // 2,147,483,647

        // Default: wraps silently
        int wrapped = max + 1;
        Console.WriteLine($"Unchecked overflow: {max} + 1 = {wrapped}"); // -2147483648!

        // checked: throws OverflowException
        try
        {
            int safe = checked(max + 1);
        }
        catch (OverflowException)
        {
            Console.WriteLine("checked: OverflowException caught");
        }

        // checked block — all arithmetic inside is checked
        try
        {
            checked
            {
                int a = int.MaxValue;
                int b = a * 2; // throws
            }
        }
        catch (OverflowException) { Console.WriteLine("checked block caught overflow"); }
    }
}

// ============================================================
// 8. params — variable argument lists
//
// Q: What does params do?
//    Allows passing any number of arguments; compiler wraps them in an array.
// Q: What is the TRICKY rule with params?
//    Must be the LAST parameter. Cannot have ref/out params alongside it.
//    If you pass an array, it is used as-is. Null is legal.
// ============================================================

public static class ParamsExamples
{
    // params: zero or more ints
    public static int Sum(params int[] numbers)
    {
        int total = 0;
        foreach (var n in numbers) total += n;
        return total;
    }

    // Mixed — params must come last
    public static string Format(string template, params object[] args) =>
        string.Format(template, args);

    public static void Demonstrate()
    {
        Console.WriteLine("\n--- params ---");
        Console.WriteLine(Sum());               // 0  — zero args allowed
        Console.WriteLine(Sum(1, 2, 3));        // 6
        Console.WriteLine(Sum(new[] { 1, 2, 3, 4 })); // 10 — array passed directly
        Console.WriteLine(Format("Hello {0}, you are {1}!", "Alice", 30));
    }
}

// ============================================================
// 9. nameof / typeof / GetType
//
// Q: Difference between nameof, typeof, GetType()?
//    nameof:   compile-time string of identifier — refactor-safe, zero runtime cost.
//    typeof:   compile-time Type object for a known type — no instance needed.
//    GetType(): runtime Type of actual instance — reflects polymorphic type.
// Q: When do you use nameof vs hardcoded string?
//    Property validation, ArgumentException, logging — rename refactors still work.
// ============================================================

public static class NameofTypeofGetType
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- nameof / typeof / GetType ---");

        // nameof — compile-time, refactor-safe
        string propName = nameof(DatabaseConnection);
        Console.WriteLine($"nameof: {propName}");  // "DatabaseConnection"

        // ArgumentException pattern — refactor safe
        void Validate(string? value, string paramName)
        {
            if (value == null) throw new ArgumentNullException(nameof(value)); // auto name
        }

        // typeof — compile-time Type, no instance
        Type t1 = typeof(string);
        Console.WriteLine($"typeof(string): {t1.FullName}");

        // GetType() — runtime, returns ACTUAL type
        object obj = "hello";
        Type t2 = obj.GetType();
        Console.WriteLine($"obj.GetType(): {t2.Name}");  // String

        object dog = new Dog();
        Console.WriteLine($"typeof: {typeof(Animal).Name}, GetType: {dog.GetType().Name}");
        // typeof = Animal, GetType = Dog (runtime actual type)
    }
}

// ============================================================
// 10. is / as — safe casting + pattern matching
//
// Q: Difference between is and as?
//    is: returns bool (type check). Modern form: if (x is Dog d) — type pattern with variable.
//    as: returns null if cast fails (reference/nullable types only). No exception.
//    (Cast): throws InvalidCastException on failure.
// Q: What are the pattern matching forms?
//    Type pattern, constant pattern, property pattern, relational pattern, logical pattern,
//    positional pattern (deconstruct), var pattern, discard pattern.
// ============================================================

public static class IsAsPatterns
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- is / as / patterns ---");

        object[] items = { "hello", 42, 3.14, new Dog(), null!, new Cat() };

        foreach (var item in items)
        {
            // Type pattern — is with declaration
            if (item is string s) { Console.WriteLine($"string: '{s}'"); continue; }
            if (item is int i)    { Console.WriteLine($"int:    {i}");   continue; }
            if (item is Animal a) { Console.WriteLine($"Animal: {a.Speak()}"); continue; }
            if (item is null)     { Console.WriteLine("null");           continue; }
            Console.WriteLine($"other: {item}");
        }

        // Property pattern — check properties inline
        var orders = new[] {
            new { Amount = 150m, Status = "Paid" },
            new { Amount = 50m,  Status = "Pending" },
            new { Amount = 200m, Status = "Paid" }
        };

        foreach (var o in orders)
        {
            var label = o switch
            {
                { Status: "Paid",    Amount: > 100 } => "VIP paid",
                { Status: "Paid"                   } => "Regular paid",
                { Status: "Pending"                } => "Needs follow-up",
                _                                    => "Unknown"
            };
            Console.WriteLine($"  ${o.Amount} [{o.Status}] => {label}");
        }

        // Relational + logical patterns
        static string Classify(int score) => score switch
        {
            >= 90          => "A",
            >= 80 and < 90 => "B",
            >= 70 and < 80 => "C",
            _              => "F"   // < 70 and any other int
        };
        Console.WriteLine($"85 => {Classify(85)}, 92 => {Classify(92)}, 65 => {Classify(65)}");
    }
}

// ============================================================
// 11. record / record struct / init / required
//
// Q: What does record give you automatically?
//    Value equality (Equals/GetHashCode based on properties), ToString, Deconstruct,
//    copy constructor, with expression for non-destructive mutation.
// Q: Difference between record class and record struct?
//    record class: heap-allocated, reference type. record struct: value type, stack allocated.
// Q: What is `init` accessor?
//    Property can only be set during object initialisation (not after construction).
// Q: What is `required` (C#11)?
//    Property MUST be set in the object initialiser — compile-time enforcement.
// ============================================================

// record class — immutable, value equality, with-expression
public record OrderSummary(int OrderId, string Customer, decimal Total)
{
    // init-only extra property
    public string Currency { get; init; } = "USD";
}

// record struct — value type, same features
public record struct Coordinate(double Lat, double Lon);

public class InitRequired
{
    // required: compiler error if not set in object initialiser
    public required string Name { get; init; }
    public required string Email { get; init; }
    public int Age { get; init; } // optional
}

public static class RecordExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- record / init / required ---");

        var o1 = new OrderSummary(1, "Alice", 99.99m) { Currency = "GBP" };
        var o2 = new OrderSummary(1, "Alice", 99.99m) { Currency = "GBP" };
        var o3 = new OrderSummary(2, "Bob",   49.99m);

        Console.WriteLine($"o1 == o2 (value eq): {o1 == o2}"); // True — value equality
        Console.WriteLine($"o1 == o3: {o1 == o3}");            // False

        // with-expression: non-destructive mutation — creates new record
        var upgraded = o1 with { Total = 199.99m, Currency = "EUR" };
        Console.WriteLine($"Original:  {o1}");
        Console.WriteLine($"Upgraded:  {upgraded}");

        // Deconstruct
        var (id, cust, total) = o1;
        Console.WriteLine($"Deconstructed: id={id}, cust={cust}, total={total}");

        // record struct — stack allocated
        var c1 = new Coordinate(51.5, -0.12);
        var c2 = c1 with { Lon = 0.0 };
        Console.WriteLine($"c1={c1}, c2={c2}");

        // required
        var user = new InitRequired { Name = "Alice", Email = "alice@example.com", Age = 30 };
        // var bad = new InitRequired { Age = 25 }; // CS9035 — Name and Email required
        Console.WriteLine($"User: {user.Name} <{user.Email}> age {user.Age}");
    }
}

// ============================================================
// 12. Nullable Reference Types (NRT) — C#8+
//
// Q: What problem do NRTs solve?
//    Annotate the compiler's understanding of nullability — catch NullReferenceExceptions at compile time.
// Q: What is the difference between `string` and `string?` with NRTs enabled?
//    string: not-nullable — compiler warns if null assigned or returned.
//    string?: nullable — compiler warns if accessed without null check.
// Q: What does the `!` null-forgiving operator do?
//    Tells compiler "I know this isn't null" — suppresses the warning. Use sparingly.
// ============================================================

#nullable enable
public class NullableRefTypes
{
    private readonly Dictionary<string, string?> _metadata = new();

    public string GetRequired(string key)
    {
        if (!_metadata.TryGetValue(key, out var value) || value is null)
            throw new KeyNotFoundException($"Key '{key}' not found or null");
        return value; // compiler knows value is non-null here
    }

    public string? GetOptional(string key) =>
        _metadata.TryGetValue(key, out var value) ? value : null;

    public void Set(string key, string? value) => _metadata[key] = value;

    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Nullable Reference Types ---");
        var store = new NullableRefTypes();
        store.Set("name", "Alice");
        store.Set("bio", null);

        Console.WriteLine(store.GetRequired("name"));

        var bio = store.GetOptional("bio");
        // Without null check, compiler warns: "bio may be null"
        Console.WriteLine(bio is null ? "No bio" : bio.ToUpper());

        // Null-coalescing and conditional access
        string? maybeNull = null;
        Console.WriteLine(maybeNull?.Length ?? -1);         // -1
        string result = maybeNull ?? "default";             // "default"
        maybeNull ??= "assigned if null";                   // ??= assignment
        Console.WriteLine(maybeNull);
    }
}
#nullable restore

// ---------- Demo ----------
public static class TrickyKeywordsDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Tricky Keywords Demo ===\n");
        RefOutIn.Demonstrate();
        ConstVsReadonly.Demonstrate();
        VirtualOverrideNew.Demonstrate();
        YieldExamples.Demonstrate();
        await UsingExamples.Demonstrate();
        CheckedExamples.Demonstrate();
        ParamsExamples.Demonstrate();
        NameofTypeofGetType.Demonstrate();
        IsAsPatterns.Demonstrate();
        RecordExamples.Demonstrate();
        NullableRefTypes.Demonstrate();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is the difference between `new` constraint on a generic and `new()` keyword?
//      Generic: where T : new() means T must have a public parameterless constructor.
//   2. Can you have a `readonly` local variable? YES — readonly ref locals.
//   3. Why does `const string` get embedded in assemblies but `static readonly string` doesn't?
//      const is substituted at compile time as a literal. Changing a const library value
//      requires recompiling the caller — ABI break.
//   4. What is the `global` keyword in C#10?
//      global using Foo; — applies the using to the entire project, every file.
//   5. What is `file` scoped type (C#11)?
//      file class Foo { } — visible only within the same source file. Good for helpers.
//   6. What is `required` and how does it differ from a non-null constructor parameter?
//      required enforces object-initializer assignment. Constructor enforces at construction.
//      required works better with object initialiser style (EF model classes, DTOs).
//   7. What is the `scoped` keyword in C#11?
//      Used with ref struct or Span<T> to prevent the value from escaping the current method.
// ============================================================
