// ============================================================
// TOPIC: Delegates, Func, Action, Predicate, Events
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What is a delegate?
//      A type-safe function pointer — variable that holds a reference to a method.
//   Q: Difference between delegate, Func, Action, Predicate?
//      delegate: custom named type. Func<>: returns value. Action<>: returns void.
//      Predicate<T>: equivalent to Func<T,bool>.
//   Q: What is a multicast delegate?
//      A delegate variable that holds multiple method references — all invoked in order.
//   Q: What is an event vs a delegate?
//      event restricts access: outside callers can only += / -= (not invoke directly).
//   Q: What are closures and what pitfalls do they have?
//      Lambda captures variables by reference. Classic loop capture bug.
//   Q: What is the difference between Func and Expression<Func>?
//      Func: compiled code. Expression<Func>: expression tree — inspectable, translatable to SQL.
// ============================================================

namespace Practice.DelegatesActions;

// ============================================================
// 1. Custom delegate declaration and usage
// ============================================================

// Delegate type declaration — like defining a method signature as a type
public delegate double MathOperation(double a, double b);
public delegate bool Validator<T>(T value);

public static class DelegateBasics
{
    public static void Demonstrate()
    {
        Console.WriteLine("--- Custom Delegate ---");

        // Assign a method to a delegate variable
        MathOperation add = Add;
        MathOperation multiply = Multiply;

        Console.WriteLine($"Add: {add(3, 4)}");           // 7
        Console.WriteLine($"Multiply: {multiply(3, 4)}"); // 12

        // Delegate as parameter — passing behaviour
        Console.WriteLine($"Apply add: {ApplyOperation(10, 5, add)}");
        Console.WriteLine($"Apply mul: {ApplyOperation(10, 5, multiply)}");

        // Anonymous method (old C# style, rarely used now)
        MathOperation power = delegate(double a, double b) { return Math.Pow(a, b); };
        Console.WriteLine($"Power: {power(2, 8)}"); // 256

        // Lambda (preferred modern syntax)
        MathOperation subtract = (a, b) => a - b;
        Console.WriteLine($"Subtract: {subtract(10, 3)}"); // 7
    }

    private static double Add(double a, double b) => a + b;
    private static double Multiply(double a, double b) => a * b;
    private static double ApplyOperation(double a, double b, MathOperation op) => op(a, b);
}

// ============================================================
// 2. Func<>, Action<>, Predicate<T>
// ============================================================

public static class FuncActionPredicate
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Func / Action / Predicate ---");

        // Func<TIn, TOut> — takes input, returns output
        Func<int, int, int> max = (a, b) => a > b ? a : b;
        Console.WriteLine($"Max(5,9): {max(5, 9)}");

        // Func<string, bool> — same as Predicate<string>
        Func<string, bool> isLong = s => s.Length > 10;

        // Action<T> — returns void
        Action<string> log = msg => Console.WriteLine($"[LOG] {msg}");
        log("Action demo");

        // Action with multiple params
        Action<string, int> repeat = (s, n) => { for (int i = 0; i < n; i++) Console.Write(s + " "); Console.WriteLine(); };
        repeat("hello", 3);

        // Predicate<T> — specialized Func<T, bool>
        Predicate<int> isEven = n => n % 2 == 0;
        var numbers = new List<int> { 1, 2, 3, 4, 5, 6 };
        var evens = numbers.FindAll(isEven); // List.FindAll takes Predicate<T>
        Console.Write("Evens: "); evens.ForEach(n => Console.Write(n + " ")); Console.WriteLine();

        // Composing functions
        Func<int, int> doubleIt = x => x * 2;
        Func<int, int> addTen   = x => x + 10;
        Func<int, int> composed = x => addTen(doubleIt(x)); // manual composition
        Console.WriteLine($"Composed(5): {composed(5)}"); // (5*2)+10 = 20
    }
}

// ============================================================
// 3. Multicast Delegates
// ============================================================

public static class MulticastDelegates
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Multicast Delegate ---");

        Action<string> pipeline = Step1;
        pipeline += Step2;   // add to invocation list
        pipeline += Step3;

        pipeline("order-123"); // calls all three in order

        pipeline -= Step2;     // remove from invocation list
        Console.WriteLine("After removing Step2:");
        pipeline("order-456");

        // FOLLOW-UP Q: What if a delegate in the chain throws?
        // Remaining delegates are NOT called. Fix: invoke manually via GetInvocationList().
        Console.WriteLine("\n--- Safe multicast with GetInvocationList ---");
        Action<string> safeChain = SafeStep1;
        safeChain += ThrowingStep;
        safeChain += SafeStep2;

        foreach (Action<string> handler in safeChain.GetInvocationList())
        {
            try { handler("data"); }
            catch (Exception ex) { Console.WriteLine($"[Error in handler] {ex.Message}"); }
        }
    }

    private static void Step1(string x) => Console.WriteLine($"  Step1 processing {x}");
    private static void Step2(string x) => Console.WriteLine($"  Step2 processing {x}");
    private static void Step3(string x) => Console.WriteLine($"  Step3 processing {x}");
    private static void SafeStep1(string x) => Console.WriteLine($"  SafeStep1: {x}");
    private static void ThrowingStep(string x) => throw new InvalidOperationException("Step failed");
    private static void SafeStep2(string x) => Console.WriteLine($"  SafeStep2: {x}");
}

// ============================================================
// 4. Events — delegate + encapsulation
// ============================================================

public class EventArgs<T> : System.EventArgs
{
    public T Data { get; }
    public EventArgs(T data) => Data = data;
}

public class OrderProcessor
{
    // event keyword: outside callers can only += / -= , NOT invoke or assign
    public event EventHandler<EventArgs<Order>>? OrderPlaced;
    public event EventHandler<EventArgs<string>>? OrderFailed;

    public void ProcessOrder(Order order)
    {
        Console.WriteLine($"[OrderProcessor] Processing order {order.Id}...");

        if (order.Total <= 0)
        {
            // Raise failure event (null-conditional for thread safety)
            OrderFailed?.Invoke(this, new EventArgs<string>("Invalid total"));
            return;
        }

        // Raise success event
        OrderPlaced?.Invoke(this, new EventArgs<Order>(order));
    }
}

public class EmailNotifier
{
    public void OnOrderPlaced(object? sender, EventArgs<Order> e) =>
        Console.WriteLine($"[EmailNotifier] Email sent for order {e.Data.Id}");
}

public class AuditLogger
{
    public void OnOrderPlaced(object? sender, EventArgs<Order> e) =>
        Console.WriteLine($"[AuditLogger] Logged order {e.Data.Id} at {DateTime.UtcNow:u}");

    public void OnOrderFailed(object? sender, EventArgs<string> e) =>
        Console.WriteLine($"[AuditLogger] FAILURE: {e.Data}");
}

// ============================================================
// 5. Closure pitfall — loop variable capture
// ============================================================

public static class ClosurePitfall
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Closure Pitfall ---");

        var actions = new List<Action>();

        // BUG: all lambdas capture the SAME variable i
        for (int i = 0; i < 3; i++)
            actions.Add(() => Console.Write(i + " ")); // prints 3 3 3

        Console.Write("BAD (all capture i=3): ");
        actions.ForEach(a => a());
        Console.WriteLine();

        // FIX: capture a copy per iteration
        actions.Clear();
        for (int i = 0; i < 3; i++)
        {
            int captured = i; // new variable each iteration
            actions.Add(() => Console.Write(captured + " ")); // prints 0 1 2
        }

        Console.Write("GOOD (captured copy):  ");
        actions.ForEach(a => a());
        Console.WriteLine();
    }
}

// ============================================================
// 6. Expression<Func> vs Func — why it matters for EF Core
// ============================================================

public static class ExpressionVsFunc
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Expression<Func> vs Func ---");

        // Func: compiled IL — can execute but can't inspect
        Func<int, bool> funcFilter = x => x > 5;

        // Expression<Func>: expression tree — can inspect AND translate to SQL
        System.Linq.Expressions.Expression<Func<int, bool>> exprFilter = x => x > 5;

        // In EF Core:
        // dbContext.Products.Where(funcFilter)   -> loads ALL rows, filters in C#
        // dbContext.Products.Where(exprFilter)   -> generates WHERE Price > 5 in SQL

        // Reading the expression tree
        Console.WriteLine($"Expression body: {exprFilter.Body}");   // (x > 5)
        Console.WriteLine($"Parameter:       {exprFilter.Parameters[0].Name}"); // x

        var compiled = exprFilter.Compile();
        Console.WriteLine($"Compiled(10): {compiled(10)}, Compiled(3): {compiled(3)}");
    }
}

// ============================================================
// Reuse Order from SolidPrinciples namespace — for demo we redeclare minimal version
// ============================================================
public class Order { public int Id { get; init; } = 1; public decimal Total { get; init; } = 100m; }

// ---------- Demo ----------
public static class DelegatesDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Delegates, Func, Action, Events Demo ===\n");

        DelegateBasics.Demonstrate();
        FuncActionPredicate.Demonstrate();
        MulticastDelegates.Demonstrate();

        Console.WriteLine("\n--- Events ---");
        var processor = new OrderProcessor();
        var emailer   = new EmailNotifier();
        var logger    = new AuditLogger();

        processor.OrderPlaced += emailer.OnOrderPlaced;
        processor.OrderPlaced += logger.OnOrderPlaced;
        processor.OrderFailed += logger.OnOrderFailed;

        processor.ProcessOrder(new Order { Id = 1, Total = 99.99m });
        processor.ProcessOrder(new Order { Id = 2, Total = -5m });   // triggers failure

        ClosurePitfall.Demonstrate();
        ExpressionVsFunc.Demonstrate();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. Why is event preferred over raw delegate as a public member?
//      Encapsulation — outsiders can't invoke or replace the invocation list.
//   2. What is the difference between covariance/contravariance in delegates?
//      Func<Animal> can be assigned to Func<Dog> (covariant return, WRONG direction).
//      Actually: Func<out TResult> is covariant in TResult (can return subtype).
//      Action<in T> is contravariant in T (can accept base type).
//   3. What is the observer pattern and how does it relate to events?
//      Events ARE the observer pattern in C# — publisher raises, subscribers handle.
//   4. Why is += on events safe from a threading perspective?
//      Delegates are immutable — += creates a new multicast delegate and replaces atomically.
//   5. What is AsyncEventHandler and why is it tricky?
//      void return means async void — fire-and-forget, exceptions not catchable.
//      Use Func<Task> pattern for async event-like callbacks instead.
// ============================================================
