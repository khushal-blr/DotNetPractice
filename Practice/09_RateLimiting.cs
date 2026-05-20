// ============================================================
// TOPIC: Rate Limiting
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What are the common rate limiting algorithms?
//      Fixed Window, Sliding Window, Token Bucket, Leaky Bucket.
//   Q: When would you use each algorithm?
//      Fixed Window: simplest, but allows burst at window boundary (2x rate spike).
//      Sliding Window: smoother, no boundary burst, slightly more memory.
//      Token Bucket: allows controlled burst up to bucket size, then throttles.
//      Leaky Bucket: smooths bursts into constant output rate (queue-based).
//   Q: How does .NET 7+ built-in rate limiting work?
//      Microsoft.AspNetCore.RateLimiting middleware + System.Threading.RateLimiting.
//   Q: Where do you store rate limit counters in production?
//      Redis — shared across multiple app instances. Local memory only works for single node.
//   Q: What HTTP status code do you return when rate limited?
//      429 Too Many Requests with Retry-After header.
// ============================================================

namespace Practice.RateLimiting;

// ============================================================
// 1. Fixed Window Counter
//    Simple: count requests in a fixed time window, reset at window boundary.
//    Problem: client can burst 2x limit at boundary (e.g., 100 at 11:59, 100 at 12:00)
// ============================================================

public class FixedWindowRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private int _count;
    private DateTime _windowStart;
    private readonly object _lock = new();

    public FixedWindowRateLimiter(int requestsPerWindow, TimeSpan window)
    {
        _limit = requestsPerWindow;
        _window = window;
        _windowStart = DateTime.UtcNow;
    }

    // Returns true if request is allowed
    public bool TryAcquire(string clientId)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if (now - _windowStart >= _window)
            {
                _count = 0;
                _windowStart = now;
            }

            if (_count >= _limit)
            {
                var retryAfter = _window - (now - _windowStart);
                Console.WriteLine($"[FixedWindow] {clientId} DENIED. Retry after {retryAfter.TotalSeconds:F1}s");
                return false;
            }

            _count++;
            Console.WriteLine($"[FixedWindow] {clientId} ALLOWED. Count={_count}/{_limit}");
            return true;
        }
    }
}

// ============================================================
// 2. Sliding Window Counter
//    Tracks requests per sub-bucket within a rolling window — no boundary burst.
//    Trade-off: more memory (stores per-bucket counts).
// ============================================================

public class SlidingWindowRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly int _bucketCount;
    private readonly TimeSpan _bucketSize;
    private readonly Queue<(DateTime Timestamp, int Count)> _buckets = new();
    private int _totalCount;
    private readonly object _lock = new();

    public SlidingWindowRateLimiter(int limit, TimeSpan window, int bucketCount = 10)
    {
        _limit = limit;
        _window = window;
        _bucketCount = bucketCount;
        _bucketSize = TimeSpan.FromTicks(window.Ticks / bucketCount);
    }

    public bool TryAcquire(string clientId)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            EvictOldBuckets(now);

            if (_totalCount >= _limit)
            {
                Console.WriteLine($"[SlidingWindow] {clientId} DENIED. Count={_totalCount}/{_limit}");
                return false;
            }

            AddToBucket(now);
            _totalCount++;
            Console.WriteLine($"[SlidingWindow] {clientId} ALLOWED. Count={_totalCount}/{_limit}");
            return true;
        }
    }

    private void EvictOldBuckets(DateTime now)
    {
        var cutoff = now - _window;
        while (_buckets.Count > 0 && _buckets.Peek().Timestamp < cutoff)
        {
            var removed = _buckets.Dequeue();
            _totalCount -= removed.Count;
        }
    }

    private void AddToBucket(DateTime now)
    {
        // Check if last bucket is still current
        if (_buckets.Count > 0)
        {
            var last = _buckets.Last();
            if (now - last.Timestamp < _bucketSize)
            {
                // Update last bucket — can't mutate struct in queue, so rebuild
                var (ts, cnt) = _buckets.Last();
                // Dequeue all, increment last, re-enqueue (simplified — use array for production)
                var items = _buckets.ToArray();
                items[^1] = (ts, cnt + 1);
                _buckets.Clear();
                foreach (var i in items) _buckets.Enqueue(i);
                return;
            }
        }
        _buckets.Enqueue((now, 1));
    }
}

// ============================================================
// 3. Token Bucket
//    Tokens accumulate at a fixed rate up to bucket capacity.
//    Each request consumes a token. Allows short bursts up to bucket size.
//    Most common algorithm for API rate limiting.
// ============================================================

public class TokenBucketRateLimiter
{
    private readonly int _capacity;     // max burst size
    private readonly double _refillRate; // tokens per second
    private double _tokens;
    private DateTime _lastRefill;
    private readonly object _lock = new();

    public TokenBucketRateLimiter(int capacity, double tokensPerSecond)
    {
        _capacity   = capacity;
        _refillRate = tokensPerSecond;
        _tokens     = capacity; // start full
        _lastRefill = DateTime.UtcNow;
    }

    public bool TryAcquire(string clientId, int tokensNeeded = 1)
    {
        lock (_lock)
        {
            Refill();

            if (_tokens < tokensNeeded)
            {
                double waitSeconds = (tokensNeeded - _tokens) / _refillRate;
                Console.WriteLine($"[TokenBucket] {clientId} DENIED. Tokens={_tokens:F1}/{_capacity}. Retry in {waitSeconds:F1}s");
                return false;
            }

            _tokens -= tokensNeeded;
            Console.WriteLine($"[TokenBucket] {clientId} ALLOWED. Tokens remaining={_tokens:F1}/{_capacity}");
            return true;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        double elapsed = (now - _lastRefill).TotalSeconds;
        double newTokens = elapsed * _refillRate;

        _tokens = Math.Min(_capacity, _tokens + newTokens);
        _lastRefill = now;
    }
}

// ============================================================
// 4. Per-client rate limiting (multi-tenant)
//    In production: use Redis for distributed rate limiting.
// ============================================================

public class PerClientRateLimiter
{
    // In production: ConcurrentDictionary + background cleanup task
    private readonly Dictionary<string, TokenBucketRateLimiter> _limiters = new();
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly double _tokensPerSecond;

    public PerClientRateLimiter(int capacity, double tokensPerSecond)
    {
        _capacity = capacity;
        _tokensPerSecond = tokensPerSecond;
    }

    public bool TryAcquire(string clientId)
    {
        TokenBucketRateLimiter limiter;
        lock (_lock)
        {
            if (!_limiters.TryGetValue(clientId, out limiter!))
            {
                limiter = new TokenBucketRateLimiter(_capacity, _tokensPerSecond);
                _limiters[clientId] = limiter;
            }
        }
        return limiter.TryAcquire(clientId);
    }
}

// ============================================================
// 5. ASP.NET Core built-in Rate Limiting (NET 7+)
//    This is how you'd wire it up in Program.cs:
// ============================================================

/*
// In Program.cs:

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    // Fixed window
    options.AddFixedWindowLimiter("fixed", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 100;
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        cfg.QueueLimit = 5;
    });

    // Sliding window
    options.AddSlidingWindowLimiter("sliding", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.SegmentsPerWindow = 6; // 10-second buckets
        cfg.PermitLimit = 100;
    });

    // Token bucket
    options.AddTokenBucketLimiter("token", cfg =>
    {
        cfg.TokenLimit = 100;
        cfg.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        cfg.TokensPerPeriod = 10;
        cfg.AutoReplenishment = true;
    });

    // Concurrency limiter (limit simultaneous requests)
    options.AddConcurrencyLimiter("concurrent", cfg =>
    {
        cfg.PermitLimit = 10;
        cfg.QueueLimit = 5;
    });

    // Return 429 with Retry-After header
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        await context.HttpContext.Response.WriteAsync("Too many requests.", ct);
    };
});

app.UseRateLimiter();

// Apply to specific endpoints:
app.MapGet("/api/products", GetProducts).RequireRateLimiting("fixed");
// Or apply globally:
// options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
//     RateLimitPartition.GetTokenBucketLimiter(
//         ctx.User.Identity?.Name ?? ctx.Request.Headers.Host.ToString(),
//         _ => new TokenBucketRateLimiterOptions { ... }));
*/

// ---------- Demo ----------
public static class RateLimitingDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Rate Limiting Demo ===\n");

        // Fixed Window — 5 requests per 2 seconds
        Console.WriteLine("--- Fixed Window (5 req / 2s) ---");
        var fixedLimiter = new FixedWindowRateLimiter(5, TimeSpan.FromSeconds(2));
        for (int i = 0; i < 8; i++)
            fixedLimiter.TryAcquire("client-A");

        Console.WriteLine("\n--- Token Bucket (capacity=5, refill=2/s) ---");
        var bucket = new TokenBucketRateLimiter(capacity: 5, tokensPerSecond: 2);

        // Burst: consume all tokens fast
        for (int i = 0; i < 7; i++)
            bucket.TryAcquire("client-B");

        // Wait for refill
        await Task.Delay(1500);
        Console.WriteLine("After 1.5s refill:");
        for (int i = 0; i < 4; i++)
            bucket.TryAcquire("client-B");

        Console.WriteLine("\n--- Per-Client Rate Limiter ---");
        var perClient = new PerClientRateLimiter(capacity: 3, tokensPerSecond: 1);
        perClient.TryAcquire("alice");
        perClient.TryAcquire("alice");
        perClient.TryAcquire("bob");
        perClient.TryAcquire("alice");
        perClient.TryAcquire("alice"); // denied
        perClient.TryAcquire("bob");   // allowed (separate bucket)
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. How do you implement rate limiting across multiple server instances?
//      Use Redis as shared store — lua scripts for atomic increment+check.
//      Libraries: StackExchange.Redis, AspNetCoreRateLimit (NuGet).
//   2. What is the difference between rate limiting and throttling?
//      Rate limiting: hard reject (429). Throttling: slow down / queue requests.
//   3. How do you identify the client for per-client rate limiting?
//      API key, user ID, IP address (careful with NAT/proxies), JWT sub claim.
//   4. What are the problems with IP-based rate limiting?
//      NAT: all users behind one IP share the limit. CDN/proxy: you see CDN IP.
//      Fix: use X-Forwarded-For header (verify the proxy is trusted!).
//   5. What HTTP headers should a well-behaved rate limiter return?
//      X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset, Retry-After.
//   6. How does a leaky bucket differ from token bucket?
//      Leaky bucket: outgoing rate is constant (smoothed). Token bucket: burst allowed up to capacity.
// ============================================================
