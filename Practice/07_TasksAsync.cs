// ============================================================
// TOPIC: Tasks, async/await, Cancellation, Parallel
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What is the difference between Task and Thread?
//      Thread: OS-level resource, expensive. Task: logical unit of work, scheduled
//      on ThreadPool — much cheaper, and composable.
//   Q: What does async/await actually compile to?
//      State machine — method is split at each await point; continuation queued on
//      the scheduler when the awaited operation completes.
//   Q: What is ConfigureAwait(false) and when should you use it?
//      Avoids capturing SynchronizationContext — prevents deadlocks in library code.
//      DO use in library/service code. DON'T need it in ASP.NET Core (no sync context).
//   Q: Difference between Task.WhenAll and Task.WhenAny?
//      WhenAll: waits for ALL tasks, aggregates exceptions.
//      WhenAny: returns as soon as the first completes.
//   Q: What is async void and when should you use it?
//      ONLY for event handlers. Exceptions are unobservable — avoid everywhere else.
//   Q: What is ValueTask and when does it give a performance advantage?
//      Avoids heap allocation when the result is synchronously available (common path).
// ============================================================

namespace Practice.TasksAsync;

// ============================================================
// 1. Task vs Thread — basic comparison
// ============================================================

public static class TaskVsThread
{
    public static void Demonstrate()
    {
        Console.WriteLine("--- Task vs Thread ---");

        // Thread — raw, not pooled, 1 MB stack per thread
        var thread = new Thread(() =>
        {
            Console.WriteLine($"[Thread] Running on thread {Environment.CurrentManagedThreadId}");
            Thread.Sleep(50);
        });
        thread.Start();
        thread.Join(); // block caller until done

        // Task — pooled, composable, returns value, supports await
        var task = Task.Run(() =>
        {
            Console.WriteLine($"[Task] Running on thread {Environment.CurrentManagedThreadId}");
            Thread.Sleep(50);
            return 42;
        });
        int result = task.Result; // .Result blocks — fine here, bad in async context (deadlock risk)
        Console.WriteLine($"[Task] Result: {result}");
    }
}

// ============================================================
// 2. async/await fundamentals
// ============================================================

public class OrderService
{
    // async method: returns Task<T>, can be awaited
    public async Task<string> GetOrderStatusAsync(int orderId, CancellationToken ct = default)
    {
        Console.WriteLine($"[Async] Fetching order {orderId} on thread {Environment.CurrentManagedThreadId}");

        // await releases the thread back to pool while waiting
        await Task.Delay(100, ct); // simulates DB or HTTP call

        Console.WriteLine($"[Async] Resumed on thread {Environment.CurrentManagedThreadId}");
        return $"Order-{orderId}: Shipped";
    }

    // async void — ONLY acceptable for event handlers
    // Exceptions are lost; caller cannot await it
    // public async void ButtonClick(object sender, EventArgs e) { await DoSomethingAsync(); }

    // ConfigureAwait(false) — use in library code to avoid context capture
    public async Task<decimal> GetPriceAsync(int productId)
    {
        await Task.Delay(50).ConfigureAwait(false); // no sync context resume needed
        return productId * 9.99m;
    }
}

// ============================================================
// 3. Task.WhenAll — run tasks concurrently
// ============================================================

public static class WhenAllExample
{
    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- Task.WhenAll ---");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Sequential: total ~300ms
        // await FetchUserAsync(1);    // 100ms
        // await FetchOrderAsync(1);   // 100ms
        // await FetchProductAsync(1); // 100ms

        // Concurrent: total ~100ms (overlap)
        var userTask    = FetchUserAsync(1);
        var orderTask   = FetchOrderAsync(1);
        var productTask = FetchProductAsync(1);

        await Task.WhenAll(userTask, orderTask, productTask);

        sw.Stop();
        Console.WriteLine($"All done in {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"User: {userTask.Result}, Order: {orderTask.Result}, Product: {productTask.Result}");

        // WhenAll with exception handling — AggregateException
        Console.WriteLine("\n--- WhenAll error handling ---");
        try
        {
            await Task.WhenAll(
                Task.FromResult(1),
                Task.FromException<int>(new InvalidOperationException("DB down")),
                Task.FromException<int>(new TimeoutException("Timeout"))
            );
        }
        catch (Exception ex)
        {
            // Only first exception is rethrown here
            Console.WriteLine($"Caught: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<string> FetchUserAsync(int id) { await Task.Delay(100); return $"User-{id}"; }
    private static async Task<string> FetchOrderAsync(int id) { await Task.Delay(100); return $"Order-{id}"; }
    private static async Task<string> FetchProductAsync(int id) { await Task.Delay(100); return $"Product-{id}"; }
}

// ============================================================
// 4. Task.WhenAny — first one wins
// ============================================================

public static class WhenAnyExample
{
    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- Task.WhenAny (timeout pattern) ---");

        var fetchTask   = SlowFetchAsync();
        var timeoutTask = Task.Delay(150); // 150ms timeout

        var first = await Task.WhenAny(fetchTask, timeoutTask);

        if (first == fetchTask)
            Console.WriteLine($"Fetch succeeded: {await fetchTask}");
        else
            Console.WriteLine("Timed out! fetchTask still running in background.");
    }

    private static async Task<string> SlowFetchAsync()
    {
        await Task.Delay(300); // slower than timeout
        return "data";
    }
}

// ============================================================
// 5. CancellationToken — cooperative cancellation
// ============================================================

public static class CancellationExample
{
    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- CancellationToken ---");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)); // auto-cancel after 200ms

        try
        {
            await LongRunningOperationAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[Cancel] Operation was cancelled at {DateTime.UtcNow:u}");
        }

        // Manual cancellation
        using var manualCts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                manualCts.Token.ThrowIfCancellationRequested(); // check and throw
                Console.WriteLine($"  Iteration {i}");
                await Task.Delay(50, manualCts.Token);
            }
        }, manualCts.Token);

        await Task.Delay(120); // let it run 2 iterations
        manualCts.Cancel();
        try { await task; }
        catch (OperationCanceledException) { Console.WriteLine("[Cancel] Manual cancel fired."); }
    }

    private static async Task LongRunningOperationAsync(CancellationToken ct)
    {
        for (int i = 0; i < 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($"  [LongOp] step {i}");
            await Task.Delay(50, ct); // Task.Delay respects cancellation
        }
    }
}

// ============================================================
// 6. ValueTask — avoids heap allocation on hot paths
// ============================================================

public class CachingService
{
    private readonly Dictionary<int, string> _cache = new();

    // ValueTask: if data is cached, returns synchronously — NO Task allocation
    // If not cached, goes async — allocates Task as usual
    public ValueTask<string> GetAsync(int id)
    {
        if (_cache.TryGetValue(id, out var cached))
            return ValueTask.FromResult(cached); // synchronous — no alloc

        return new ValueTask<string>(FetchAndCacheAsync(id)); // async path
    }

    private async Task<string> FetchAndCacheAsync(int id)
    {
        await Task.Delay(50);
        var value = $"data-{id}";
        _cache[id] = value;
        return value;
    }
}

// ============================================================
// 7. Parallel.ForEachAsync — CPU-bound parallel work
// ============================================================

public static class ParallelExample
{
    public static async Task Demonstrate()
    {
        Console.WriteLine("\n--- Parallel.ForEachAsync ---");

        var items = Enumerable.Range(1, 8).ToList();

        // Limit concurrency to avoid overwhelming resources
        await Parallel.ForEachAsync(items,
            new ParallelOptions { MaxDegreeOfParallelism = 3 },
            async (item, ct) =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine($"  Processed item {item} on thread {Environment.CurrentManagedThreadId}");
            });

        Console.WriteLine("All parallel items processed.");
    }
}

// ---------- Demo ----------
public static class TasksAsyncDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Tasks & Async Demo ===\n");

        TaskVsThread.Demonstrate();

        var svc = new OrderService();
        var status = await svc.GetOrderStatusAsync(99);
        Console.WriteLine($"\n{status}");

        await WhenAllExample.Demonstrate();
        await WhenAnyExample.Demonstrate();
        await CancellationExample.Demonstrate();

        Console.WriteLine("\n--- ValueTask ---");
        var cache = new CachingService();
        var r1 = await cache.GetAsync(1); // async path
        var r2 = await cache.GetAsync(1); // sync path (cached)
        Console.WriteLine($"r1={r1}, r2={r2}");

        await ParallelExample.Demonstrate();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is a deadlock in async code and how does it happen?
//      .Result or .Wait() on a task inside a method that has a SynchronizationContext
//      (e.g., ASP.NET classic, WPF) blocks the context thread, which the continuation
//      needs to resume — infinite wait. Fix: ConfigureAwait(false) or go async all the way.
//   2. What does Task.Run do differently from async/await?
//      Task.Run explicitly queues work to a ThreadPool thread. Useful for CPU-bound work.
//      async/await is for I/O-bound work — doesn't need an extra thread while waiting.
//   3. What is the difference between Task.WhenAll and Parallel.ForEach?
//      WhenAll: async concurrent I/O tasks. Parallel.ForEach: CPU-bound parallel.
//   4. What is a CancellationTokenSource.CreateLinkedTokenSource?
//      Links multiple tokens — fires if ANY of the linked tokens is cancelled.
//   5. Can you await the same Task multiple times?
//      YES — awaiting a completed Task is always safe and returns cached result.
//   6. What is Lazy<Task<T>>? When is it useful?
//      Ensures an async operation runs at most once — lazy initialized shared task.
// ============================================================
