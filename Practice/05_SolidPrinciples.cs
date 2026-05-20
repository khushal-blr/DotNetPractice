// ============================================================
// TOPIC: SOLID Principles — all five with realistic examples
// ============================================================
// S — Single Responsibility Principle
// O — Open/Closed Principle
// L — Liskov Substitution Principle
// I — Interface Segregation Principle
// D — Dependency Inversion Principle
// ============================================================

namespace Practice.SolidPrinciples;

// ============================================================
// S — Single Responsibility Principle (SRP)
// "A class should have only ONE reason to change."
//
// BAD: OrderService doing everything — DB save, email, invoice, logging
// GOOD: each concern is its own class
// FOLLOW-UP Q: How many methods is too many for SRP? It's not about count;
//             it's about how many DIFFERENT STAKEHOLDERS can change the class.
// ============================================================

public class Order
{
    public int Id { get; init; }
    public string CustomerEmail { get; init; } = "";
    public decimal Total { get; init; }
    public List<OrderItem> Items { get; init; } = [];
}

public class OrderItem
{
    public string ProductName { get; init; } = "";
    public int Quantity { get; init; }
    public decimal Price { get; init; }
}

// Each class has ONE reason to change:
public class OrderRepository
{
    public void Save(Order order) =>
        Console.WriteLine($"[DB] Saving order {order.Id} total={order.Total:C}");
}

public class OrderEmailer
{
    public void SendConfirmation(Order order) =>
        Console.WriteLine($"[Email] Sending confirmation to {order.CustomerEmail}");
}

public class InvoiceGenerator
{
    public string Generate(Order order) =>
        $"INVOICE-{order.Id:D6} | {order.Items.Count} items | {order.Total:C}";
}

// Orchestrator — thin coordinator, not a business logic dump
public class OrderService_SRP
{
    private readonly OrderRepository _repo;
    private readonly OrderEmailer _emailer;
    private readonly InvoiceGenerator _invoicer;

    public OrderService_SRP(OrderRepository repo, OrderEmailer emailer, InvoiceGenerator invoicer)
    {
        _repo = repo; _emailer = emailer; _invoicer = invoicer;
    }

    public void PlaceOrder(Order order)
    {
        _repo.Save(order);
        _emailer.SendConfirmation(order);
        Console.WriteLine($"[OrderService] Invoice: {_invoicer.Generate(order)}");
    }
}

// ============================================================
// O — Open/Closed Principle (OCP)
// "Open for extension, closed for modification."
//
// BAD: giant switch statement in DiscountCalculator — add new type = edit class
// GOOD: introduce IDiscountStrategy; add types by adding new classes
// ============================================================

public interface IDiscountStrategy
{
    decimal Apply(decimal price);
    string Name { get; }
}

public class NoDiscount : IDiscountStrategy
{
    public string Name => "None";
    public decimal Apply(decimal price) => price;
}

public class PercentageDiscount : IDiscountStrategy
{
    private readonly decimal _pct;
    public PercentageDiscount(decimal percentOff) => _pct = percentOff / 100;
    public string Name => $"{_pct * 100}% off";
    public decimal Apply(decimal price) => price * (1 - _pct);
}

public class BuyOneGetOneFree : IDiscountStrategy
{
    public string Name => "Buy One Get One Free";
    // Applies to total price of pair (50% off effectively)
    public decimal Apply(decimal price) => price * 0.5m;
}

public class SeasonalDiscount : IDiscountStrategy
{
    private readonly decimal _flat;
    public SeasonalDiscount(decimal flatOff) => _flat = flatOff;
    public string Name => $"${_flat} seasonal";
    public decimal Apply(decimal price) => Math.Max(0, price - _flat);
}

// Closed for modification — new discount = new class, no changes here
public class PriceCalculator
{
    public decimal Calculate(decimal price, IDiscountStrategy discount)
    {
        var final = discount.Apply(price);
        Console.WriteLine($"[OCP] {discount.Name}: ${price:F2} -> ${final:F2}");
        return final;
    }
}

// ============================================================
// L — Liskov Substitution Principle (LSP)
// "Subclasses must be substitutable for their base class without breaking behaviour."
//
// CLASSIC VIOLATION: Square extends Rectangle — setting Width on a Square
//   silently changes Height, breaking callers that expect independent dimensions.
// ============================================================

// LSP-violating design (commented out — don't copy this pattern):
// class Square : Rectangle
// {
//     public override int Width  { set { base.Width = value; base.Height = value; } }
//     public override int Height { set { base.Width = value; base.Height = value; } }
// }
// Area(r) { r.Width = 4; r.Height = 5; return r.Area(); } => 25 for Square, not 20!

// LSP-correct design: separate types, shared abstraction
public interface IShape
{
    double Area();
}

public class LspRectangle : IShape
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Area() => Width * Height;
}

public class LspSquare : IShape
{
    public double Side { get; set; }
    public double Area() => Side * Side;
}

// This method works correctly regardless of which IShape is passed
public static class AreaPrinter
{
    public static void Print(IShape shape) =>
        Console.WriteLine($"[LSP] Area = {shape.Area():F2}");
}

// ============================================================
// I — Interface Segregation Principle (ISP)
// "No client should be forced to depend on methods it doesn't use."
//
// BAD: Fat IWorker interface forces Robot to implement EatLunch
// GOOD: Split into smaller focused interfaces
// ============================================================

// BAD (don't implement):
// interface IWorker { void Work(); void EatLunch(); void Sleep(); }
// class Robot : IWorker { public void Work() {...} public void EatLunch() => throw new NotImplementedException(); }

// GOOD — segregated
public interface IWorkable   { void Work(); }
public interface IFeedable   { void EatLunch(); }
public interface ISleepable  { void Sleep(); }

public class HumanWorker : IWorkable, IFeedable, ISleepable
{
    public void Work()     => Console.WriteLine("[ISP] Human: working");
    public void EatLunch() => Console.WriteLine("[ISP] Human: eating lunch");
    public void Sleep()    => Console.WriteLine("[ISP] Human: sleeping");
}

public class RobotWorker : IWorkable   // only what it needs
{
    public void Work() => Console.WriteLine("[ISP] Robot: working (no lunch needed)");
}

// Another real example: split IRepository
public interface IReader<T>  { Task<T?> GetByIdAsync(int id); Task<List<T>> GetAllAsync(); }
public interface IWriter<T>  { Task AddAsync(T entity); Task UpdateAsync(T entity); Task DeleteAsync(int id); }
public interface IRepository<T> : IReader<T>, IWriter<T> { } // full repository combines both

// A read-only service only depends on IReader — doesn't know Delete exists
public class ProductQueryService
{
    private readonly IReader<Product> _reader;
    public ProductQueryService(IReader<Product> reader) => _reader = reader;
    public async Task<Product?> FindAsync(int id) => await _reader.GetByIdAsync(id);
}

// Dummy implementations for demo
public class Product; // re-declare if not in scope

// ============================================================
// D — Dependency Inversion Principle (DIP)
// "High-level modules should not depend on low-level modules. Both depend on abstractions."
//
// BAD: NotificationService creates SmtpEmailSender directly — tightly coupled
// GOOD: Depend on INotificationChannel — swap SMS, Email, Slack without changing service
// ============================================================

public interface INotificationChannel
{
    Task SendAsync(string recipient, string subject, string body);
}

// Low-level modules
public class SmtpEmailSender : INotificationChannel
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        await Task.Delay(30);
        Console.WriteLine($"[SMTP] To:{recipient} | Subject:{subject} | {body}");
    }
}

public class SmsGatewaySender : INotificationChannel
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        await Task.Delay(20);
        Console.WriteLine($"[SMS] To:{recipient} | {body}");
    }
}

public class SlackWebhookSender : INotificationChannel
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        await Task.Delay(10);
        Console.WriteLine($"[Slack] #{recipient} | {subject}: {body}");
    }
}

// High-level module — depends on abstraction, not concrete sender
public class NotificationService
{
    private readonly IEnumerable<INotificationChannel> _channels;

    // Injected — unit tests can pass mocks; production passes real senders
    public NotificationService(IEnumerable<INotificationChannel> channels) =>
        _channels = channels;

    public async Task NotifyAllAsync(string recipient, string subject, string body)
    {
        var tasks = _channels.Select(c => c.SendAsync(recipient, subject, body));
        await Task.WhenAll(tasks);
    }
}

// ---------- Demo ----------
public static class SolidDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== SOLID Principles Demo ===\n");

        // S — SRP
        Console.WriteLine("--- S: Single Responsibility ---");
        var order = new Order { Id = 42, CustomerEmail = "user@example.com", Total = 199.99m,
            Items = [new OrderItem { ProductName = "Laptop Stand", Quantity = 1, Price = 199.99m }] };
        var svc = new OrderService_SRP(new OrderRepository(), new OrderEmailer(), new InvoiceGenerator());
        svc.PlaceOrder(order);

        // O — OCP
        Console.WriteLine("\n--- O: Open/Closed ---");
        var calc = new PriceCalculator();
        calc.Calculate(100m, new NoDiscount());
        calc.Calculate(100m, new PercentageDiscount(20));
        calc.Calculate(100m, new BuyOneGetOneFree());
        calc.Calculate(100m, new SeasonalDiscount(15));

        // L — LSP
        Console.WriteLine("\n--- L: Liskov Substitution ---");
        IShape[] shapes = { new LspRectangle { Width = 4, Height = 5 }, new LspSquare { Side = 5 } };
        foreach (var s in shapes) AreaPrinter.Print(s);

        // I — ISP
        Console.WriteLine("\n--- I: Interface Segregation ---");
        IWorkable[] workers = { new HumanWorker(), new RobotWorker() };
        foreach (var w in workers) w.Work();
        if (workers[0] is IFeedable f) f.EatLunch();

        // D — DIP
        Console.WriteLine("\n--- D: Dependency Inversion ---");
        var channels = new INotificationChannel[]
        {
            new SmtpEmailSender(),
            new SmsGatewaySender(),
            new SlackWebhookSender()
        };
        var notifier = new NotificationService(channels);
        await notifier.NotifyAllAsync("dev-team", "Deploy Complete", "v2.3.1 deployed to prod.");
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   S: How is SRP different from "one method per class"?
//      SRP is about reasons to change (stakeholders), not method count.
//   O: What design patterns implement OCP?
//      Strategy (OCP for algorithms), Decorator (OCP for behaviour), Observer.
//   L: What is a "behavioural contract"? Why does LSP go beyond just method signatures?
//      Pre/post-conditions and invariants must also hold in the subtype.
//   I: How does ISP relate to microservices?
//      Each service exposes only the contract its clients need — avoid bloated contracts.
//   D: What is the difference between DI (Dependency Injection) and IoC (Inversion of Control)?
//      IoC is the principle (control of flow/creation shifted to framework).
//      DI is one way to achieve IoC (inject dependencies from outside).
// ============================================================
