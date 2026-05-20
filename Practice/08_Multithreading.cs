// ============================================================
// TOPIC: Multithreading — lock, Monitor, Mutex, SemaphoreSlim,
//         Interlocked, ReaderWriterLockSlim
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What is a race condition?
//      Two threads read-modify-write shared state without synchronisation — result depends on timing.
//   Q: What is a deadlock?
//      Thread A holds Lock1 and waits for Lock2; Thread B holds Lock2 and waits for Lock1 — stuck.
//   Q: What is the difference between lock and Monitor?
//      lock is syntactic sugar for Monitor.Enter/Exit in a try/finally block.
//   Q: When use Mutex vs lock?
//      Mutex: cross-process synchronisation, or when you need a named system-wide lock.
//      lock: single-process only, much faster.
//   Q: When use SemaphoreSlim vs lock?
//      Semaphore allows N concurrent threads (not just 1). Also supports async waiting.
//   Q: What is Interlocked and when is it better than lock?
//      CPU-level atomic operations — no kernel mode, much faster for simple counter/flag ops.
//   Q: What is ReaderWriterLockSlim?
//      Multiple readers allowed simultaneously; writers get exclusive access. Great for
//      read-heavy, occasional-write scenarios (e.g., in-memory cache).
// ============================================================

namespace Practice.Multithreading;

// ============================================================
// 1. Race condition demo + lock fix
// ============================================================

public class BankAccount
{
    private decimal _balance;
    private readonly object _lock = new();

    public BankAccount(decimal initial) => _balance = initial;

    // UNSAFE — race condition: two threads can read the same balance and both add
    public void DepositUnsafe(decimal amount) => _balance += amount;

    // SAFE — lock ensures only one thread modifies at a time
    public void Deposit(decimal amount)
    {
        lock (_lock) // equivalent to Monitor.Enter(_lock); try { ... } finally { Monitor.Exit(_lock); }
        {
            _balance += amount;
        }
    }

    public void Withdraw(decimal amount)
    {
        lock (_lock)
        {
            if (_balance < amount) throw new InvalidOperationException("Insufficient funds");
            _balance -= amount;
        }
    }

    // Thread-safe read — decimal reads are atomic on 64-bit, but for correctness use lock
    public decimal Balance { get { lock (_lock) { return _balance; } } }
}

// ============================================================
// 2. Monitor — gives finer control (TryEnter, Wait, Pulse)
// ============================================================

public class BoundedQueue<T>
{
    private readonly Queue<T> _queue = new();
    private readonly int _maxSize;
    private readonly object _lock = new();

    public BoundedQueue(int maxSize) => _maxSize = maxSize;

    // Producer: blocks if full (waits for consumer to drain)
    public void Enqueue(T item)
    {
        lock (_lock)
        {
            while (_queue.Count >= _maxSize)
            {
                Console.WriteLine($"[Monitor] Queue full — producer waiting");
                Monitor.Wait(_lock); // releases lock and suspends thread
            }
            _queue.Enqueue(item);
            Console.WriteLine($"[Monitor] Enqueued. Size={_queue.Count}");
            Monitor.PulseAll(_lock); // wake all waiting threads
        }
    }

    // Consumer: blocks if empty
    public T Dequeue()
    {
        lock (_lock)
        {
            while (_queue.Count == 0)
            {
                Console.WriteLine($"[Monitor] Queue empty — consumer waiting");
                Monitor.Wait(_lock);
            }
            var item = _queue.Dequeue();
            Console.WriteLine($"[Monitor] Dequeued. Size={_queue.Count}");
            Monitor.PulseAll(_lock);
            return item;
        }
    }

    // Monitor.TryEnter — non-blocking attempt with timeout
    public bool TryPeek(out T? item, int timeoutMs = 100)
    {
        item = default;
        if (!Monitor.TryEnter(_lock, timeoutMs)) return false;
        try
        {
            if (_queue.Count == 0) return false;
            item = _queue.Peek();
            return true;
        }
        finally { Monitor.Exit(_lock); }
    }
}

// ============================================================
// 3. Mutex — cross-process or when named system lock needed
// ============================================================

public static class MutexExample
{
    private const string MutexName = "Global\\MyApp_SingleInstance";

    public static void EnsureSingleInstance()
    {
        // Named mutex — system-wide; prevents two app instances
        var mutex = new Mutex(initiallyOwned: false, MutexName, out bool created);

        if (!created)
        {
            Console.WriteLine("[Mutex] Another instance is already running.");
            return;
        }

        try
        {
            Console.WriteLine("[Mutex] This is the only instance — acquired.");
            // Do exclusive work here
            Thread.Sleep(100);
        }
        finally
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    // Mutex for cross-thread exclusive access to a shared file
    public static void WriteToSharedFile(string data)
    {
        using var mutex = new Mutex(false, "Global\\SharedFileAccess");
        bool acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
        if (!acquired) throw new TimeoutException("Could not acquire file mutex");
        try
        {
            Console.WriteLine($"[Mutex] Writing: {data}");
            Thread.Sleep(50); // simulate file write
        }
        finally { mutex.ReleaseMutex(); }
    }
}

// ============================================================
// 4. SemaphoreSlim — limit concurrent access (e.g., connection pool)
// ============================================================

public class ConnectionPool
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConnections;

    public ConnectionPool(int maxConnections)
    {
        _maxConnections = maxConnections;
        _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
    }

    // async-friendly — doesn't block a thread while waiting for a slot
    public async Task<string> GetConnectionAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[SemaphoreSlim] Thread {Environment.CurrentManagedThreadId} waiting. Available={_semaphore.CurrentCount}");
        await _semaphore.WaitAsync(ct); // non-blocking wait
        Console.WriteLine($"[SemaphoreSlim] Thread {Environment.CurrentManagedThreadId} acquired. Available={_semaphore.CurrentCount}");
        return $"conn-{Guid.NewGuid():N[..8]}";
    }

    public void ReleaseConnection(string connection)
    {
        Console.WriteLine($"[SemaphoreSlim] Releasing {connection}");
        _semaphore.Release();
    }
}

// ============================================================
// 5. Interlocked — lock-free atomic operations
// ============================================================

public class AtomicCounter
{
    private int _count;
    private long _totalRequests;

    // Interlocked.Increment: CPU-level atomic add — no lock needed
    public int Increment()       => Interlocked.Increment(ref _count);
    public int Decrement()       => Interlocked.Decrement(ref _count);
    public int Current           => Interlocked.CompareExchange(ref _count, 0, 0); // atomic read

    public long AddRequests(long n) => Interlocked.Add(ref _totalRequests, n);

    // CompareExchange: only write if current value matches expected
    // Pattern: spin loop for optimistic concurrency (lock-free update)
    public int SafeMultiply(int factor)
    {
        int initial, updated;
        do
        {
            initial = _count;
            updated = initial * factor;
        }
        while (Interlocked.CompareExchange(ref _count, updated, initial) != initial);
        return updated;
    }
}

// ============================================================
// 6. ReaderWriterLockSlim — many readers, exclusive writer
// ============================================================

public class InMemoryCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _store = new();
    private readonly ReaderWriterLockSlim _rwLock = new();

    // Multiple threads can read concurrently
    public bool TryGet(TKey key, out TValue? value)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _store.TryGetValue(key, out value);
        }
        finally { _rwLock.ExitReadLock(); }
    }

    // Only one thread can write; blocks all readers during write
    public void Set(TKey key, TValue value)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _store[key] = value;
            Console.WriteLine($"[RWLock] Cache SET: {key}");
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    // Upgradeable: start as reader, upgrade to writer only if needed
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        _rwLock.EnterUpgradeableReadLock();
        try
        {
            if (_store.TryGetValue(key, out var existing)) return existing;

            _rwLock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_store.TryGetValue(key, out existing)) return existing;
                var value = factory(key);
                _store[key] = value;
                Console.WriteLine($"[RWLock] Cache MISS — added: {key}");
                return value;
            }
            finally { _rwLock.ExitWriteLock(); }
        }
        finally { _rwLock.ExitUpgradeableReadLock(); }
    }

    public int Count { get { _rwLock.EnterReadLock(); try { return _store.Count; } finally { _rwLock.ExitReadLock(); } } }
}

// ============================================================
// 7. Deadlock — how it happens and how to avoid it
// ============================================================

public static class DeadlockDemo
{
    private static readonly object _lockA = new();
    private static readonly object _lockB = new();

    // DEADLOCK: Thread 1 locks A then tries B; Thread 2 locks B then tries A
    public static void ShowDeadlock()
    {
        Console.WriteLine("\n[DEADLOCK PATTERN — DO NOT RUN BOTH SIMULTANEOUSLY]");
        // Thread 1: lock A -> lock B
        // Thread 2: lock B -> lock A
        // = deadlock
    }

    // FIX 1: Always acquire locks in the same order
    public static void SafeLockOrdering()
    {
        lock (_lockA) { lock (_lockB) { /* always A then B */ } }
    }

    // FIX 2: Use Monitor.TryEnter with timeout — fail fast instead of deadlocking
    public static bool TryDoWork(int timeoutMs = 500)
    {
        bool acquiredA = false, acquiredB = false;
        try
        {
            acquiredA = Monitor.TryEnter(_lockA, timeoutMs);
            if (!acquiredA) return false;

            acquiredB = Monitor.TryEnter(_lockB, timeoutMs);
            if (!acquiredB) return false;

            Console.WriteLine("[DeadlockFix] Both locks acquired safely.");
            return true;
        }
        finally
        {
            if (acquiredB) Monitor.Exit(_lockB);
            if (acquiredA) Monitor.Exit(_lockA);
        }
    }
}

// ---------- Demo ----------
public static class MultithreadingDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Multithreading Demo ===\n");

        // 1. BankAccount with lock
        Console.WriteLine("--- lock (BankAccount) ---");
        var account = new BankAccount(1000);
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => account.Deposit(100)))
            .ToArray();
        await Task.WhenAll(tasks);
        Console.WriteLine($"Final balance (expected 2000): {account.Balance}");

        // 2. SemaphoreSlim — limit to 2 concurrent connections
        Console.WriteLine("\n--- SemaphoreSlim (ConnectionPool) ---");
        var pool = new ConnectionPool(maxConnections: 2);
        var connTasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            var conn = await pool.GetConnectionAsync();
            await Task.Delay(80); // hold connection
            pool.ReleaseConnection(conn);
        }).ToArray();
        await Task.WhenAll(connTasks);

        // 3. Interlocked counter
        Console.WriteLine("\n--- Interlocked (AtomicCounter) ---");
        var counter = new AtomicCounter();
        var incTasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() => counter.Increment()));
        await Task.WhenAll(incTasks);
        Console.WriteLine($"Counter (expected 100): {counter.Current}");

        // 4. ReaderWriterLockSlim cache
        Console.WriteLine("\n--- ReaderWriterLockSlim (InMemoryCache) ---");
        var cache = new InMemoryCache<string, int>();
        cache.Set("price:A", 100);
        cache.Set("price:B", 200);
        var reads = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            cache.TryGet("price:A", out var v);
            return v;
        })).ToArray();
        await Task.WhenAll(reads);
        var v2 = cache.GetOrAdd("price:C", k => 300);
        Console.WriteLine($"GetOrAdd price:C = {v2}");

        // 5. Deadlock fix
        Console.WriteLine("\n--- Deadlock prevention ---");
        bool ok = DeadlockDemo.TryDoWork();
        Console.WriteLine($"TryDoWork result: {ok}");

        MutexExample.EnsureSingleInstance();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is the difference between a spinlock and a blocking lock?
//      Spinlock: thread busy-waits (no context switch) — good only for very short critical sections.
//      lock/Monitor: thread suspends (context switch) — better when wait is long.
//   2. What is thread starvation?
//      Low-priority threads never get to run because high-priority threads hog locks.
//   3. What is volatile and when do you need it?
//      Forces reads/writes to happen in memory order (no CPU reordering). Needed for flags
//      shared across threads when you're NOT using lock or Interlocked.
//   4. What is ConcurrentDictionary and when is it better than Dictionary + lock?
//      Fine-grained locking on segments — better throughput for concurrent read/write.
//      Use it instead of manually locking a Dictionary.
//   5. What is ThreadLocal<T>?
//      Each thread has its own copy — no synchronisation needed. Useful for per-thread buffers.
//   6. What is the difference between SemaphoreSlim and Semaphore?
//      SemaphoreSlim: in-process, async-friendly. Semaphore: cross-process (like Mutex).
// ============================================================
