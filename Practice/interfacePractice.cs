// Online C# Editor for free
// Write, Edit and Run your C# code using C# Online Compiler

using System;
using System.Threading.Tasks; // Needed for Task

// Contract / interface
public interface IPaymentService
{
    Task<bool> ProcessPaymentAsync(decimal amount);
}

// Stripe implementation
public class StripePayment : IPaymentService
{
    public async Task<bool> ProcessPaymentAsync(decimal amount)
    {
        // ISSUE:
        // Console.WriteLine("Start Stripe " + {amount});
        // {amount} is invalid outside string interpolation

        // FIX:
        Console.WriteLine($"Start Stripe {amount}");

        // Simulate async work
        await Task.Delay(100);

        return true;
    }
}

// PayPal implementation
public class PayPalPayment : IPaymentService
{
    public async Task<bool> ProcessPaymentAsync(decimal amount)
    {
        // Same issue here with string formatting
        Console.WriteLine($"Start PayPal {amount}");

        await Task.Delay(100);

        return true;
    }
}

public class HelloWorld
{
    // ISSUE:
    // Main should be async because you're calling async methods

    public static async Task Main(string[] args)
    {
        // Better practice:
        // Use interface reference instead of concrete class
        IPaymentService payObj = new StripePayment();

        // ISSUE:
        // Method name typo:
        // ProcessPaymentAysnc -> wrong spelling

        // FIX:
        bool result = await payObj.ProcessPaymentAsync(50);

        Console.WriteLine($"Payment Success: {result}");
    }
}