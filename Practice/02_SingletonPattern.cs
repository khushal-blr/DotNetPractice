// ============================================================
// TOPIC: Singleton Pattern
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What problem does Singleton solve?
//      Ensure only ONE instance of a class exists (e.g. config, connection pool, logger).
//   Q: How do you make Singleton thread-safe?
//      Options: lock, double-checked locking + volatile, Lazy<T>, static field initializer.
//   Q: What is volatile and why is it needed in double-checked locking?
//      Without volatile the CPU/JIT can reorder writes — another thread may read
//      a partially constructed object before _instance is fully written.
//   Q: Drawbacks of Singleton?
//      Hidden global state, hard to unit test (tight coupling), violates DIP.
//   Q: How do you test code that uses Singleton?
//      Extract interface, inject via DI — singleton lifetime is managed by container.
// ============================================================

namespace Practice.SingletonPattern;

// ---------- 1. NAIVE — NOT thread-safe (do NOT use) ----------
// Two threads can both see _instance == null and create two instances.
public class AuditLoggerNaive
{
    private static AuditLoggerNaive? _instance;
    private AuditLoggerNaive() { }

    public static AuditLoggerNaive Instance
    {
        get
        {
            if (_instance == null)            // Race condition here!
                _instance = new AuditLoggerNaive();
            return _instance;
        }
    }
}

// ---------- 2. Lock on every call (safe but slower) ----------
public class AuditLoggerLocked
{
    private static AuditLoggerLocked? _instance;
    private static readonly object _lock = new();
    private AuditLoggerLocked() { }

    public static AuditLoggerLocked Instance
    {
        get
        {
            lock (_lock)                     // Every call acquires lock — expensive
            {
                _instance ??= new AuditLoggerLocked();
                return _instance;
            }
        }
    }
}

// ---------- 3. Double-Checked Locking (fast + safe) ----------
// FOLLOW-UP Q: Why two null checks? First avoids lock overhead once initialized.
//              Second prevents race inside the lock.
public class AuditLoggerDoubleChecked
{
    private static volatile AuditLoggerDoubleChecked? _instance;  // volatile!
    private static readonly object _lock = new();
    private AuditLoggerDoubleChecked() { }

    public static AuditLoggerDoubleChecked Instance
    {
        get
        {
            if (_instance == null)           // Fast path — no lock after first init
            {
                lock (_lock)
                {
                    if (_instance == null)   // Safe path — inside lock
                        _instance = new AuditLoggerDoubleChecked();
                }
            }
            return _instance;
        }
    }
}

// ---------- 4. Lazy<T> — RECOMMENDED in most codebases ----------
// LazyThreadSafetyMode.ExecutionAndPublication: only one thread initializes,
// all others wait. Most conservative and correct option.
public class AuditLoggerLazy
{
    private static readonly Lazy<AuditLoggerLazy> _lazy =
        new(() => new AuditLoggerLazy(), LazyThreadSafetyMode.ExecutionAndPublication);

    private int _eventCount;
    private AuditLoggerLazy() => Console.WriteLine("[AuditLoggerLazy] Created on thread " + Environment.CurrentManagedThreadId);

    public static AuditLoggerLazy Instance => _lazy.Value;

    public void LogEvent(string source)
    {
        // Interlocked for safe counter increment across threads
        int count = Interlocked.Increment(ref _eventCount);
        Console.WriteLine($"[AuditLog] event#{count} from '{source}' on thread {Environment.CurrentManagedThreadId}");
    }

    public int EventCount => _eventCount;
}

// ---------- 5. Static field initializer (simplest, eager) ----------
// CLR guarantees type initializer runs exactly once, before first use.
// Drawback: instance created at class load time, not lazily.
public class AuditLoggerStatic
{
    private static readonly AuditLoggerStatic _instance = new();

    // Static constructor prevents beforefieldinit optimization,
    // ensuring init happens at first access — not before.
    static AuditLoggerStatic() { }

    private AuditLoggerStatic() { }

    public static AuditLoggerStatic Instance => _instance;

    public void WriteEntry(string message) => Console.WriteLine($"[AuditLoggerStatic] {message}");
}

// ---------- 6. Singleton via ASP.NET Core DI (production pattern) ----------
// In real apps use AddSingleton — avoids the class itself managing lifetime.
// public interface IAuditLogger { void LogEvent(string source); }
// public class AuditLogger : IAuditLogger { ... }
// builder.Services.AddSingleton<IAuditLogger, AuditLogger>();
// — injected via constructor, easy to swap in tests with a mock.

// ---------- Demo ----------
public static class SingletonDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Singleton Demo ===\n");

        // Prove same instance
        var a = AuditLoggerLazy.Instance;
        var b = AuditLoggerLazy.Instance;
        Console.WriteLine($"a == b (same instance): {ReferenceEquals(a, b)}\n");

        // Multithreaded access — all threads get the same instance
        var threads = new Thread[5];
        for (int i = 0; i < 5; i++)
        {
            string source = $"Service-{i}";
            threads[i] = new Thread(() => AuditLoggerLazy.Instance.LogEvent(source));
            threads[i].Start();
        }
        foreach (var t in threads) t.Join();

        Console.WriteLine($"\nTotal events logged: {AuditLoggerLazy.Instance.EventCount}");

        AuditLoggerStatic.Instance.WriteEntry("Application shutdown initiated.");
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. Why is Singleton considered an anti-pattern by some?
//      Global mutable state is hard to reason about and test.
//   2. How does AddSingleton differ from a static Singleton?
//      DI container manages lifetime; easy to swap implementation in tests.
//   3. What is the Monostate pattern?
//      All fields are static but instances can be freely created — same shared state.
//   4. Can you have a Singleton per-thread? YES — use [ThreadStatic] or ThreadLocal<T>.
//   5. What is LazyThreadSafetyMode.None vs PublicationOnly vs ExecutionAndPublication?
//      None: no safety. PublicationOnly: multiple may construct, first published wins.
//      ExecutionAndPublication: only one constructs, others wait.
// ============================================================
