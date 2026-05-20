// ============================================================
// TOPIC: Pagination — Offset, Keyset (Cursor), Infinite Scroll
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What are the two main pagination strategies?
//      Offset (skip/take): simple but slow on large tables (DB must scan skipped rows).
//      Keyset (cursor): uses WHERE id > lastId ORDER BY id — O(log n), stable, fast.
//   Q: What is the problem with offset pagination on large datasets?
//      SELECT * OFFSET 1000000 LIMIT 20 — DB scans 1,000,020 rows, discards most.
//   Q: When would you choose offset over keyset pagination?
//      Offset: user can jump to any page, sorting by arbitrary columns is simple.
//      Keyset: infinite scroll, real-time feeds, very large datasets.
//   Q: What is a "PagedResult" and what metadata does it contain?
//      Total count, current page, page size, has-next/has-previous, next cursor.
//   Q: What HTTP response format is standard for paginated APIs?
//      Varies: Link header (GitHub), meta wrapper object, or response envelope.
// ============================================================

namespace Practice.Pagination;

// ============================================================
// 1. Common models
// ============================================================

public record PagedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages  => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext    => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public record CursorPagedResult<T>(
    List<T> Items,
    string? NextCursor,  // null = no more pages
    bool HasMore)
{
    // Cursor is typically: base64(lastItem.Id) — opaque to client
    public static string EncodeCursor(int id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(id.ToString()));

    public static int DecodeCursor(string cursor) =>
        int.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
}

public record PaginationRequest(int Page = 1, int PageSize = 20)
{
    public int Skip => (Page - 1) * PageSize;

    // Guard against abuse — cap page size
    public int SafePageSize => Math.Clamp(PageSize, 1, 100);
}

// ============================================================
// 2. Entities
// ============================================================

public class BlogPost
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string Category { get; init; } = "";
    public int ViewCount { get; init; }
}

// ============================================================
// 3. Offset Pagination (Skip/Take)
//    Simple. Works for small-medium datasets or when page jumping is needed.
// ============================================================

public class OffsetPaginationService
{
    private readonly List<BlogPost> _store; // simulates DbSet<BlogPost>

    public OffsetPaginationService() => _store = SeedPosts();

    // In real EF Core:
    //   var total = await query.CountAsync(ct);
    //   var items = await query.Skip(req.Skip).Take(req.SafePageSize).ToListAsync(ct);
    public async Task<PagedResult<BlogPost>> GetPostsAsync(PaginationRequest req, string? category = null, CancellationToken ct = default)
    {
        await Task.Yield(); // simulate async DB call

        var query = _store.AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        // IMPORTANT: always ORDER before Skip/Take — undefined order = unpredictable pages
        query = query.OrderByDescending(p => p.CreatedAt);

        int total = query.Count();  // DB: SELECT COUNT(*) (no skip/take yet)

        var items = query
            .Skip(req.Skip)          // DB: OFFSET N ROWS
            .Take(req.SafePageSize)  // DB: FETCH NEXT M ROWS ONLY
            .ToList();

        return new PagedResult<BlogPost>(items, req.Page, req.SafePageSize, total);
    }

    // FOLLOW-UP Q: What happens if records are inserted/deleted between page requests?
    // Row inserted on page 1 while you're on page 2: you miss it OR see a duplicate.
    // This is the "pagination instability" problem — keyset pagination solves it.

    private static List<BlogPost> SeedPosts()
    {
        var categories = new[] { "Tech", "Design", "Business", "Health" };
        return Enumerable.Range(1, 50).Select(i => new BlogPost
        {
            Id        = i,
            Title     = $"Post Title #{i}",
            Author    = i % 3 == 0 ? "Alice" : i % 3 == 1 ? "Bob" : "Carol",
            CreatedAt = DateTime.UtcNow.AddDays(-i),
            Category  = categories[i % categories.Length],
            ViewCount = i * 100
        }).ToList();
    }
}

// ============================================================
// 4. Keyset Pagination (Cursor-based)
//    Uses WHERE id > lastSeenId — index seek, O(log n), stable.
//    No page jumping. Ideal for: infinite scroll, feeds, large datasets.
// ============================================================

public class KeysetPaginationService
{
    private readonly List<BlogPost> _store;
    public KeysetPaginationService() => _store = GeneratePosts(200);

    // In real EF Core:
    //   var query = dbContext.Posts
    //       .Where(p => p.Id > afterId)   // uses index seek
    //       .OrderBy(p => p.Id)
    //       .Take(pageSize + 1);          // fetch one extra to detect hasMore
    public async Task<CursorPagedResult<BlogPost>> GetPostsAsync(
        string? afterCursor = null,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        await Task.Yield();

        int afterId = afterCursor == null ? 0 : CursorPagedResult<BlogPost>.DecodeCursor(afterCursor);
        int safeSize = Math.Clamp(pageSize, 1, 100);

        var items = _store
            .Where(p => p.Id > afterId)   // keyset condition — uses index in real DB
            .OrderBy(p => p.Id)
            .Take(safeSize + 1)           // fetch one extra
            .ToList();

        bool hasMore = items.Count > safeSize;
        if (hasMore) items.RemoveAt(items.Count - 1); // remove extra

        string? nextCursor = hasMore
            ? CursorPagedResult<BlogPost>.EncodeCursor(items[^1].Id)
            : null;

        return new CursorPagedResult<BlogPost>(items, nextCursor, hasMore);
    }

    private static List<BlogPost> GeneratePosts(int count) =>
        Enumerable.Range(1, count).Select(i => new BlogPost
        {
            Id        = i,
            Title     = $"Keyset Post #{i}",
            Author    = "Author" + (i % 5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-i),
            Category  = "Tech",
            ViewCount = i * 50
        }).ToList();
}

// ============================================================
// 5. API response envelope — what your controller returns
// ============================================================

// GET /api/posts?page=2&pageSize=10
// Response:
// {
//   "items": [...],
//   "page": 2,
//   "pageSize": 10,
//   "totalCount": 50,
//   "totalPages": 5,
//   "hasNext": true,
//   "hasPrevious": true
// }

// GET /api/posts/feed?after=MTI1&pageSize=20
// Response:
// {
//   "items": [...],
//   "nextCursor": "MTQ1",
//   "hasMore": true
// }

// Controller example (not actually routing here — illustrative):
public class BlogController
{
    private readonly OffsetPaginationService _offset;
    private readonly KeysetPaginationService _keyset;

    public BlogController(OffsetPaginationService offset, KeysetPaginationService keyset)
    {
        _offset = offset;
        _keyset = keyset;
    }

    public async Task<PagedResult<BlogPost>> GetPostsAsync(int page = 1, int pageSize = 10)
    {
        return await _offset.GetPostsAsync(new PaginationRequest(page, pageSize));
    }

    public async Task<CursorPagedResult<BlogPost>> GetFeedAsync(string? after = null, int pageSize = 20)
    {
        return await _keyset.GetPostsAsync(after, pageSize);
    }
}

// ============================================================
// 6. Extension methods for reusable pagination on IQueryable
// ============================================================

public static class PaginationExtensions
{
    // Usage: await dbContext.Posts.OrderBy(p => p.Id).ToPagedResultAsync(req, ct);
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationRequest req,
        CancellationToken ct = default)
    {
        // In real EF Core, use CountAsync and ToListAsync
        int total = query.Count();
        var items = query.Skip(req.Skip).Take(req.SafePageSize).ToList();
        await Task.CompletedTask;
        return new PagedResult<T>(items, req.Page, req.SafePageSize, total);
    }
}

// ---------- Demo ----------
public static class PaginationDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Pagination Demo ===\n");

        // Offset pagination
        Console.WriteLine("--- Offset Pagination ---");
        var offsetSvc = new OffsetPaginationService();

        for (int page = 1; page <= 3; page++)
        {
            var result = await offsetSvc.GetPostsAsync(new PaginationRequest(page, 5));
            Console.WriteLine($"Page {result.Page}/{result.TotalPages} " +
                              $"[{result.Items.Count} items] " +
                              $"HasNext={result.HasNext} HasPrev={result.HasPrevious}");
            foreach (var p in result.Items)
                Console.WriteLine($"  [{p.Id}] {p.Title} by {p.Author}");
        }

        // Category filter
        Console.WriteLine("\n--- Filtered: Category=Tech, Page 1 ---");
        var techPage = await offsetSvc.GetPostsAsync(new PaginationRequest(1, 5), "Tech");
        Console.WriteLine($"Tech posts: {techPage.TotalCount} total, showing {techPage.Items.Count}");

        // Keyset / cursor pagination
        Console.WriteLine("\n--- Keyset Pagination (cursor-based) ---");
        var keysetSvc = new KeysetPaginationService();
        string? cursor = null;
        int pageNum = 0;

        do
        {
            pageNum++;
            var page = await keysetSvc.GetPostsAsync(cursor, pageSize: 10);
            cursor = page.NextCursor;

            Console.WriteLine($"Keyset page {pageNum}: {page.Items.Count} items | " +
                              $"HasMore={page.HasMore} | NextCursor={cursor?[..8]}...");

            if (pageNum >= 3) break; // stop after 3 pages for demo
        }
        while (cursor != null);
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. How do you paginate with multiple sort columns in keyset pagination?
//      Compound cursor: e.g., WHERE (created_at, id) < (@lastDate, @lastId)
//      Requires composite index on (created_at, id).
//   2. Why should you avoid COUNT(*) on every request?
//      On large tables, COUNT(*) is expensive. Cache the total count or omit it for
//      cursor-based pagination (you don't need total for infinite scroll).
//   3. How does EF Core's .Skip().Take() translate to SQL?
//      SQL Server: OFFSET N ROWS FETCH NEXT M ROWS ONLY
//      MySQL/Postgres: LIMIT M OFFSET N
//   4. What is the GraphQL Relay spec for pagination?
//      Edges/nodes/pageInfo structure with cursor, hasNextPage, hasPreviousPage.
//   5. How do you implement search + pagination together?
//      Apply WHERE/full-text search first, then paginate the filtered result set.
//      Use COUNT on filtered query. In SQL Server: CONTAINSTABLE / FREETEXTTABLE.
//   6. What is deferred loading and how does it affect pagination in EF Core?
//      Lazy loading navigation properties inside a loop causes N+1 queries.
//      Fix: eager load with .Include() in the paginated query.
// ============================================================
