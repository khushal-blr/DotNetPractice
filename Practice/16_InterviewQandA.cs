// ============================================================
// INTERVIEW Q&A + OUTPUT PREDICTION SNIPPETS
// ============================================================
// FORMAT:
//   - Top Q&A: most-asked concepts with crisp answers
//   - Bottom: "What does this print?" tricky code snippets with answers + explanations
//
// HOW TO USE:
//   Read a question, cover the answer, try to answer from memory.
//   For output snippets, trace through in your head BEFORE reading the answer.
// ============================================================

namespace Practice.InterviewQandA;

/*
════════════════════════════════════════════════════════════
  SECTION 1 — OOP FUNDAMENTALS
════════════════════════════════════════════════════════════

Q: What are the four pillars of OOP?
A: Encapsulation, Abstraction, Inheritance, Polymorphism.
   - Encapsulation: bundle data + behaviour; hide internal state (private fields, public properties).
   - Abstraction: expose only what's necessary (abstract class, interface).
   - Inheritance: IS-A relationship; derived class reuses base class.
   - Polymorphism: same interface, different behaviour (virtual/override, interface dispatch).

Q: What is the difference between abstract class and interface?
A: Abstract class: single inheritance, can have state (fields), constructors, concrete methods.
              Use when classes SHARE implementation.
   Interface:  multiple implementation, pure contract (pre-C#8), no state.
              Use to define a CAPABILITY any unrelated type can satisfy.
   C#8+: interfaces can have default method bodies, but still no instance fields.

Q: Can an abstract class have a constructor?
A: YES. Called via base() from the derived class constructor. Cannot be called directly
   because abstract classes cannot be instantiated.

Q: What is method hiding (new keyword) vs overriding?
A: override: polymorphic — calling through a base reference dispatches to derived implementation.
   new:       hides — calling through a base reference still calls the BASE method.
   RULE: if in doubt, use override. new is a design smell in most cases.

Q: What is the difference between IS-A and HAS-A?
A: IS-A = inheritance (Dog extends Animal).
   HAS-A = composition (Car has an Engine).
   Prefer HAS-A (composition) over IS-A (inheritance) for flexibility. Inheritance is a strong,
   fragile coupling — changes in the base class affect all subclasses.

Q: What is the Liskov Substitution Principle?
A: A subtype must be usable wherever the base type is expected WITHOUT breaking correctness.
   Classic violation: Square extends Rectangle — setting Width changes Height, breaking callers
   that assume Width and Height are independent.

════════════════════════════════════════════════════════════
  SECTION 2 — C# LANGUAGE SPECIFICS
════════════════════════════════════════════════════════════

Q: What is the difference between value type and reference type?
A: Value type (struct, int, enum): stored inline (stack or inside containing object),
   copied on assignment, no GC overhead.
   Reference type (class, interface, delegate): heap-allocated, GC-managed,
   copy copies the REFERENCE not the object.

Q: What is boxing and unboxing? Why is it bad?
A: Boxing: wrapping a value type in a heap object (int → object). Allocates on heap.
   Unboxing: casting back (object → int). Must match exact type or InvalidCastException.
   Bad: allocations in hot paths = GC pressure + cache misses. Fix: use generics, Span<T>.

Q: What is the difference between String and StringBuilder?
A: string: immutable — each concatenation creates a new string. O(n²) in a loop.
   StringBuilder: mutable buffer, amortised O(n). Use when concatenating in a loop
   or building large strings (>3-4 concatenations).

Q: What is the difference between == and .Equals() for strings?
A: == on strings is overloaded to do VALUE comparison (same as Equals).
   .Equals() also value-compares. For reference equality: ReferenceEquals().
   BUT: == on object compiles to reference equality. Know your static type.

Q: What is readonly vs const?
A: const: compile-time literal, embedded in caller's IL at compile time.
          Changing a const in a library requires callers to recompile (ABI break).
   readonly: set once (field initialiser or constructor), evaluated at runtime.
             Safe to change in libraries. Use readonly by default.

Q: What does `using` do in three different contexts?
A: 1. Namespace import:     using System;
   2. Resource disposal:    using var conn = new DbConnection(); → calls Dispose() at end of scope.
   3. Alias:                using Str = System.String;

Q: What is the difference between ref, out, and in parameters?
A: ref: pass by reference, caller must initialise, callee can read + write.
   out: caller doesn't initialise, callee MUST assign before return.
   in:  pass by reference, READ-ONLY — zero-copy for large structs.

Q: What is a record type in C#9?
A: Shorthand for an immutable reference type with: value equality (Equals/GetHashCode by value),
   ToString, Deconstruct, and with-expression for non-destructive mutation.
   record struct: same but value type.

Q: What is init accessor?
A: Property can only be set during object initialisation (constructor or { } initialiser).
   After that, it's effectively readonly.

════════════════════════════════════════════════════════════
  SECTION 3 — DELEGATES & EVENTS
════════════════════════════════════════════════════════════

Q: What is a delegate?
A: A type-safe function pointer. A variable that holds a reference to a method (or lambda).
   Under the hood: a sealed class extending MulticastDelegate.

Q: What is the difference between Func, Action, and Predicate?
A: Func<T,TResult>:  takes input, returns output.      Func<int,bool>
   Action<T>:        takes input, returns void.         Action<string>
   Predicate<T>:     equivalent to Func<T,bool>.        Used in List.FindAll().

Q: What is a multicast delegate?
A: A delegate variable holding multiple method references. All invoked in order on call.
   Only the LAST return value is captured. If one throws, remaining are NOT called.
   Fix: iterate GetInvocationList() in a try/catch.

Q: What is the difference between event and delegate field?
A: event: outside classes can ONLY += and -=. Cannot invoke or assign (=).
   Raw delegate field: any code can invoke or replace the entire invocation list.
   event = encapsulated delegate — use event for public facing publish/subscribe.

Q: What is a closure?
A: A lambda that captures variables from its enclosing scope by REFERENCE.
   Classic bug: capturing a loop variable — all lambdas share the same reference,
   see the final value. Fix: copy to a local variable inside the loop.

════════════════════════════════════════════════════════════
  SECTION 4 — ASYNC & THREADING
════════════════════════════════════════════════════════════

Q: What does async/await compile to?
A: A state machine. The method is split at each await point. When the awaited task
   completes, execution resumes (continues) via a callback on the captured context.

Q: What is the difference between Task.Run and async/await?
A: Task.Run: explicitly queues work to a ThreadPool thread — for CPU-bound work.
   async/await: for I/O-bound work — releases thread while waiting, no new thread needed.

Q: What causes a deadlock in async code?
A: Calling .Result or .Wait() on a task inside a method that has a SynchronizationContext
   (WPF, WinForms, old ASP.NET) — blocks the context thread, which the continuation
   needs to resume. Fix: ConfigureAwait(false) in library code, or go async all the way.

Q: What is ConfigureAwait(false)?
A: After await, don't capture the current SynchronizationContext for the continuation.
   Use in library/service code. Not needed in ASP.NET Core (no sync context).

Q: What is the difference between lock and Monitor?
A: lock is syntactic sugar: Monitor.Enter(obj); try { ... } finally { Monitor.Exit(obj); }
   Monitor has extra: TryEnter (timeout), Wait (release lock + suspend), Pulse/PulseAll.

Q: When do you use SemaphoreSlim vs lock?
A: lock: exclusive access (1 thread at a time), synchronous only.
   SemaphoreSlim: N concurrent threads allowed. Supports async waiting (WaitAsync).
   Use SemaphoreSlim for: connection pool, rate limiting, async-compatible locking.

Q: What is the difference between Mutex and lock?
A: lock: in-process only, fast (no kernel mode).
   Mutex: cross-process, can be named (system-wide). Much slower due to kernel mode.
   Use Mutex only for: single-instance enforcement, cross-process synchronisation.

Q: What is Interlocked and when is it better than lock?
A: CPU-level atomic operations: Increment, Decrement, Add, CompareExchange.
   No kernel mode, no lock contention. Faster than lock for simple counter/flag updates.

════════════════════════════════════════════════════════════
  SECTION 5 — LINQ
════════════════════════════════════════════════════════════

Q: What is deferred execution in LINQ?
A: The query is NOT evaluated when declared — only when iterated (foreach, ToList(), Count(), etc.).
   Corollary: calling the query twice iterates the source twice.

Q: What is the difference between IEnumerable and IQueryable?
A: IEnumerable: in-memory iteration, LINQ executed in C#.
   IQueryable: expression tree, provider translates to SQL (EF Core) or other query.
   Putting .AsEnumerable() before a filter loads ALL rows first — major bug with large tables.

Q: What is SelectMany?
A: Projects each element to a sequence, then flattens all into one sequence.
   Think: nested foreach { foreach }.  orders.SelectMany(o => o.Lines) = all lines flat.

Q: What is the difference between GroupBy and ToLookup?
A: GroupBy: deferred — re-groups every time you iterate.
   ToLookup: eager — builds a lookup structure once. O(1) per key. Best for batch processing.

Q: What is the N+1 problem?
A: Loading a list (1 query) then accessing a navigation property per element (N queries).
   Fix: use .Include() in EF Core (eager load) or a JOIN to fetch in one query.

════════════════════════════════════════════════════════════
  SECTION 6 — .NET RUNTIME / SENIOR TOPICS
════════════════════════════════════════════════════════════

Q: What are the GC generations?
A: Gen0: new short-lived objects, cheapest to collect, most frequent.
   Gen1: survived Gen0 — buffer zone.
   Gen2: long-lived (statics, caches), expensive full GC.
   LOH: objects >= 85KB, not compacted by default (use ArrayPool to avoid).

Q: What is Span<T> and why is it useful?
A: Ref struct — a view over contiguous memory (array, stack, native) with no heap allocation.
   Enables zero-copy slicing of strings/arrays. Cannot cross async await boundaries.

Q: What is the difference between Span<T> and Memory<T>?
A: Span<T>: stack only, cannot be stored in fields or used across awaits.
   Memory<T>: can live on heap, safe across awaits. Slightly more overhead than Span.

Q: What is string interning?
A: The runtime interns string literals at compile time — identical literals share one reference.
   string.Intern() adds to the pool. Saves memory for repeated equal strings.

Q: What is the difference between shallow copy and deep copy?
A: Shallow: copies the reference — both variables point to same heap objects.
   Deep: recursively copies all referenced objects — fully independent clone.
   record with-expression is a shallow copy.

Q: What is dependency injection (DI) and what problem does it solve?
A: Passing dependencies into a class from outside rather than creating them inside.
   Solves tight coupling, enables unit testing (inject mocks), follows DIP.
   .NET DI container lifetimes: Transient (new each time), Scoped (per request), Singleton (one ever).

Q: What is a memory leak in .NET and how do you find it?
A: Long-lived references preventing GC collection:
   - Event handlers not unsubscribed (publisher outlives subscriber)
   - Static collections accumulating without cleanup
   - Disposable objects not disposed (holding unmanaged resources)
   Tools: dotMemory, PerfView, dotnet-dump, VS Diagnostic Tools.
*/

// ============================================================
// SECTION 7 — OUTPUT PREDICTION SNIPPETS (tricky!)
// For each: read the code, predict the output, then read the answer.
// ============================================================

public static class OutputSnippets
{
    public static void RunAll()
    {
        Snippet01_StringEquality();
        Snippet02_ValueTypeRef();
        Snippet03_VirtualHiding();
        Snippet04_ClosureBug();
        Snippet05_NullCoalescing();
        Snippet06_YieldDeferred();
        Snippet07_AsyncVoid();
        Snippet08_StaticConstructor();
        Snippet09_InterfaceDefault();
        Snippet10_RecordEquality();
        Snippet11_BoxingEquality();
        Snippet12_ExceptionFinally();
        Snippet13_DelegateReturn();
        Snippet14_LinqDeferred();
        Snippet15_OverflowWrap();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 01 — String equality
    // ──────────────────────────────────────────────────────
    /*
    string s1 = "hello";
    string s2 = "hello";
    string s3 = new string("hello");
    Console.WriteLine(s1 == s2);
    Console.WriteLine(ReferenceEquals(s1, s2));
    Console.WriteLine(s1 == s3);
    Console.WriteLine(ReferenceEquals(s1, s3));
    */
    static void Snippet01_StringEquality()
    {
        Console.WriteLine("=== Snippet 01 — String equality ===");
        string s1 = "hello";
        string s2 = "hello";
        string s3 = new string("hello".ToCharArray());

        Console.WriteLine(s1 == s2);                  // OUTPUT: True  — value equality, literals interned
        Console.WriteLine(ReferenceEquals(s1, s2));   // OUTPUT: True  — same interned reference
        Console.WriteLine(s1 == s3);                  // OUTPUT: True  — == is overloaded for value comparison
        Console.WriteLine(ReferenceEquals(s1, s3));   // OUTPUT: False — s3 is new heap object, not interned

        // EXPLANATION: string == compares values. Literals share reference (interned).
        // new string(...) forces a new heap allocation — not in the intern pool.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 02 — Struct assignment (value copy)
    // ──────────────────────────────────────────────────────
    /*
    struct Point { public int X, Y; }
    var p1 = new Point { X = 1, Y = 2 };
    var p2 = p1;   // copy
    p2.X = 99;
    Console.WriteLine(p1.X);   // ?
    Console.WriteLine(p2.X);   // ?
    */
    struct Point_S01 { public int X, Y; }
    static void Snippet02_ValueTypeRef()
    {
        Console.WriteLine("=== Snippet 02 — Struct copy ===");
        var p1 = new Point_S01 { X = 1, Y = 2 };
        var p2 = p1;      // full value copy
        p2.X = 99;
        Console.WriteLine(p1.X);  // OUTPUT: 1  — p1 unaffected; structs are COPIED
        Console.WriteLine(p2.X);  // OUTPUT: 99
        // EXPLANATION: Struct assignment copies ALL fields. Modifying p2 doesn't touch p1.
        // If Point were a class, both would see 99.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 03 — Method hiding vs override
    // ──────────────────────────────────────────────────────
    /*
    class A { public virtual  string Foo() => "A"; public string Bar() => "A"; }
    class B : A { public override string Foo() => "B"; public new string Bar() => "B"; }

    A obj = new B();
    Console.WriteLine(obj.Foo());  // ?
    Console.WriteLine(obj.Bar());  // ?
    */
    class A_S03 { public virtual string Foo() => "A"; public string Bar() => "A"; }
    class B_S03 : A_S03 { public override string Foo() => "B"; public new string Bar() => "B"; }

    static void Snippet03_VirtualHiding()
    {
        Console.WriteLine("=== Snippet 03 — Virtual vs hiding ===");
        A_S03 obj = new B_S03();
        Console.WriteLine(obj.Foo());  // OUTPUT: B — override: polymorphic dispatch uses runtime type
        Console.WriteLine(obj.Bar());  // OUTPUT: A — new hides, static dispatch uses compile-time type A
        // EXPLANATION: Foo is virtual — runtime dispatches to B_S03.Foo().
        // Bar uses `new` — not polymorphic. Static type is A, so A.Bar() is called.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 04 — Closure loop capture bug
    // ──────────────────────────────────────────────────────
    /*
    var funcs = new List<Func<int>>();
    for (int i = 0; i < 3; i++)
        funcs.Add(() => i);
    funcs.ForEach(f => Console.WriteLine(f()));
    */
    static void Snippet04_ClosureBug()
    {
        Console.WriteLine("=== Snippet 04 — Closure loop capture ===");
        var funcs = new List<Func<int>>();
        for (int i = 0; i < 3; i++)
            funcs.Add(() => i);  // captures REFERENCE to i, not value
        funcs.ForEach(f => Console.Write(f() + " "));
        Console.WriteLine();
        // OUTPUT: 3 3 3
        // EXPLANATION: All lambdas share one reference to the same `i`. Loop ends at i=3.
        // When they run, i is 3. Fix: int captured = i; funcs.Add(() => captured);
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 05 — Null coalescing and conditional
    // ──────────────────────────────────────────────────────
    /*
    string? s = null;
    Console.WriteLine(s?.Length ?? -1);
    Console.WriteLine(s?.ToUpper() ?? "default");
    s ??= "assigned";
    Console.WriteLine(s);
    */
    static void Snippet05_NullCoalescing()
    {
        Console.WriteLine("=== Snippet 05 — Null coalescing ===");
        string? s = null;
        Console.WriteLine(s?.Length ?? -1);          // OUTPUT: -1   — s is null, ?. returns null, ?? gives -1
        Console.WriteLine(s?.ToUpper() ?? "default");// OUTPUT: default
        s ??= "assigned";                            // assigns only if null
        Console.WriteLine(s);                        // OUTPUT: assigned
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 06 — yield deferred execution
    // ──────────────────────────────────────────────────────
    /*
    IEnumerable<int> Gen() {
        Console.WriteLine("start");
        yield return 1;
        Console.WriteLine("middle");
        yield return 2;
        Console.WriteLine("end");
    }
    var seq = Gen();      // does this print anything?
    Console.WriteLine("before foreach");
    foreach (var x in seq) Console.WriteLine(x);
    */
    static IEnumerable<int> Gen_S06()
    {
        Console.WriteLine("  start");
        yield return 1;
        Console.WriteLine("  middle");
        yield return 2;
        Console.WriteLine("  end");
    }
    static void Snippet06_YieldDeferred()
    {
        Console.WriteLine("=== Snippet 06 — yield deferred ===");
        var seq = Gen_S06();                      // prints NOTHING — deferred!
        Console.WriteLine("  before foreach");
        foreach (var x in seq) Console.WriteLine($"  got: {x}");
        // OUTPUT:
        //   before foreach   ← Gen() body not entered yet
        //   start
        //   got: 1
        //   middle
        //   got: 2
        //   end
        // EXPLANATION: yield creates a state machine. Body runs lazily, interleaved with consumer.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 07 — static constructor order
    // ──────────────────────────────────────────────────────
    /*
    class Foo {
        public static int Value = Init();
        static Foo() { Console.WriteLine("static ctor"); }
        static int Init() { Console.WriteLine("Init()"); return 42; }
    }
    Console.WriteLine(Foo.Value);
    Console.WriteLine(Foo.Value);
    */
    class Foo_S07
    {
        public static int Value = Init();
        static Foo_S07() { Console.WriteLine("  static ctor"); }
        static int Init() { Console.WriteLine("  Init()"); return 42; }
    }
    static void Snippet08_StaticConstructor()
    {
        Console.WriteLine("=== Snippet 08 — static constructor order ===");
        Console.WriteLine("  " + Foo_S07.Value);
        Console.WriteLine("  " + Foo_S07.Value);
        // OUTPUT:
        //   Init()        ← field initialiser runs BEFORE static constructor
        //   static ctor
        //   42
        //   42            ← second access: static ctor NOT called again (runs exactly once)
        // EXPLANATION: Field initialisers run before the static constructor body.
        // Static constructor runs exactly once, before the first access to any static member.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 08 — Default interface method
    // ──────────────────────────────────────────────────────
    /*
    interface IGreeter { string Greet() => "Hello from interface"; }
    class MyGreeter : IGreeter { }     // no override

    IGreeter g = new MyGreeter();
    MyGreeter m = new MyGreeter();
    Console.WriteLine(g.Greet());    // ?
    Console.WriteLine(m.Greet());    // ?   COMPILER ERROR!
    */
    interface IGreeter_S09 { string Greet() => "Hello from interface"; }
    class MyGreeter_S09 : IGreeter_S09 { }

    static void Snippet09_InterfaceDefault()
    {
        Console.WriteLine("=== Snippet 09 — Default interface method ===");
        IGreeter_S09 g = new MyGreeter_S09();
        Console.WriteLine(g.Greet());  // OUTPUT: Hello from interface — accessible via interface ref

        // MyGreeter_S09 m = new MyGreeter_S09();
        // Console.WriteLine(m.Greet()); // COMPILER ERROR — default method not in MyGreeter's API
        //                                // must cast to interface to access default method

        // EXPLANATION: Default interface methods are only accessible via the interface reference.
        // The concrete class does NOT inherit the default implementation into its own namespace.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 09 — Record equality
    // ──────────────────────────────────────────────────────
    /*
    record Point(int X, int Y);
    var p1 = new Point(1, 2);
    var p2 = new Point(1, 2);
    var p3 = p1;
    Console.WriteLine(p1 == p2);
    Console.WriteLine(ReferenceEquals(p1, p2));
    Console.WriteLine(ReferenceEquals(p1, p3));
    */
    record Point_S10(int X, int Y);
    static void Snippet10_RecordEquality()
    {
        Console.WriteLine("=== Snippet 10 — Record equality ===");
        var p1 = new Point_S10(1, 2);
        var p2 = new Point_S10(1, 2);
        var p3 = p1;
        Console.WriteLine(p1 == p2);                  // OUTPUT: True  — value equality
        Console.WriteLine(ReferenceEquals(p1, p2));   // OUTPUT: False — different objects
        Console.WriteLine(ReferenceEquals(p1, p3));   // OUTPUT: True  — same reference
        // EXPLANATION: Records override == to compare by value. p1 and p2 are different
        // heap objects but contain equal values so == is true. p3 is just another name for p1.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 10 — Boxing equality
    // ──────────────────────────────────────────────────────
    /*
    int a = 5;
    object b = a;   // boxing
    object c = a;   // boxing again
    Console.WriteLine(b == c);
    Console.WriteLine(b.Equals(c));
    */
    static void Snippet11_BoxingEquality()
    {
        Console.WriteLine("=== Snippet 11 — Boxing equality ===");
        int a = 5;
        object b = a;
        object c = a;
        Console.WriteLine(b == c);         // OUTPUT: False! — object == is reference equality; two separate boxes
        Console.WriteLine(b.Equals(c));    // OUTPUT: True  — Equals() unboxes and compares values
        // EXPLANATION: Boxing creates a new heap object each time. b and c are different objects.
        // == on object compares references. Equals() is overridden by the boxed Int32 to compare values.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 11 — Exception + finally
    // ──────────────────────────────────────────────────────
    /*
    static int Risky() {
        try { throw new Exception("!"); return 1; }
        catch { return 2; }
        finally { Console.WriteLine("finally"); }
    }
    Console.WriteLine(Risky());
    */
    static int Risky_S12()
    {
        try { throw new Exception("!"); return 1; }
        catch { return 2; }
        finally { Console.WriteLine("  finally"); }  // always runs
    }
    static void Snippet12_ExceptionFinally()
    {
        Console.WriteLine("=== Snippet 12 — Exception + finally ===");
        Console.WriteLine("  " + Risky_S12());
        // OUTPUT:
        //   finally    ← runs BEFORE the method actually returns
        //   2          ← catch returned 2; finally can't change return value
        // EXPLANATION: finally always executes. The return value 2 is already decided
        // in the catch block; finally runs but cannot change what's returned.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 12 — Multicast delegate return value
    // ──────────────────────────────────────────────────────
    /*
    Func<int> f = () => 1;
    f += () => 2;
    f += () => 3;
    Console.WriteLine(f());
    */
    static void Snippet13_DelegateReturn()
    {
        Console.WriteLine("=== Snippet 13 — Multicast delegate return ===");
        Func<int> f = () => 1;
        f += () => 2;
        f += () => 3;
        Console.WriteLine(f());  // OUTPUT: 3 — ALL three run, but only LAST return value kept
        // EXPLANATION: All delegates in the invocation list are called in order.
        // Only the return value of the LAST delegate is returned to the caller.
        // To collect all: f.GetInvocationList().Select(d => ((Func<int>)d)()).ToList()
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 13 — LINQ deferred + source mutation
    // ──────────────────────────────────────────────────────
    /*
    var list = new List<int> { 1, 2, 3 };
    var query = list.Where(x => x > 1);
    list.Add(4);
    Console.WriteLine(query.Count());
    */
    static void Snippet14_LinqDeferred()
    {
        Console.WriteLine("=== Snippet 14 — LINQ deferred + source mutation ===");
        var list = new List<int> { 1, 2, 3 };
        var query = list.Where(x => x > 1);   // NOT executed yet
        list.Add(4);                           // mutate source BEFORE iteration
        Console.WriteLine(query.Count());      // OUTPUT: 3  (2, 3, 4 — all > 1)
        // EXPLANATION: LINQ Where is deferred. list.Add(4) happens before Count() iterates.
        // So the query sees {1,2,3,4} and returns elements > 1 = {2,3,4} → count = 3.
        // If ToList() had been called first: query would be fixed at {2,3} → count = 2.
        Console.WriteLine();
    }

    // ──────────────────────────────────────────────────────
    // SNIPPET 14 — checked overflow
    // ──────────────────────────────────────────────────────
    /*
    unchecked {
        int max = int.MaxValue;
        Console.WriteLine(max + 1);
    }
    checked {
        try { int max = int.MaxValue; int _ = max + 1; }
        catch (OverflowException) { Console.WriteLine("overflow"); }
    }
    */
    static void Snippet15_OverflowWrap()
    {
        Console.WriteLine("=== Snippet 15 — checked overflow ===");
        unchecked
        {
            int max = int.MaxValue;
            Console.WriteLine(max + 1);   // OUTPUT: -2147483648  — wraps silently
        }
        checked
        {
            try { int max = int.MaxValue; int _ = max + 1; }
            catch (OverflowException) { Console.WriteLine("overflow"); } // OUTPUT: overflow
        }
        // EXPLANATION: Default C# arithmetic is unchecked — wraps on overflow.
        // checked block/expression throws OverflowException instead.
        Console.WriteLine();
    }

    // Not included inline above but exercises async void:
    static void Snippet07_AsyncVoid()
    {
        Console.WriteLine("=== Snippet 07 — async void ===");
        Console.WriteLine("  async void fires exception to SynchronizationContext — unobservable.");
        Console.WriteLine("  Use async Task instead. Only acceptable in: event handlers.");
        Console.WriteLine();
    }
}

// ---------- Demo ----------
public static class InterviewQandADemo
{
    public static void Run()
    {
        Console.WriteLine("=== Interview Output Snippets ===\n");
        OutputSnippets.RunAll();
    }
}

// ============================================================
// BONUS — COMMON TRAPS SUMMARY
// ============================================================
/*
  TRAP 1: foreach over IEnumerable twice hits the source twice
          (bad if source is a DB query or network stream)
          FIX: .ToList() to materialise once.

  TRAP 2: Closing over a loop variable in a lambda
          FIX: int copy = i; lambda captures copy.

  TRAP 3: == on boxed value types returns false
          FIX: Use .Equals() or cast to the concrete type first.

  TRAP 4: async void — exceptions are lost
          FIX: async Task everywhere except event handlers.

  TRAP 5: .Result or .Wait() on a Task in a SynchronizationContext — deadlock
          FIX: ConfigureAwait(false) or go fully async.

  TRAP 6: Singleton with lazy init in a multi-threaded app without thread safety
          FIX: Lazy<T> with LazyThreadSafetyMode.ExecutionAndPublication.

  TRAP 7: new HttpClient() per request — socket exhaustion
          FIX: Use IHttpClientFactory / typed client.

  TRAP 8: LINQ .Where() on IQueryable after .AsEnumerable() = loads ALL rows
          FIX: Keep filters on IQueryable before calling ToList().

  TRAP 9: string + in a loop = O(n²) allocations
          FIX: StringBuilder or string.Join / string.Concat.

  TRAP 10: Not disposing SemaphoreSlim, DbContext, HttpClient, Stream, etc.
           FIX: always `using var` or wrap in try/finally.
*/
