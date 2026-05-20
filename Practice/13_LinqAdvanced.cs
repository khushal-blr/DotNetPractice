// ============================================================
// TOPIC: LINQ — Real-World Use Cases, Loop Design, Advanced Operators
// ============================================================
// Covers: GroupBy, SelectMany, Join/GroupJoin, Aggregate, ToLookup,
//         Zip, Chunk, DistinctBy, MinBy/MaxBy, TakeWhile/SkipWhile,
//         query syntax vs method syntax, LINQ vs manual loops,
//         common traps, custom comparers
//
// KEY INTERVIEW QUESTIONS:
//   Q: When should you use LINQ vs a manual foreach loop?
//      LINQ: declarative, readable, composable. Loop: when you need early mutation,
//      complex state, multiple passes, or imperative control flow.
//   Q: What is the difference between GroupBy and ToLookup?
//      GroupBy: deferred — elements grouped lazily when iterated.
//      ToLookup: immediate — builds a dictionary-like structure up front, O(1) lookup.
//   Q: What is SelectMany used for?
//      Flattening nested collections (one-to-many). Equivalent to nested foreach loops.
//   Q: What is the difference between Join and GroupJoin?
//      Join: flat inner join — one result row per match.
//      GroupJoin: left outer join — one result row per left item, with grouped right items.
// ============================================================

namespace Practice.LinqAdvanced;

// ============================================================
// Domain Models (real-world e-commerce system)
// ============================================================

public class Customer
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Region { get; init; } = "";
    public string Tier { get; init; } = "Standard"; // Standard, Gold, Platinum
}

public class Order
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public DateTime OrderDate { get; init; }
    public string Status { get; init; } = ""; // Pending, Shipped, Delivered, Cancelled
    public List<OrderLine> Lines { get; init; } = [];
}

public class OrderLine
{
    public string ProductName { get; init; } = "";
    public string Category { get; init; } = "";
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class Product
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal Price { get; init; }
    public int StockLevel { get; init; }
    public List<string> Tags { get; init; } = [];
}

public class Employee
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Department { get; init; } = "";
    public decimal Salary { get; init; }
    public int? ManagerId { get; init; }
}

// ============================================================
// Test data
// ============================================================

public static class TestData
{
    public static List<Customer> Customers() =>
    [
        new() { Id=1, Name="Alice",   Region="North", Tier="Platinum" },
        new() { Id=2, Name="Bob",     Region="South", Tier="Gold"     },
        new() { Id=3, Name="Carol",   Region="North", Tier="Standard" },
        new() { Id=4, Name="Dave",    Region="East",  Tier="Gold"     },
        new() { Id=5, Name="Eve",     Region="West",  Tier="Platinum" },
    ];

    public static List<Order> Orders() =>
    [
        new() { Id=101, CustomerId=1, OrderDate=new(2024,1,10), Status="Delivered",
            Lines=[new(){ProductName="Laptop", Category="Electronics", Quantity=1, UnitPrice=999m},
                   new(){ProductName="Mouse",  Category="Electronics", Quantity=2, UnitPrice=29m}]},
        new() { Id=102, CustomerId=1, OrderDate=new(2024,3,15), Status="Shipped",
            Lines=[new(){ProductName="Keyboard",Category="Electronics",Quantity=1,UnitPrice=79m}]},
        new() { Id=103, CustomerId=2, OrderDate=new(2024,2,1),  Status="Delivered",
            Lines=[new(){ProductName="Desk",   Category="Furniture",  Quantity=1,UnitPrice=349m}]},
        new() { Id=104, CustomerId=3, OrderDate=new(2024,2,20), Status="Pending",
            Lines=[new(){ProductName="Chair",  Category="Furniture",  Quantity=2,UnitPrice=249m}]},
        new() { Id=105, CustomerId=4, OrderDate=new(2024,3,5),  Status="Cancelled",
            Lines=[new(){ProductName="Monitor",Category="Electronics",Quantity=1,UnitPrice=449m}]},
        new() { Id=106, CustomerId=5, OrderDate=new(2024,3,20), Status="Delivered",
            Lines=[new(){ProductName="Laptop", Category="Electronics",Quantity=2,UnitPrice=999m},
                   new(){ProductName="Desk",   Category="Furniture",  Quantity=1,UnitPrice=349m}]},
    ];

    public static List<Product> Products() =>
    [
        new() { Id=1, Name="Laptop",   Category="Electronics", Price=999m, StockLevel=10, Tags=["portable","powerful","business"]},
        new() { Id=2, Name="Mouse",    Category="Electronics", Price=29m,  StockLevel=100,Tags=["wireless","ergonomic"]},
        new() { Id=3, Name="Keyboard", Category="Electronics", Price=79m,  StockLevel=50, Tags=["mechanical","business"]},
        new() { Id=4, Name="Desk",     Category="Furniture",   Price=349m, StockLevel=5,  Tags=["oak","large","office"]},
        new() { Id=5, Name="Chair",    Category="Furniture",   Price=249m, StockLevel=8,  Tags=["ergonomic","adjustable"]},
        new() { Id=6, Name="Monitor",  Category="Electronics", Price=449m, StockLevel=0,  Tags=["4k","wide"]},
    ];

    public static List<Employee> Employees() =>
    [
        new() { Id=1, Name="CEO Sara",     Department="Executive", Salary=200000m, ManagerId=null },
        new() { Id=2, Name="CTO Mike",     Department="Tech",      Salary=180000m, ManagerId=1   },
        new() { Id=3, Name="Dev Alice",    Department="Tech",      Salary=120000m, ManagerId=2   },
        new() { Id=4, Name="Dev Bob",      Department="Tech",      Salary=110000m, ManagerId=2   },
        new() { Id=5, Name="CFO Carol",    Department="Finance",   Salary=170000m, ManagerId=1   },
        new() { Id=6, Name="Acc Dave",     Department="Finance",   Salary=90000m,  ManagerId=5   },
        new() { Id=7, Name="Sales Eve",    Department="Sales",     Salary=85000m,  ManagerId=1   },
        new() { Id=8, Name="Sales Frank",  Department="Sales",     Salary=80000m,  ManagerId=1   },
    ];
}

// ============================================================
// 1. GroupBy — analytics, aggregation
// ============================================================

public static class GroupByExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("=== GroupBy ===\n");
        var orders = TestData.Orders();

        // Group orders by status, show count and total revenue per group
        var byStatus = orders
            .GroupBy(o => o.Status)
            .Select(g => new
            {
                Status  = g.Key,
                Count   = g.Count(),
                Revenue = g.Sum(o => o.Lines.Sum(l => l.LineTotal))
            })
            .OrderByDescending(x => x.Revenue);

        Console.WriteLine("Orders by status:");
        foreach (var g in byStatus)
            Console.WriteLine($"  {g.Status,-12} | {g.Count} orders | ${g.Revenue:N0}");

        // Multi-key GroupBy — group by year + month (real-world: monthly report)
        var byMonth = orders
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Period  = $"{g.Key.Year}-{g.Key.Month:D2}",
                Orders  = g.Count(),
                Revenue = g.Sum(o => o.Lines.Sum(l => l.LineTotal))
            });

        Console.WriteLine("\nMonthly revenue:");
        foreach (var m in byMonth)
            Console.WriteLine($"  {m.Period} | {m.Orders} orders | ${m.Revenue:N0}");
    }
}

// ============================================================
// 2. SelectMany — flatten nested collections
// ============================================================

public static class SelectManyExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== SelectMany ===\n");
        var orders  = TestData.Orders();
        var products = TestData.Products();

        // All order lines across all orders (flatten)
        var allLines = orders.SelectMany(o => o.Lines);
        Console.WriteLine($"Total line items across all orders: {allLines.Count()}");

        // With parent context: keep order info alongside each line
        var linesWithOrder = orders.SelectMany(
            o => o.Lines,
            (o, line) => new { OrderId = o.Id, o.CustomerId, line.ProductName, line.LineTotal }
        );
        Console.WriteLine("\nAll lines with order context:");
        foreach (var l in linesWithOrder.Take(4))
            Console.WriteLine($"  Order#{l.OrderId} C#{l.CustomerId} {l.ProductName,-12} ${l.LineTotal:N0}");

        // Flatten Tags — each product has a list of tags
        var allTags = products
            .SelectMany(p => p.Tags, (p, tag) => new { p.Name, Tag = tag })
            .OrderBy(x => x.Tag);

        Console.WriteLine("\nAll product tags:");
        foreach (var t in allTags.Take(6))
            Console.WriteLine($"  [{t.Tag}] on {t.Name}");

        // Find products with a specific tag
        var ergonomic = products
            .Where(p => p.Tags.Any(t => t == "ergonomic"))
            .Select(p => p.Name);
        Console.WriteLine($"\nErgonomic products: {string.Join(", ", ergonomic)}");
    }
}

// ============================================================
// 3. Join / GroupJoin
// ============================================================

public static class JoinExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Join / GroupJoin ===\n");
        var customers = TestData.Customers();
        var orders    = TestData.Orders();

        // Inner join — only customers WITH orders
        var joined = customers.Join(
            orders,
            c => c.Id,
            o => o.CustomerId,
            (c, o) => new { c.Name, c.Tier, OrderId = o.Id, o.Status,
                            Total = o.Lines.Sum(l => l.LineTotal) }
        );

        Console.WriteLine("Inner join (customer-order):");
        foreach (var row in joined)
            Console.WriteLine($"  {row.Name,-8} [{row.Tier,-8}] Order#{row.OrderId} {row.Status,-10} ${row.Total:N0}");

        // GroupJoin — LEFT outer join: all customers, grouped with their orders (0 or more)
        var groupJoined = customers.GroupJoin(
            orders,
            c => c.Id,
            o => o.CustomerId,
            (c, custOrders) => new
            {
                c.Name,
                c.Tier,
                OrderCount = custOrders.Count(),
                TotalSpent = custOrders.Sum(o => o.Lines.Sum(l => l.LineTotal))
            }
        );

        Console.WriteLine("\nGroup join (all customers, order summary):");
        foreach (var row in groupJoined)
            Console.WriteLine($"  {row.Name,-8} [{row.Tier,-8}] {row.OrderCount} orders | total ${row.TotalSpent:N0}");

        // Self-join — employee -> manager name (hierarchical data)
        var employees = TestData.Employees();
        var withManagers = employees.GroupJoin(
            employees,
            emp => emp.ManagerId,
            mgr => (int?)mgr.Id,
            (emp, managers) => new { emp.Name, Manager = managers.FirstOrDefault()?.Name ?? "None" }
        );

        Console.WriteLine("\nEmployee -> Manager:");
        foreach (var e in withManagers.Take(4))
            Console.WriteLine($"  {e.Name,-15} reports to: {e.Manager}");
    }
}

// ============================================================
// 4. Aggregate — custom fold / reduce
// ============================================================

public static class AggregateExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Aggregate ===\n");

        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Aggregate = Reduce/Fold. Seed=0, accumulate sum
        int sum     = numbers.Aggregate(0, (acc, n) => acc + n);
        int product = numbers.Aggregate(1, (acc, n) => acc * n);
        Console.WriteLine($"Sum={sum}, Product={product}");

        // Running total
        var running = numbers
            .Aggregate(new List<int>(), (acc, n) =>
            {
                acc.Add(acc.Count == 0 ? n : acc[^1] + n);
                return acc;
            });
        Console.WriteLine($"Running totals: [{string.Join(", ", running)}]");

        // Build a histogram of product categories
        var lines = TestData.Orders().SelectMany(o => o.Lines);
        var histogram = lines.Aggregate(
            new Dictionary<string, decimal>(),
            (dict, line) =>
            {
                dict.TryGetValue(line.Category, out var existing);
                dict[line.Category] = existing + line.LineTotal;
                return dict;
            });

        Console.WriteLine("\nRevenue by category (via Aggregate):");
        foreach (var (cat, rev) in histogram)
            Console.WriteLine($"  {cat,-15} ${rev:N0}");

        // vs cleaner GroupBy approach (both valid, GroupBy more readable)
        var byCategory = TestData.Orders()
            .SelectMany(o => o.Lines)
            .GroupBy(l => l.Category)
            .Select(g => new { Category = g.Key, Revenue = g.Sum(l => l.LineTotal) });
        // FOLLOW-UP Q: when would you prefer Aggregate over GroupBy?
        // When you need a non-dictionary accumulator (string builder, custom object, running state)
    }
}

// ============================================================
// 5. ToLookup — fast multi-value dictionary
// ============================================================

public static class ToLookupExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== ToLookup ===\n");

        var orders = TestData.Orders();

        // Build lookup ONCE — O(n) build, O(1) per lookup
        // vs GroupBy which re-enumerates each time you access a group
        ILookup<string, Order> byStatus = orders.ToLookup(o => o.Status);

        Console.WriteLine("Delivered orders:");
        foreach (var o in byStatus["Delivered"])
            Console.WriteLine($"  Order#{o.Id} C#{o.CustomerId}");

        Console.WriteLine($"\nPending count: {byStatus["Pending"].Count()}");
        Console.WriteLine($"Nonexistent key returns empty (no KeyNotFoundException): {byStatus["Unknown"].Count()}");

        // Real-world: pre-build order lookup for batch processing
        // Instead of LINQ .Where() inside a loop (O(n²)), build once O(n) + O(1) lookup
        var customers = TestData.Customers();
        var ordersByCustomer = orders.ToLookup(o => o.CustomerId);

        Console.WriteLine("\nPer-customer order count:");
        foreach (var c in customers)
        {
            var custOrders = ordersByCustomer[c.Id]; // O(1) — no re-scan
            Console.WriteLine($"  {c.Name,-8} {custOrders.Count()} orders");
        }
        // FOLLOW-UP Q: Why is ToLookup better than a GroupBy inside a foreach?
        // GroupBy + foreach loops over orders for EVERY customer = O(n * m).
        // ToLookup builds once = O(n + m).
    }
}

// ============================================================
// 6. Zip, Chunk, DistinctBy, MinBy/MaxBy
// ============================================================

public static class ModernLinqExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Zip / Chunk / DistinctBy / MinBy / MaxBy ===\n");

        // Zip — pair two sequences element-by-element
        var names = new[] { "Alice", "Bob", "Carol" };
        var scores = new[] { 92, 85, 78 };
        var paired = names.Zip(scores, (name, score) => $"{name}: {score}");
        Console.WriteLine("Zip: " + string.Join(", ", paired));

        // Chunk — split into batches (great for bulk processing)
        var ids = Enumerable.Range(1, 10).ToList();
        Console.WriteLine("\nChunk into batches of 3:");
        foreach (var batch in ids.Chunk(3))
            Console.WriteLine($"  Batch: [{string.Join(", ", batch)}]");

        // DistinctBy — deduplicate by a key projection
        var employees = TestData.Employees();
        var uniqueDepts = employees.DistinctBy(e => e.Department).Select(e => e.Department);
        Console.WriteLine("\nDistinct departments: " + string.Join(", ", uniqueDepts));

        // MinBy / MaxBy — find element, not just the min/max value
        var highest = employees.MaxBy(e => e.Salary)!;
        var lowest  = employees.MinBy(e => e.Salary)!;
        Console.WriteLine($"\nHighest paid: {highest.Name} (${highest.Salary:N0})");
        Console.WriteLine($"Lowest paid:  {lowest.Name} (${lowest.Salary:N0})");

        // TakeWhile / SkipWhile — stop at condition (not filter whole sequence)
        var sortedSalaries = employees.OrderByDescending(e => e.Salary).Select(e => e.Salary);
        var above100k = sortedSalaries.TakeWhile(s => s >= 100000);
        Console.WriteLine($"\nEmployees earning ≥100k: {above100k.Count()}");
    }
}

// ============================================================
// 7. LINQ vs manual loop — when each wins
// ============================================================

public static class LinqVsLoop
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== LINQ vs Loop ===\n");

        var orders = TestData.Orders();

        // --- LINQ: great for read-only transformations ---
        var topCustomers = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new { g.Key, Spend = g.Sum(o => o.Lines.Sum(l => l.LineTotal)) })
            .OrderByDescending(x => x.Spend)
            .Take(3);

        Console.WriteLine("Top 3 customers by spend (LINQ):");
        foreach (var c in topCustomers)
            Console.WriteLine($"  Customer {c.Key}: ${c.Spend:N0}");

        // --- Loop: better when you need side effects or early mutation ---
        // Example: process orders and update a mutable state object
        var report = new Dictionary<string, (int Count, decimal Revenue)>();

        foreach (var order in orders)
        {
            if (order.Status == "Cancelled") continue;        // early skip
            foreach (var line in order.Lines)
            {
                var (count, rev) = report.GetValueOrDefault(line.Category);
                report[line.Category] = (count + line.Quantity, rev + line.LineTotal);
            }
        }

        Console.WriteLine("\nCategory report (loop with mutable state):");
        foreach (var (cat, (count, rev)) in report)
            Console.WriteLine($"  {cat,-15} qty={count} rev=${rev:N0}");

        // FOLLOW-UP Q: Is LINQ thread-safe?
        // Reading from collections with LINQ is safe if collection isn't mutated.
        // Aggregating across threads requires ConcurrentDictionary or locking.
    }
}

// ============================================================
// 8. Query syntax vs method syntax
// ============================================================

public static class QueryVsMethod
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Query syntax vs Method syntax ===\n");

        var customers = TestData.Customers();
        var orders    = TestData.Orders();

        // Method syntax — more composable, familiar to functional programmers
        var methodResult = customers
            .Where(c => c.Tier == "Gold" || c.Tier == "Platinum")
            .Join(orders, c => c.Id, o => o.CustomerId, (c, o) => new { c.Name, c.Tier, o.Id })
            .OrderBy(x => x.Name)
            .ToList();

        // Query syntax — closer to SQL, readable for complex joins and let clauses
        var queryResult = (
            from c in customers
            where c.Tier == "Gold" || c.Tier == "Platinum"
            join o in orders on c.Id equals o.CustomerId
            orderby c.Name
            select new { c.Name, c.Tier, o.Id }
        ).ToList();

        Console.WriteLine($"Method syntax count: {methodResult.Count}");
        Console.WriteLine($"Query syntax count:  {queryResult.Count} (same result)");

        // let clause — compute intermediate value (query syntax advantage)
        var withTotal = (
            from o in orders
            let total = o.Lines.Sum(l => l.LineTotal)
            where total > 200
            orderby total descending
            select new { o.Id, Total = total }
        ).ToList();

        Console.WriteLine("\nOrders > $200 (let clause):");
        foreach (var x in withTotal) Console.WriteLine($"  Order#{x.Id} ${x.Total:N0}");
    }
}

// ============================================================
// 9. Custom EqualityComparer — DistinctBy advanced, Intersect, Except
// ============================================================

public class ProductByNameComparer : IEqualityComparer<Product>
{
    public bool Equals(Product? x, Product? y) =>
        string.Equals(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);
    public int GetHashCode(Product obj) =>
        obj.Name.ToLowerInvariant().GetHashCode();
}

public static class ComparerExamples
{
    public static void Demonstrate()
    {
        Console.WriteLine("\n=== Custom Comparer ===\n");
        var p1 = TestData.Products();
        var p2 = new List<Product> { new() { Id=99, Name="laptop", Category="Electronics", Price=0m } }; // same name diff case

        // Intersect with custom comparer — find products in both lists by name
        var common = p1.Intersect(p2, new ProductByNameComparer());
        Console.WriteLine("Common (case-insensitive name): " + string.Join(", ", common.Select(p => p.Name)));

        // Except — products in p1 not in p2
        var onlyInP1 = p1.Except(p2, new ProductByNameComparer());
        Console.WriteLine("Only in p1: " + string.Join(", ", onlyInP1.Select(p => p.Name)));
    }
}

// ---------- Demo ----------
public static class LinqAdvancedDemo
{
    public static void Run()
    {
        GroupByExamples.Demonstrate();
        SelectManyExamples.Demonstrate();
        JoinExamples.Demonstrate();
        AggregateExamples.Demonstrate();
        ToLookupExamples.Demonstrate();
        ModernLinqExamples.Demonstrate();
        LinqVsLoop.Demonstrate();
        QueryVsMethod.Demonstrate();
        ComparerExamples.Demonstrate();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. What is the difference between Count() and Count property?
//      Count(): LINQ extension — may iterate the full sequence.
//      Count property: direct access (List, Array) — O(1). Prefer Count property when available.
//   2. What is short-circuit evaluation in Any() vs Where().Count() > 0?
//      Any() stops at first match. Where().Count() enumerates everything.
//   3. Can you use LINQ on arrays? YES — arrays implement IEnumerable<T>.
//   4. What is the performance difference between .Where().First() and .First(predicate)?
//      .First(predicate) is shorter and internally equivalent — both short-circuit.
//   5. What does Enumerable.Empty<T>() do and when is it useful?
//      Returns a singleton empty enumerable with no allocation — useful as a default value.
//   6. What is the difference between Concat and Union?
//      Concat: appends (may have duplicates). Union: distinct elements from both.
//   7. What is OrderBy stability in LINQ?
//      LINQ OrderBy is a stable sort — equal elements maintain original order.
//      LINQ OrderByDescending is also stable.
//   8. What is Enumerable.Range vs Enumerable.Repeat?
//      Range: sequence 0..n. Repeat: same element n times.
// ============================================================
