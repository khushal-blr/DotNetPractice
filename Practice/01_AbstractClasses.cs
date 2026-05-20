// ============================================================
// TOPIC: Abstract Classes
// ============================================================
// KEY INTERVIEW QUESTIONS:
//   Q: Difference between abstract class and interface?
//      - Abstract class: single inheritance, can have fields, constructors, state,
//        concrete methods. Interface: multiple inheritance, no state (pre-C#8), contract only.
//   Q: When do you pick abstract class over interface?
//      - When classes SHARE code/state (not just a contract). e.g. base validation logic.
//   Q: Can you instantiate an abstract class? NO.
//   Q: Difference between abstract method vs virtual method?
//      - abstract: no body, MUST override. virtual: has body, CAN override.
//   Q: Can abstract class have constructor? YES — called via base() from derived class.
//   Q: What is the Template Method pattern?
//      - Define algorithm skeleton in abstract base; let subclasses fill in steps.
// ============================================================

namespace Practice.AbstractClasses;

// ---------- 1. Basic Abstract Class ----------

public abstract class Employee
{
    protected string Department;

    // Constructor — abstract class CAN have constructor
    protected Employee(string department)
    {
        Department = department;
    }

    // Abstract: no body, subclass MUST override
    public abstract decimal CalculateMonthlySalary();
    public abstract decimal CalculateTax();

    // Virtual: has body, subclass CAN override
    public virtual string GetPaySummary()
    {
        return $"[{Department}] salary={CalculateMonthlySalary():C} | tax={CalculateTax():C}";
    }

    // Concrete: cannot be overridden further
    public void PrintPaySlip() => Console.WriteLine(GetPaySummary());
}

public class FullTimeEmployee : Employee
{
    private readonly string _name;
    private readonly decimal _annualSalary;

    public FullTimeEmployee(string name, decimal annualSalary, string department)
        : base(department)
    {
        _name = name;
        _annualSalary = annualSalary;
    }

    public override decimal CalculateMonthlySalary() => _annualSalary / 12;

    // 30% income tax bracket (simplified)
    public override decimal CalculateTax() => CalculateMonthlySalary() * 0.30m;

    // Override virtual to add employee name
    public override string GetPaySummary() => $"{_name} (FTE) | " + base.GetPaySummary();
}

public class ContractEmployee : Employee
{
    private readonly string _name;
    private readonly decimal _hourlyRate;
    private readonly int _hoursWorked;

    public ContractEmployee(string name, decimal hourlyRate, int hoursWorked, string department)
        : base(department)
    {
        _name = name;
        _hourlyRate = hourlyRate;
        _hoursWorked = hoursWorked;
    }

    public override decimal CalculateMonthlySalary() => _hourlyRate * _hoursWorked;

    // Contractors taxed at flat 25% (no employer benefits)
    public override decimal CalculateTax() => CalculateMonthlySalary() * 0.25m;

    public override string GetPaySummary() => $"{_name} (Contract) | " + base.GetPaySummary();
}

// ---------- 2. Template Method Pattern ----------
// FOLLOW-UP Q: How does Template Method enforce the algorithm order?
// The base class calls steps in a fixed sequence — subclasses can't reorder them.

public abstract class DataIngestionPipeline
{
    // Template method — non-virtual so subclasses cannot override the order
    // (sealed only works on overrides; non-virtual achieves the same goal here)
    public void Run()
    {
        FetchData();
        ValidateData();     // hook — has default, subclass may override
        TransformData();
        Persist();
        Console.WriteLine($"[{DateTime.UtcNow:u}] Ingestion pipeline complete.\n");
    }

    protected abstract void FetchData();
    protected abstract void TransformData();
    protected abstract void Persist();

    // Hook method: optional override
    protected virtual void ValidateData()
    {
        Console.WriteLine("Default validation: checking for null/empty records.");
    }
}

public class SqlIngestionPipeline : DataIngestionPipeline
{
    protected override void FetchData()       => Console.WriteLine("Fetching rows from SQL Server (dbo.Orders)...");
    protected override void TransformData()   => Console.WriteLine("Normalizing columns and applying business rules...");
    protected override void Persist()         => Console.WriteLine("Upserting into data warehouse (dbo.Orders_Staging).");

    // Override hook for stricter validation
    protected override void ValidateData()
    {
        Console.WriteLine("SQL validation: checking referential integrity and row counts.");
    }
}

public class ApiIngestionPipeline : DataIngestionPipeline
{
    protected override void FetchData()     => Console.WriteLine("Calling REST API: GET /v2/events?since=last_run...");
    protected override void TransformData() => Console.WriteLine("Deserializing JSON and mapping to domain model...");
    protected override void Persist()       => Console.WriteLine("Writing records to Azure Blob Storage (bronze layer).");
    // Uses default ValidateData hook
}

// ---------- 3. Demo ----------

public static class AbstractClassDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Abstract Class Demo ===\n");

        // Employee[] shows polymorphism — each override dispatches to the correct subclass
        Employee[] employees =
        {
            new FullTimeEmployee("Alice Chen",    120_000m, "Engineering"),
            new ContractEmployee("Bob Patel", 85m, 160, "Data"),
            new FullTimeEmployee("Carol Smith",    95_000m, "Product"),
        };

        foreach (var e in employees)
            e.PrintPaySlip();

        decimal totalPayroll = 0;
        foreach (var e in employees) totalPayroll += e.CalculateMonthlySalary();
        Console.WriteLine($"\nTotal monthly payroll: {totalPayroll:C}");

        Console.WriteLine("\n--- Template Method Pattern ---");
        DataIngestionPipeline sql = new SqlIngestionPipeline();
        sql.Run();

        DataIngestionPipeline api = new ApiIngestionPipeline();
        api.Run();
    }
}

// ============================================================
// FOLLOW-UP QUESTIONS TO THINK THROUGH:
//   1. If I add a new method to an abstract class, all derived classes still compile.
//      If I add a method to an interface, all implementors BREAK. Why?
//      (Answer: abstract class can provide a default body; interfaces force all implementors to add the method — use default interface methods in C#8+ to mitigate)
//   2. Why seal the Run() template method?
//      Prevents subclasses from breaking the algorithm invariant.
//   3. Can an abstract class implement an interface? YES — and it can leave
//      some interface methods abstract for subclasses to implement.
//   4. What is the "diamond problem" and why doesn't C# have it with classes?
//      C# allows only single class inheritance. Multiple interface inheritance is
//      allowed; default interface methods can cause ambiguity, resolved by explicit cast.
// ============================================================
