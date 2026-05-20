// ============================================================
// TOPIC: IEnumerable<T> vs IQueryable<T>
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What is the core difference between IEnumerable and IQueryable?
//      IEnumerable: pull-based in-memory iteration. LINQ is executed in C# (client-side).
//      IQueryable:  expression tree — provider translates LINQ to SQL (or other query lang).
//   Q: What is deferred execution?
//      The query is NOT run when declared — it runs when you iterate (foreach, ToList(), etc.)
//   Q: Why does calling .Where() on DbSet return IQueryable and stay as SQL?
//      EF Core builds an expression tree; calling ToList() sends the final SQL to the DB.
//   Q: What is the N+1 problem and how does IQueryable help?
//      Selecting a list and then accessing navigation property per row = N+1 queries.
//      Use .Include() on IQueryable to JOIN in a single query.
//   Q: When should you use AsEnumerable() and why?
//      When you need client-side evaluation (e.g., calling a C# method that can't be translated).
// ============================================================

namespace Practice.EnumerableVsQueryable;

// ============================================================
// 1. IEnumerable<T> — in-memory, forward-only iteration
// ============================================================

public static class EnumerableExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("=== IEnumerable<T> ===\n");

        // All data loaded into memory FIRST, then LINQ filters in C#
        var numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Deferred execution: this line does NOT iterate yet
        IEnumerable<int> evens = numbers.Where(n => n % 2 == 0);

        // Iteration happens HERE (foreach triggers it)
        Console.Write("Evens: ");
        foreach (var n in evens) Console.Write(n + " ");
        Console.WriteLine();

        // Deferred demo: mutating source BEFORE iteration changes result
        numbers.Add(12);
        Console.Write("Evens after adding 12 (re-iterates): ");
        foreach (var n in evens) Console.Write(n + " ");  // 12 appears
        Console.WriteLine();

        // Multiple enumeration problem — iterating twice hits the source twice
        // If source is a DB call or network stream, this is expensive / dangerous
        IEnumerable<int> query = numbers.Where(n => n > 5);
        Console.WriteLine($"Count: {query.Count()}");     // iterates once
        Console.WriteLine($"First: {query.First()}");     // iterates again!
        // FIX: materialise with ToList() first
        List<int> materialised = query.ToList();          // single iteration

        // Custom IEnumerable with yield — lazy generation
        Console.WriteLine("\nFibonacci (first 8):");
        foreach (var f in Fibonacci().Take(8))
            Console.Write(f + " ");
        Console.WriteLine();
    }

    // yield return creates a state machine — values produced on demand, not all upfront
    // FOLLOW-UP Q: What happens if you call Fibonacci().ToList()? Infinite loop!
    private static IEnumerable<long> Fibonacci()
    {
        long a = 0, b = 1;
        while (true)
        {
            yield return a;
            (a, b) = (b, a + b);
        }
    }
}

// ============================================================
// 2. IQueryable<T> — expression tree, translated by provider
// ============================================================

// Simulate what EF Core does internally (simplified)
public class Product
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public decimal Price { get; init; }
    public string Category { get; init; } = "";
    public bool IsActive { get; init; }
}

// In EF Core you would do:
//   DbContext.Products                  -> IQueryable<Product>
//   .Where(p => p.Price > 100)          -> adds to expression tree (NO SQL yet)
//   .OrderBy(p => p.Name)               -> adds to expression tree
//   .Skip(0).Take(20)                   -> pagination pushed to SQL
//   .ToListAsync()                      -> NOW sends SELECT ... FROM Products
//                                          WHERE Price > 100 ORDER BY Name
//                                          OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY

public static class QueryableExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("=== IQueryable<T> (simulated) ===\n");

        // In-memory demo: AsQueryable() lets us show the IQueryable API on a list
        var products = SeedProducts().AsQueryable();

        // Building up query — NOTHING executed yet
        IQueryable<Product> query = products
            .Where(p => p.IsActive)
            .Where(p => p.Price > 50);

        // Add more clauses — still no execution
        query = query.OrderBy(p => p.Name);

        // Apply pagination — in EF Core this becomes OFFSET/FETCH in SQL
        int page = 0, pageSize = 3;
        IQueryable<Product> paged = query.Skip(page * pageSize).Take(pageSize);

        // Execution happens HERE
        List<Product> results = paged.ToList();

        Console.WriteLine($"Page {page + 1} results (pageSize={pageSize}):");
        foreach (var p in results)
            Console.WriteLine($"  {p.Id} | {p.Name,-20} | ${p.Price,7:F2} | {p.Category}");

        // -------------------------------------------------------
        // CRITICAL TRAP: AsEnumerable() vs AsQueryable()
        // -------------------------------------------------------
        Console.WriteLine("\n--- AsEnumerable() trap ---");

        // BAD: This loads ALL products into memory, THEN filters in C#
        var badQuery = products
            .AsEnumerable()              // << breaks the SQL translation here
            .Where(p => p.Price > 100)  // runs in C# on all rows
            .ToList();

        // GOOD: Filter stays as SQL, only matching rows transferred
        var goodQuery = products
            .Where(p => p.Price > 100)  // expression tree — stays as SQL
            .ToList();

        Console.WriteLine($"Results (bad vs good match): {badQuery.Count} == {goodQuery.Count}");

        // -------------------------------------------------------
        // IQueryable vs IEnumerable return type from a repository
        // -------------------------------------------------------
        // Returning IQueryable from repo: allows callers to compose queries (flexible)
        //   BUT: leaks EF Core concern outside the repository (harder to test, mock)
        // Returning IEnumerable / List from repo: repo is responsible for the full query
        //   PREFERRED for clean architecture; caller gets materialized data.
        // FOLLOW-UP Q: What does "leaky abstraction" mean in this context?
    }

    // -------------------------------------------------------
    // Select projection — only fetch needed columns (avoid SELECT *)
    // -------------------------------------------------------
    public static void ProjectionDemo()
    {
        var products = SeedProducts().AsQueryable();

        // In EF Core, Select() generates a SQL projection — no unmapped columns fetched
        var names = products
            .Where(p => p.Category == "Electronics")
            .Select(p => new { p.Id, p.Name, p.Price })
            .ToList();

        Console.WriteLine("\n--- Projection (Electronics only) ---");
        foreach (var n in names)
            Console.WriteLine($"  {n.Id} {n.Name} ${n.Price:F2}");
    }

    private static List<Product> SeedProducts() =>
    [
        new Product { Id = 1, Name = "Laptop",       Price = 999.99m, Category = "Electronics", IsActive = true  },
        new Product { Id = 2, Name = "Mouse",        Price = 29.99m,  Category = "Electronics", IsActive = true  },
        new Product { Id = 3, Name = "Keyboard",     Price = 79.99m,  Category = "Electronics", IsActive = true  },
        new Product { Id = 4, Name = "Desk",         Price = 349.00m, Category = "Furniture",   IsActive = true  },
        new Product { Id = 5, Name = "Chair",        Price = 249.00m, Category = "Furniture",   IsActive = false },
        new Product { Id = 6, Name = "Monitor",      Price = 449.00m, Category = "Electronics", IsActive = true  },
        new Product { Id = 7, Name = "USB Hub",      Price = 19.99m,  Category = "Electronics", IsActive = true  },
    ];
}

// ---------- Demo ----------
public static class EnumerableQueryableDemo
{
    public static void Run()
    {
        EnumerableExamples.Demonstrate();
        Console.WriteLine();
        QueryableExamples.Demonstrate();
        QueryableExamples.ProjectionDemo();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What does ToList() do to an IQueryable in EF Core?
//      Materialises the query — sends SQL to DB, maps rows to objects.
//   2. What is the difference between .Count() on IQueryable vs IEnumerable?
//      IQueryable: generates SELECT COUNT(*) in SQL — no rows transferred.
//      IEnumerable: loads all rows into memory, then counts in C#.
//   3. What is the risk of returning IQueryable from a repository?
//      Callers can add .Include() or filter after the repo boundary — makes the
//      repo easy to bypass and hard to test/mock cleanly.
//   4. Why does .FirstOrDefault() short-circuit in IQueryable but not IEnumerable?
//      IQueryable adds TOP 1 / LIMIT 1 to SQL. IEnumerable iterates until first match.
//   5. What is the difference between Select and SelectMany?
//      Select: one-to-one projection. SelectMany: flatten nested collections (one-to-many).
// ============================================================
