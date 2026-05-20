// ============================================================
// TOPIC: HTTP API Calls — HttpClient, IHttpClientFactory, Typed Client, Retry
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: Why shouldn't you use `new HttpClient()` in a loop / per request?
//      HttpClient holds open connections. Creating/disposing per request causes socket
//      exhaustion (TIME_WAIT). Also misses DNS refresh when using long-lived client.
//   Q: What is IHttpClientFactory and why use it?
//      Manages HttpClient lifetime and handler pooling. Named/typed clients allow
//      pre-configured base URLs, headers, auth. Integrates with DI.
//   Q: What is a Typed HttpClient?
//      A strongly-typed wrapper class that takes HttpClient in its constructor.
//      Registered with AddHttpClient<T>() — each type gets its own pre-configured client.
//   Q: What is Polly and what policies does it provide?
//      Resilience library: Retry, Circuit Breaker, Timeout, Bulkhead, Fallback.
//   Q: What is a Circuit Breaker pattern?
//      After N failures, stop calling the failing service for a period.
//      States: Closed (normal) -> Open (blocking) -> Half-Open (testing).
//   Q: What is the difference between HttpRequestMessage and just calling GetAsync?
//      HttpRequestMessage gives full control: custom headers, method, content.
// ============================================================

namespace Practice.HttpApiCall;

// ============================================================
// 1. Models
// ============================================================

public record GitHubUser(
    string Login,
    int Id,
    string? Name,
    string? Email,
    int PublicRepos,
    int Followers);

public record GitHubRepo(
    string Name,
    string? Description,
    string Language,
    int StargazersCount);

public record ApiResponse<T>(bool Success, T? Data, string? Error);

// ============================================================
// 2. Typed HttpClient — the recommended pattern
// ============================================================

public class GitHubApiClient
{
    private readonly HttpClient _client;
    private readonly ILogger<GitHubApiClient>? _logger;

    // IHttpClientFactory injects a pre-configured HttpClient
    public GitHubApiClient(HttpClient client, ILogger<GitHubApiClient>? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    // GET with error handling and deserialization
    public async Task<ApiResponse<GitHubUser>> GetUserAsync(string username, CancellationToken ct = default)
    {
        try
        {
            // Build request explicitly — full control over headers, method
            var request = new HttpRequestMessage(HttpMethod.Get, $"users/{username}");
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString());

            var response = await _client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogWarning("GitHub API returned {Status} for user {Username}: {Body}",
                    response.StatusCode, username, body);
                return new ApiResponse<GitHubUser>(false, null, $"HTTP {(int)response.StatusCode}");
            }

            // ReadFromJsonAsync — deserialises using System.Text.Json
            var user = await response.Content.ReadFromJsonAsync<GitHubUser>(
                cancellationToken: ct);

            return new ApiResponse<GitHubUser>(true, user, null);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error fetching GitHub user {Username}", username);
            return new ApiResponse<GitHubUser>(false, null, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return new ApiResponse<GitHubUser>(false, null, "Request timed out or cancelled");
        }
    }

    // GET list with pagination headers
    public async Task<List<GitHubRepo>> GetUserReposAsync(string username, int page = 1, int perPage = 30, CancellationToken ct = default)
    {
        var response = await _client.GetAsync(
            $"users/{username}/repos?page={page}&per_page={perPage}&sort=updated", ct);

        response.EnsureSuccessStatusCode();

        var repos = await response.Content.ReadFromJsonAsync<List<GitHubRepo>>(ct);
        return repos ?? [];
    }

    // POST — creating a resource
    public async Task<HttpResponseMessage> CreateIssueAsync(string owner, string repo, object issueBody, CancellationToken ct = default)
    {
        // JsonContent serialises the object and sets Content-Type: application/json
        var content = JsonContent.Create(issueBody);
        return await _client.PostAsync($"repos/{owner}/{repo}/issues", content, ct);
    }
}

// ============================================================
// 3. Registration in Program.cs (ASP.NET Core)
// ============================================================

/*
// In Program.cs:

builder.Services.AddHttpClient<GitHubApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
})
// Polly retry: retry 3 times with exponential backoff on transient errors
.AddTransientHttpErrorPolicy(policy =>
    policy.WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (outcome, delay, attempt, _) =>
            Console.WriteLine($"Retry {attempt} after {delay.TotalSeconds}s: {outcome.Exception?.Message}")))
// Circuit breaker: open after 5 failures, stay open 30 seconds
.AddTransientHttpErrorPolicy(policy =>
    policy.CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (outcome, delay) => Console.WriteLine($"Circuit OPEN for {delay.TotalSeconds}s"),
        onReset: () => Console.WriteLine("Circuit CLOSED — resuming normal calls")));
*/

// ============================================================
// 4. Manual retry with exponential backoff (without Polly)
//    Shows the pattern in case interviewer asks you to implement it
// ============================================================

public static class RetryHelper
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation(ct);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                Console.WriteLine($"[Retry] Attempt {attempt}/{maxRetries} failed: {ex.Message}. Waiting {delay.TotalSeconds}s...");
                await Task.Delay(delay, ct);
            }
        }
    }
}

// ============================================================
// 5. Low-level HttpRequestMessage — full control
// ============================================================

public static class HttpRequestMessageExamples
{
    public static async Task DemonstrateAsync(HttpClient client)
    {
        Console.WriteLine("--- HttpRequestMessage examples ---");

        // Custom headers, conditional request
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
        request.Headers.Add("Authorization", "Bearer my-token");
        request.Headers.Add("Accept-Language", "en-US");
        request.Headers.Add("If-None-Match", "\"etag-value\""); // cache validation

        // Multipart form (file upload)
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent("hello"), "field1");
        multipart.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "upload.bin");

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/upload")
        {
            Content = multipart
        };

        // PATCH with JSON
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, "/resource/1")
        {
            Content = JsonContent.Create(new { name = "updated" })
        };

        Console.WriteLine("HttpRequestMessage examples constructed (not sending — no real server).");
        await Task.CompletedTask; // satisfy async signature
    }
}

// ============================================================
// 6. Named clients — when you need multiple differently-configured clients
// ============================================================

/*
// Named client (less typed than TypedClient, but flexible):
builder.Services.AddHttpClient("payments", client =>
{
    client.BaseAddress = new Uri("https://api.stripe.com/v1/");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {stripeKey}");
});

// Usage in a service:
public class PaymentService(IHttpClientFactory factory)
{
    public async Task ChargeAsync(decimal amount)
    {
        var client = factory.CreateClient("payments"); // gets pre-configured client
        var response = await client.PostAsync("charges", JsonContent.Create(new { amount }));
    }
}
*/

// ---------- Demo ----------
public static class HttpApiCallDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== HTTP API Call Demo ===\n");

        // Demonstrates manual retry logic (no real HTTP call)
        Console.WriteLine("--- Retry with exponential backoff ---");
        int attempt = 0;
        try
        {
            var result = await RetryHelper.ExecuteWithRetryAsync<string>(async ct =>
            {
                attempt++;
                await Task.Delay(10, ct);
                if (attempt < 3) throw new HttpRequestException($"Simulated failure {attempt}");
                return "Success on attempt 3";
            });
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex) { Console.WriteLine($"Failed: {ex.Message}"); }

        // Show what a registered typed client would look like when used
        Console.WriteLine("\n--- Typed Client pattern (registration in Program.cs) ---");
        Console.WriteLine("builder.Services.AddHttpClient<GitHubApiClient>(client => {");
        Console.WriteLine("    client.BaseAddress = new Uri(\"https://api.github.com/\");");
        Console.WriteLine("    client.DefaultRequestHeaders.UserAgent.ParseAdd(\"MyApp/1.0\");");
        Console.WriteLine("}).AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2,i))));");

        Console.WriteLine("\nWith DI, inject GitHubApiClient into controller constructor:");
        Console.WriteLine("public MyController(GitHubApiClient github) => _github = github;");
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is socket exhaustion and how does IHttpClientFactory prevent it?
//      Too many TIME_WAIT sockets. Factory reuses HttpMessageHandler (connection pool)
//      across multiple HttpClient instances.
//   2. What is the handler lifetime and why does it matter?
//      Default handler lifetime in factory is 2 minutes — allows DNS refresh.
//      Too long: stale DNS. Too short: overhead of recreating connection pools.
//   3. What is the difference between Polly Retry and Circuit Breaker?
//      Retry: try again. Circuit Breaker: stop trying when failure rate is too high.
//      Together: retry a few times, then open circuit to protect the downstream service.
//   4. What HTTP status codes should trigger a retry?
//      408 (Timeout), 429 (Rate limited — with Retry-After delay), 503, 504.
//      NOT 400, 401, 403, 404 — those are client errors, won't succeed on retry.
//   5. How do you handle HttpClient in unit tests?
//      Mock HttpMessageHandler — inject via constructor. Don't mock HttpClient directly.
//   6. What is gRPC and when would you use it over REST?
//      Binary protocol over HTTP/2, strongly typed contracts (protobuf), lower latency.
//      Use for internal service-to-service calls where performance matters.
// ============================================================
