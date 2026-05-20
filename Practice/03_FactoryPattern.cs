// ============================================================
// TOPIC: Factory Pattern (Simple Factory, Factory Method, Abstract Factory)
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: What is the difference between Simple Factory, Factory Method, Abstract Factory?
//      Simple Factory:    one class with a static Create() method — not a true GoF pattern.
//      Factory Method:    abstract base/interface defines CreateProduct(); subclasses decide type.
//      Abstract Factory:  factory of factories — create families of related objects.
//   Q: Why use a factory over `new`?
//      Decouples creation from use, allows swapping implementations, centralises complexity.
//   Q: Where does the Factory pattern appear in .NET?
//      HttpClientFactory, ILoggerFactory, DbProviderFactory, ServiceProvider itself.
// ============================================================

namespace Practice.FactoryPattern;

// ============================================================
// SCENARIO: Payment processing — support Stripe, PayPal, BankTransfer
// ============================================================

// ---------- Product interface ----------
public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessAsync(decimal amount, string currency);
    string ProviderName { get; }
}

public record PaymentResult(bool Success, string TransactionId, string Message);

// ---------- Concrete products ----------
public class StripeProcessor : IPaymentProcessor
{
    public string ProviderName => "Stripe";

    public async Task<PaymentResult> ProcessAsync(decimal amount, string currency)
    {
        await Task.Delay(50); // simulate network
        var txId = $"stripe_{Guid.NewGuid():N}";
        Console.WriteLine($"[Stripe] Charged {amount} {currency} | txId={txId}");
        return new PaymentResult(true, txId, "Stripe payment successful");
    }
}

public class PayPalProcessor : IPaymentProcessor
{
    public string ProviderName => "PayPal";

    public async Task<PaymentResult> ProcessAsync(decimal amount, string currency)
    {
        await Task.Delay(60);
        var txId = $"pp_{Guid.NewGuid():N}";
        Console.WriteLine($"[PayPal] Charged {amount} {currency} | txId={txId}");
        return new PaymentResult(true, txId, "PayPal payment successful");
    }
}

public class BankTransferProcessor : IPaymentProcessor
{
    public string ProviderName => "BankTransfer";

    public async Task<PaymentResult> ProcessAsync(decimal amount, string currency)
    {
        await Task.Delay(100);
        var txId = $"bank_{Guid.NewGuid():N}";
        Console.WriteLine($"[Bank] Initiated transfer of {amount} {currency} | txId={txId}");
        return new PaymentResult(true, txId, "Bank transfer initiated");
    }
}

// ============================================================
// 1. SIMPLE FACTORY — static creation method
//    Not a true GoF pattern but common in interviews.
//    Problem: violates OCP — adding new providers means editing this class.
// ============================================================
public static class PaymentProcessorSimpleFactory
{
    public static IPaymentProcessor Create(string provider) => provider switch
    {
        "stripe"       => new StripeProcessor(),
        "paypal"       => new PayPalProcessor(),
        "banktransfer" => new BankTransferProcessor(),
        _ => throw new ArgumentException($"Unknown provider: {provider}")
    };
}

// ============================================================
// 2. FACTORY METHOD — abstract base defines the hook, subclasses decide type
//    Follows OCP: add a new PaymentService subclass without touching existing ones.
// ============================================================
public abstract class PaymentService
{
    // Factory method — subclass decides which processor to create
    protected abstract IPaymentProcessor CreateProcessor();

    // Template: shared workflow around the factory method
    public async Task<PaymentResult> ChargeAsync(decimal amount, string currency)
    {
        var processor = CreateProcessor();
        Console.WriteLine($"[PaymentService] Using {processor.ProviderName}");
        ValidateAmount(amount);
        var result = await processor.ProcessAsync(amount, currency);
        LogResult(result);
        return result;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");
    }

    private static void LogResult(PaymentResult r) =>
        Console.WriteLine($"[PaymentService] Result: {r.Message} (tx={r.TransactionId})");
}

public class StripePaymentService : PaymentService
{
    protected override IPaymentProcessor CreateProcessor() => new StripeProcessor();
}

public class PayPalPaymentService : PaymentService
{
    protected override IPaymentProcessor CreateProcessor() => new PayPalProcessor();
}

// ============================================================
// 3. ABSTRACT FACTORY — creates families of related objects
//    SCENARIO: UI components — each region needs matching Button + Checkbox
// ============================================================

// Abstract products
public interface IButton  { void Render(); }
public interface ICheckbox { void Render(); }

// Windows family
public class WindowsButton  : IButton   { public void Render() => Console.WriteLine("[Windows Button  ] rendered"); }
public class WindowsCheckbox : ICheckbox { public void Render() => Console.WriteLine("[Windows Checkbox] rendered"); }

// Mac family
public class MacButton   : IButton   { public void Render() => Console.WriteLine("[Mac Button  ] rendered"); }
public class MacCheckbox : ICheckbox { public void Render() => Console.WriteLine("[Mac Checkbox] rendered"); }

// Abstract factory
public interface IUIFactory
{
    IButton CreateButton();
    ICheckbox CreateCheckbox();
}

// Concrete factories — each creates a FAMILY of matching components
public class WindowsUIFactory : IUIFactory
{
    public IButton CreateButton()   => new WindowsButton();
    public ICheckbox CreateCheckbox() => new WindowsCheckbox();
}

public class MacUIFactory : IUIFactory
{
    public IButton CreateButton()   => new MacButton();
    public ICheckbox CreateCheckbox() => new MacCheckbox();
}

// Client uses the factory without knowing which family
public class Application
{
    private readonly IButton _button;
    private readonly ICheckbox _checkbox;

    public Application(IUIFactory factory)
    {
        _button   = factory.CreateButton();
        _checkbox = factory.CreateCheckbox();
    }

    public void RenderUI()
    {
        _button.Render();
        _checkbox.Render();
    }
}

// ============================================================
// 4. FACTORY with DI registration (production pattern)
//    FOLLOW-UP Q: How do you register multiple implementations in DI?
// ============================================================
public interface IPaymentProcessorFactory
{
    IPaymentProcessor GetProcessor(string provider);
}

// Keyed services (.NET 8+): builder.Services.AddKeyedSingleton<IPaymentProcessor, StripeProcessor>("stripe");
// Or use a dictionary factory:
public class PaymentProcessorFactory : IPaymentProcessorFactory
{
    private readonly IServiceProvider _sp;
    public PaymentProcessorFactory(IServiceProvider sp) => _sp = sp;

    // FOLLOW-UP Q: Why resolve from IServiceProvider here instead of injecting directly?
    // Avoids forcing all processors to be instantiated at startup.
    public IPaymentProcessor GetProcessor(string provider) => provider switch
    {
        "stripe"  => _sp.GetRequiredService<StripeProcessor>(),
        "paypal"  => _sp.GetRequiredService<PayPalProcessor>(),
        _ => throw new KeyNotFoundException($"No processor for '{provider}'")
    };
}

// ---------- Demo ----------
public static class FactoryDemo
{
    public static async Task Run()
    {
        Console.WriteLine("=== Factory Pattern Demo ===\n");

        // 1. Simple Factory
        Console.WriteLine("--- Simple Factory ---");
        var p1 = PaymentProcessorSimpleFactory.Create("stripe");
        await p1.ProcessAsync(99.99m, "USD");

        // 2. Factory Method
        Console.WriteLine("\n--- Factory Method ---");
        PaymentService svc = new PayPalPaymentService();
        await svc.ChargeAsync(49.50m, "EUR");

        // 3. Abstract Factory
        Console.WriteLine("\n--- Abstract Factory ---");
        IUIFactory factory = System.Runtime.InteropServices.RuntimeInformation
            .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
            ? new MacUIFactory()
            : new WindowsUIFactory();

        var app = new Application(factory);
        app.RenderUI();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. Factory Method vs Strategy pattern?
//      Factory Method creates objects; Strategy encapsulates algorithms. Often look similar.
//   2. How does IHttpClientFactory use the factory pattern in .NET?
//      Named/typed clients — factory creates HttpClient with pre-configured handlers.
//   3. What is the Builder pattern and how does it differ from Factory?
//      Builder: step-by-step construction of a complex object (SqlBuilder, WebApplicationBuilder).
//      Factory: single-step creation, hides which concrete type is returned.
//   4. Can factories return interfaces and still be testable?
//      YES — inject a mock IPaymentProcessorFactory in tests to return test doubles.
// ============================================================
