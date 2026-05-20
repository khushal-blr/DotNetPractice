// ============================================================
// TOPIC: Delegates Deep Dive — internals, patterns, async, covariance
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What is a delegate under the hood?
//      A sealed class that inherits from System.MulticastDelegate (which inherits Delegate).
//      It wraps: _target (object), _method (MethodInfo/IntPtr), invocation list.
//   Q: What is the difference between invoking a delegate via () and .Invoke()?
//      They are identical — () compiles to .Invoke(). .Invoke() is explicit, useful when
//      calling conditionally or from reflection.
//   Q: What is covariance and contravariance in delegates?
//      Covariant return: delegate can point to a method returning a more derived type.
//      Contravariant params: delegate can point to a method accepting a more base type.
//   Q: What is the difference between delegate.Invoke and delegate.BeginInvoke?
//      BeginInvoke was the old APM async pattern (pre-TPL). Do NOT use it in .NET Core+
//      — it's not supported. Use Task instead.
//   Q: What is a "method group" conversion?
//      When you assign a method name (without parens) to a delegate — compiler infers the
//      delegate type. e.g. Func<int,int> f = Math.Abs;
// ============================================================

namespace Practice.DelegatesDeepDive;

// ============================================================
// 1. Delegate internals — what actually happens
// ============================================================

public delegate int Transform(int value);    // sealed class extending MulticastDelegate

public static class DelegateInternals
{
    private static int Double(int x)  => x * 2;
    private static int Square(int x)  => x * x;
    private static int Negate(int x)  => -x;

    public static void Demonstrate()
    {
        Console.WriteLine("--- Delegate internals ---");

        Transform t = Double;

        // Three ways to invoke — all identical in IL
        Console.WriteLine(t(5));          // syntactic sugar
        Console.WriteLine(t.Invoke(5));   // explicit — identical
        Console.WriteLine(((Transform)Delegate.CreateDelegate(typeof(Transform), typeof(DelegateInternals).GetMethod("Square")!))(5));

        // Inspect the delegate
        Console.WriteLine($"Method:   {t.Method.Name}");
        Console.WriteLine($"Target:   {t.Target ?? (object)"<static>"}");
        Console.WriteLine($"Invocation list length: {t.GetInvocationList().Length}");

        // Multicast — combine with +
        Transform chain = Double;
        chain += Square;
        chain += Negate;

        Console.WriteLine($"\nMulticast invocation list:");
        foreach (var d in chain.GetInvocationList())
            Console.WriteLine($"  {d.Method.Name}");

        // Only the LAST return value is captured when you call a multicast delegate
        int result = chain(3); // Double=6, Square=9, Negate=-3 — result is -3
        Console.WriteLine($"Multicast result (only last return): {result}");

        // Collect ALL return values manually
        var allResults = chain.GetInvocationList()
            .Select(d => ((Transform)d)(3))
            .ToList();
        Console.WriteLine($"All results: [{string.Join(", ", allResults)}]");
    }
}

// ============================================================
// 2. Covariance and Contravariance in delegates
//
// Covariance    (out / return type):  Dog method can satisfy Animal delegate
// Contravariance (in / param type):  Animal param method can satisfy Dog delegate
// ============================================================

public class Animal { public string Name => "Animal"; }
public class Dog : Animal { public new string Name => "Dog"; }

public static class DelegateVariance
{
    // Covariant return: delegate returns Animal, but method returns Dog
    public static Dog MakeDog() => new Dog();

    // Contravariant parameter: delegate takes Dog, but method takes Animal
    public static void HandleAnimal(Animal a) =>
        Console.WriteLine($"Handling: {a.GetType().Name}");

    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Delegate covariance/contravariance ---");

        // Covariance: Func<Dog> assigned to Func<Animal> — Dog IS-A Animal
        Func<Animal> f = MakeDog;   // covariant return type
        Animal a = f();
        Console.WriteLine($"Covariant return: {a.GetType().Name}"); // Dog

        // Contravariance: Action<Animal> assigned to Action<Dog> — handler accepts base
        Action<Dog> handler = HandleAnimal;  // contravariant parameter
        handler(new Dog());

        // TRICKY: this does NOT work the other way:
        // Action<Animal> act = (Action<Dog>)(d => {}); // compile error
    }
}

// ============================================================
// 3. Method group conversion + delegate caching gotcha
// ============================================================

public static class MethodGroupConversion
{
    private static int AddTen(int x) => x + 10;

    public static void Demonstrate()
    {
        Console.WriteLine("\n--- Method group conversion ---");

        // Method group: compiler infers delegate type
        Func<int, int> f = AddTen;           // method group
        Func<int, int> g = x => AddTen(x);   // lambda wrapping — equivalent but extra allocation

        Console.WriteLine($"f(5)={f(5)}, g(5)={g(5)}");

        // GOTCHA: method groups on instance methods capture `this`
        var obj = new Counter();
        Func<int> counterFunc = obj.GetCount; // captures obj reference
        obj.Increment();
        Console.WriteLine($"Captured instance counter: {counterFunc()}"); // 1

        // GOTCHA: delegate comparison
        Func<int, int> d1 = AddTen;
        Func<int, int> d2 = AddTen;
        Console.WriteLine($"d1 == d2: {d1 == d2}");  // TRUE for static method groups

        Action<string> l1 = s => Console.WriteLine(s);
        Action<string> l2 = s => Console.WriteLine(s);
        Console.WriteLine($"l1 == l2 (lambdas): {l1 == l2}"); // FALSE — different instances
    }
}

public class Counter
{
    private int _count;
    public void Increment() => _count++;
    public int GetCount() => _count;
}

// ============================================================
// 4. Delegates as strategy / pipeline pattern
// ============================================================

public class RequestContext
{
    public string Path { get; init; } = "";
    public string User { get; init; } = "";
    public Dictionary<string, string> Headers { get; init; } = new();
    public bool IsAuthenticated { get; set; }
    public bool IsAuthorised   { get; set; }
    public string? ResponseBody { get; set; }
}

public static class MiddlewarePipeline
{
    // Delegate chain: each middleware decides whether to pass to next
    public delegate Task RequestDelegate(RequestContext ctx);

    public static RequestDelegate Build(params Func<RequestDelegate, RequestDelegate>[] middlewares)
    {
        // Start with a terminal handler
        RequestDelegate pipeline = ctx =>
        {
            ctx.ResponseBody = $"200 OK — handled {ctx.Path}";
            return Task.CompletedTask;
        };

        // Wrap in reverse order (outermost first)
        foreach (var mw in middlewares.Reverse())
            pipeline = mw(pipeline);

        return pipeline;
    }

    // Middleware: authentication
    public static Func<RequestDelegate, RequestDelegate> Authentication() =>
        next => async ctx =>
        {
            ctx.IsAuthenticated = ctx.Headers.ContainsKey("Authorization");
            if (!ctx.IsAuthenticated)
            {
                ctx.ResponseBody = "401 Unauthorized";
                return; // short-circuit — do NOT call next
            }
            await next(ctx);
        };

    // Middleware: logging
    public static Func<RequestDelegate, RequestDelegate> Logging() =>
        next => async ctx =>
        {
            Console.WriteLine($"  [Log] --> {ctx.Path} user={ctx.User}");
            await next(ctx);
            Console.WriteLine($"  [Log] <-- {ctx.Path} response={ctx.ResponseBody}");
        };

    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- Middleware Pipeline (delegate chain) ---");

        var pipeline = Build(Logging(), Authentication());

        var ctx1 = new RequestContext { Path = "/api/data", User = "alice",
            Headers = new() { ["Authorization"] = "Bearer token123" } };
        await pipeline(ctx1);
        Console.WriteLine($"  Result: {ctx1.ResponseBody}\n");

        var ctx2 = new RequestContext { Path = "/api/secret", User = "anon" };
        await pipeline(ctx2);
        Console.WriteLine($"  Result: {ctx2.ResponseBody}");
    }
}

// ============================================================
// 5. Func as async callback / continuation
// ============================================================

public static class AsyncDelegates
{
    // Passing async callbacks — common in real code (event handlers, retry, timeout)
    public static async Task<T> WithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, int, bool> shouldRetry, // delegate: (exception, attempt) => bool
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation(ct);
            }
            catch (Exception ex) when (attempt < maxAttempts && shouldRetry(ex, attempt))
            {
                Console.WriteLine($"  [Retry] Attempt {attempt} failed: {ex.Message}");
                await Task.Delay(attempt * 100, ct);
            }
        }
        return await operation(ct); // final attempt — let it throw
    }

    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- Async delegates / callbacks ---");

        int callCount = 0;
        string result = await WithRetryAsync<string>(
            async ct => {
                callCount++;
                await Task.Delay(10, ct);
                if (callCount < 3) throw new HttpRequestException("Service unavailable");
                return "Success!";
            },
            shouldRetry: (ex, attempt) => ex is HttpRequestException
        );
        Console.WriteLine($"  Result after {callCount} attempts: {result}");
    }
}

// ============================================================
// 6. Event delegation — Observer pattern cleanly
// ============================================================

public class StockPriceMonitor
{
    // Typed event args
    public record PriceChangedArgs(string Symbol, decimal OldPrice, decimal NewPrice)
    {
        public decimal ChangePercent => OldPrice == 0 ? 0 : (NewPrice - OldPrice) / OldPrice * 100;
    }

    private readonly Dictionary<string, decimal> _prices = new();

    // event: restricts subscribe/unsubscribe; can't be fired from outside
    public event EventHandler<PriceChangedArgs>? PriceChanged;
    public event EventHandler<PriceChangedArgs>? ThresholdBreached;

    public void UpdatePrice(string symbol, decimal newPrice)
    {
        var oldPrice = _prices.GetValueOrDefault(symbol);
        _prices[symbol] = newPrice;

        if (oldPrice == 0) return; // first update, no change to emit

        var args = new PriceChangedArgs(symbol, oldPrice, newPrice);
        PriceChanged?.Invoke(this, args);

        if (Math.Abs(args.ChangePercent) > 5)
            ThresholdBreached?.Invoke(this, args);
    }
}

public class AlertService
{
    // Subscribe using method group
    public void Subscribe(StockPriceMonitor monitor)
    {
        monitor.PriceChanged      += OnPriceChanged;
        monitor.ThresholdBreached += OnThresholdBreached;
    }

    private void OnPriceChanged(object? sender, StockPriceMonitor.PriceChangedArgs e) =>
        Console.WriteLine($"  [Alert] {e.Symbol}: ${e.OldPrice:F2} → ${e.NewPrice:F2} ({e.ChangePercent:+0.##;-0.##}%)");

    private void OnThresholdBreached(object? sender, StockPriceMonitor.PriceChangedArgs e) =>
        Console.WriteLine($"  [ALERT!!!] {e.Symbol} moved {e.ChangePercent:F1}% — THRESHOLD BREACHED");

    public void Unsubscribe(StockPriceMonitor monitor)
    {
        monitor.PriceChanged      -= OnPriceChanged;
        monitor.ThresholdBreached -= OnThresholdBreached;
    }
}

// ---------- Demo ----------
public static class DelegatesDeepDiveDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Delegates Deep Dive Demo ===\n");

        DelegateInternals.Demonstrate();
        DelegateVariance.Demonstrate();
        MethodGroupConversion.Demonstrate();
        await MiddlewarePipeline.Demonstrate();
        await AsyncDelegates.Demonstrate();

        Console.WriteLine("\n--- Event / Observer pattern ---");
        var monitor = new StockPriceMonitor();
        var alerts  = new AlertService();
        alerts.Subscribe(monitor);

        monitor.UpdatePrice("MSFT", 400m);
        monitor.UpdatePrice("MSFT", 410m); // +2.5%
        monitor.UpdatePrice("MSFT", 390m); // -4.9%
        monitor.UpdatePrice("MSFT", 362m); // -7.2% — THRESHOLD

        alerts.Unsubscribe(monitor);
        monitor.UpdatePrice("MSFT", 400m); // no handler — silence
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is the difference between Delegate.Combine and +=?
//      += on a delegate compiles to Delegate.Combine internally.
//   2. Can delegates be used as dictionary keys?
//      Technically yes, but equality for lambdas is by reference — usually meaningless.
//   3. What is `Func<T>` vs `Lazy<T>` for deferred execution?
//      Both defer work. Lazy<T> also caches the result (runs factory once).
//      Func<T> runs every time you call it.
//   4. What is the memory impact of capturing variables in a lambda?
//      Compiler generates a closure class. Each captured variable becomes a field.
//      Avoid capturing in tight loops — creates a new closure instance per iteration.
//   5. Why should async event handlers return void instead of Task?
//      EventHandler signature is void. Returning Task means fire-and-forget — exceptions
//      are unobservable. For proper async events, use Func<Task>-based callbacks instead.
//   6. What is the open vs closed delegate distinction?
//      Open delegate: `this` not bound — pass the target as first argument.
//      Closed delegate: `this` is bound to a specific instance at creation time.
// ============================================================
