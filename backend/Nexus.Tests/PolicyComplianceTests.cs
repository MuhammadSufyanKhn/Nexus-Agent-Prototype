using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.Policies;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class PolicyComplianceTests
{
    private NexusDbContext GetInMemoryDbContext(string dbName, bool addPolicy = true, bool addNonCompliantExpense = true)
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new NexusDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        context.Policies.RemoveRange(context.Policies);
        context.Employees.RemoveRange(context.Employees);
        context.Expenses.RemoveRange(context.Expenses);
        context.SaveChanges();

        if (addPolicy)
        {
            context.Policies.Add(new Policy
            {
                Code = "POL-FIN-002",
                Title = "Expense Reimbursement Policy",
                Category = "Finance",
                DocumentPath = "policies/expense_policy.pdf",
                ContentSummary = "Meal expenses are capped at $50 per person. Software tools require prior approval up to $500.",
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var emp = new Employee
        {
            Id = 10,
            Name = "Ahmed Khan",
            Email = "ahmed.khan@nexus.local",
            DepartmentId = 1,
            Designation = "Mid-Level .NET Developer",
            Salary = 68000.00m,
            ExperienceYears = 3,
            Status = EmployeeStatus.Active
        };
        context.Employees.Add(emp);

        if (addNonCompliantExpense)
        {
            // Non-compliant $350 meal expense vs $50 limit
            context.Expenses.Add(new Expense
            {
                Id = 101,
                EmployeeId = 10,
                ExpenseType = ExpenseType.Meal,
                Amount = 350.00m,
                ExpenseDate = DateTime.UtcNow,
                Status = ExpenseStatus.NonCompliant,
                Description = "Team Dinner (Exceeds $50 limit)"
            });
        }
        else
        {
            // Compliant $150 software expense vs $500 limit
            context.Expenses.Add(new Expense
            {
                Id = 102,
                EmployeeId = 10,
                ExpenseType = ExpenseType.Software,
                Amount = 150.00m,
                ExpenseDate = DateTime.UtcNow,
                Status = ExpenseStatus.Compliant,
                Description = "JetBrains IDE License"
            });
        }

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task PolicyComplianceEngine_Detects_NonCompliant_Meal_Expense()
    {
        using var db = GetInMemoryDbContext("NonCompliantExpenseDb", addPolicy: true, addNonCompliantExpense: true);
        var engine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);

        var result = await engine.EvaluateExpenseComplianceAsync(employeeName: "Ahmed Khan");

        Assert.NotNull(result);
        Assert.Equal(ComplianceStatus.NON_COMPLIANT, result.Status);
        Assert.Equal("NON_COMPLIANT", result.StatusName);
        Assert.Equal(350.00m, result.ClaimedAmount);
        Assert.Equal(50.00m, result.AllowedAmount);
        Assert.Equal(300.00m, result.Difference);
        Assert.Equal("POL-FIN-002: Expense Reimbursement Policy Section 4.1", result.PolicySource);
        Assert.Contains("exceeds the maximum allowed limit", result.Reason);
    }

    [Fact]
    public async Task PolicyComplianceEngine_Validates_Compliant_Software_Expense()
    {
        using var db = GetInMemoryDbContext("CompliantExpenseDb", addPolicy: true, addNonCompliantExpense: false);
        var engine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);

        var result = await engine.EvaluateExpenseComplianceAsync(employeeName: "Ahmed Khan");

        Assert.NotNull(result);
        Assert.Equal(ComplianceStatus.COMPLIANT, result.Status);
        Assert.Equal(150.00m, result.ClaimedAmount);
        Assert.Equal(500.00m, result.AllowedAmount);
        Assert.Equal(0.00m, result.Difference);
        Assert.Contains("complies with the policy limit", result.Reason);
    }

    [Fact]
    public async Task PolicyComplianceEngine_Returns_InsufficientInformation_When_Employee_Not_Found()
    {
        using var db = GetInMemoryDbContext("MissingEmpDb");
        db.Employees.RemoveRange(db.Employees);
        db.SaveChanges();

        var engine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);
        var result = await engine.EvaluateExpenseComplianceAsync(employeeName: "Unknown Employee");

        Assert.Equal(ComplianceStatus.INSUFFICIENT_INFORMATION, result.Status);
        Assert.Contains("Employee profile not found", result.Reason);
    }

    [Fact]
    public async Task PolicyComplianceEngine_Returns_PolicyNotFound_When_Policy_Document_Missing()
    {
        using var db = GetInMemoryDbContext("MissingPolicyDb", addPolicy: false);
        var engine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);

        var result = await engine.EvaluateExpenseComplianceAsync(employeeName: "Ahmed Khan");

        Assert.Equal(ComplianceStatus.POLICY_NOT_FOUND, result.Status);
        Assert.Contains("No expense reimbursement policy document found", result.Reason);
    }

    [Fact]
    public async Task ComplianceTool_Executes_Compliance_Check_Successfully()
    {
        using var db = GetInMemoryDbContext("ComplianceToolDb");
        var engine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);
        var tool = new ComplianceTool(engine, NullLogger<ComplianceTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = @"{ ""employeeName"": ""Ahmed Khan"" }"
        };

        var result = await tool.ExecuteAsync(context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        var complianceRes = result.Data as ComplianceResult;
        Assert.NotNull(complianceRes);
        Assert.Equal("Ahmed Khan", complianceRes!.EmployeeName);
        Assert.Equal(ComplianceStatus.NON_COMPLIANT, complianceRes.Status);
    }
}
