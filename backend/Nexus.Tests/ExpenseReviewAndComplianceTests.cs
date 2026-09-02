using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.Policies;
using Nexus.Data.Services;
using Nexus.Security.Permissions;
using Nexus.Security.Risk;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class ExpenseReviewAndComplianceTests
{
    private readonly IIntentParser _parser;

    public ExpenseReviewAndComplianceTests()
    {
        _parser = new IntentParser(null, NullLogger<IntentParser>.Instance);
    }

    private NexusDbContext GetInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;

        var db = new NexusDbContext(options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        db.Expenses.RemoveRange(db.Expenses);
        db.Employees.RemoveRange(db.Employees);
        db.SaveChanges();

        // Seed Sarah Ahmed
        db.Employees.Add(new Employee
        {
            Id = 1,
            Name = "Sarah Ahmed",
            Email = "sarah.ahmed@nexus.local",
            DepartmentId = 1,
            Designation = "Operations Consultant",
            Salary = 75000.00m,
            Status = EmployeeStatus.Active
        });

        // Seed Tariq Mahmood
        db.Employees.Add(new Employee
        {
            Id = 2,
            Name = "Tariq Mahmood",
            Email = "tariq.m@nexus.local",
            DepartmentId = 1,
            Designation = "Staff Engineer",
            Salary = 95000.00m,
            Status = EmployeeStatus.Active
        });

        // Seed test claims
        db.Expenses.AddRange(
            new Expense
            {
                Id = 1,
                ClaimNumber = "EXP-4012",
                EmployeeId = 1,
                ExpenseType = ExpenseType.Travel,
                Category = "Travel",
                Amount = 220.00m,
                ExpenseDate = DateTime.UtcNow,
                Status = ExpenseStatus.Pending,
                ComplianceStatus = "Compliant",
                PolicyLimit = 250.00m,
                Variance = -30.00m,
                Description = "Travel reimbursement for client site visit"
            },
            new Expense
            {
                Id = 2,
                ClaimNumber = "EXP-3089",
                EmployeeId = 1,
                ExpenseType = ExpenseType.Travel,
                Category = "Travel",
                Amount = 320.00m,
                ExpenseDate = DateTime.UtcNow,
                Status = ExpenseStatus.Pending,
                ComplianceStatus = "Flagged",
                PolicyLimit = 250.00m,
                Variance = 70.00m,
                FlagReason = "Exceeds $250 travel limit",
                Description = "Flight to client HQ"
            },
            new Expense
            {
                Id = 3,
                ClaimNumber = "EXP-5104",
                EmployeeId = 2,
                ExpenseType = ExpenseType.Equipment,
                Category = "Equipment",
                Amount = 850.00m,
                ExpenseDate = DateTime.UtcNow,
                Status = ExpenseStatus.Pending,
                ComplianceStatus = "NonCompliant",
                PolicyLimit = 500.00m,
                Variance = 350.00m,
                Description = "Standing desk and monitor arms"
            },
            new Expense
            {
                Id = 4,
                ClaimNumber = "EXP-2041",
                EmployeeId = 2,
                ExpenseType = ExpenseType.Meal,
                Category = "Meal",
                Amount = 75.00m,
                ExpenseDate = DateTime.UtcNow,
                Status = ExpenseStatus.Pending,
                ComplianceStatus = "Flagged",
                PolicyLimit = 50.00m,
                Variance = 25.00m,
                Description = "Executive dinner"
            }
        );

        db.SaveChanges();
        return db;
    }

    [Theory]
    [InlineData("Run AI Policy Compliance Sweep on all submitted employee expense claims.", "EXPENSE_COMPLIANCE_SWEEP")]
    [InlineData("Filter expense claims by Policy Violation (Flagged) status.", "EXPENSE_FILTER")]
    [InlineData("Submit meal expense claim of $65.00 for Client Dinner under Sarah Ahmed.", "EXPENSE_CREATE")]
    [InlineData("Show expense claims exceeding the $50 daily meal limit policy.", "EXPENSE_READ")]
    [InlineData("Approve expense claim #EXP-4012 for $220.00 travel reimbursement.", "EXPENSE_APPROVE")]
    [InlineData("Reject non-compliant equipment expense claim for $850.00.", "EXPENSE_REJECT")]
    [InlineData("Show total employee expense reimbursement amount submitted this month.", "EXPENSE_ANALYTICS")]
    [InlineData("Flag expense claim #EXP-3089 for manager policy review.", "EXPENSE_FLAG")]
    [InlineData("Display expense claims breakdown by category: Meal, Travel, Equipment, Software.", "EXPENSE_CATEGORY_ANALYTICS")]
    [InlineData("Calculate policy limit variance for all flagged travel claims.", "EXPENSE_POLICY_VARIANCE")]
    public async Task ParseIntent_All10ExpenseCommands_MapAccurately(string prompt, string expectedIntent)
    {
        var parsed = await _parser.ParseIntentAsync(prompt);
        Assert.Equal(expectedIntent, parsed.Intent);
        Assert.Equal("EXPENSE", parsed.TargetEntity);
    }

    [Fact]
    public async Task ExpenseService_CreateClaim_SarahAhmed_CalculatesLimitAndVariance()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_CreateClaim_SarahAhmed_CalculatesLimitAndVariance));
        var service = new ExpenseService(db);

        var created = await service.CreateAsync(new CreateExpenseDto
        {
            EmployeeName = "Sarah Ahmed",
            Category = "Meal",
            Amount = 65.00m,
            Description = "Client Dinner"
        });

        Assert.NotNull(created);
        Assert.StartsWith("EXP-", created.ClaimNumber);
        Assert.Equal("Sarah Ahmed", created.EmployeeName);
        Assert.Equal(65.00m, created.Amount);
        Assert.Equal(50.00m, created.PolicyLimit);
        Assert.Equal(15.00m, created.Variance);
        Assert.Equal("Flagged", created.ComplianceStatus);

        // Verify persistence in DB
        var persisted = await db.Expenses.FirstOrDefaultAsync(e => e.ClaimNumber == created.ClaimNumber);
        Assert.NotNull(persisted);
        Assert.Equal(65.00m, persisted.Amount);
    }

    [Fact]
    public async Task ExpenseService_CreateClaim_NonExistentEmployee_ThrowsArgumentException()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_CreateClaim_NonExistentEmployee_ThrowsArgumentException));
        var service = new ExpenseService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new CreateExpenseDto
        {
            EmployeeName = "NonExistent Person",
            Category = "Meal",
            Amount = 65.00m
        }));

        Assert.Contains("NonExistent Person", ex.Message);
        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task ExpenseService_ApproveClaim_EXP4012_Succeeds()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_ApproveClaim_EXP4012_Succeeds));
        var service = new ExpenseService(db);

        var approved = await service.ApproveClaimAsync("EXP-4012", 220.00m, "Executive Admin");
        Assert.NotNull(approved);
        Assert.Equal(ExpenseStatus.Approved, approved.Status);
        Assert.Equal("Executive Admin", approved.ReviewedBy);

        var inDb = await db.Expenses.FirstAsync(e => e.ClaimNumber == "EXP-4012");
        Assert.Equal(ExpenseStatus.Approved, inDb.Status);
    }

    [Fact]
    public async Task ExpenseService_RejectClaim_Equipment850_Succeeds()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_RejectClaim_Equipment850_Succeeds));
        var service = new ExpenseService(db);

        var rejected = await service.RejectClaimAsync(null, 850.00m, "Equipment", "Non-compliant with corporate expense policy", "Executive Admin");
        Assert.NotNull(rejected);
        Assert.Equal("EXP-5104", rejected.ClaimNumber);
        Assert.Equal(ExpenseStatus.Rejected, rejected.Status);

        var inDb = await db.Expenses.FirstAsync(e => e.ClaimNumber == "EXP-5104");
        Assert.Equal(ExpenseStatus.Rejected, inDb.Status);
    }

    [Fact]
    public async Task ExpenseService_FlagClaim_EXP3089_Succeeds()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_FlagClaim_EXP3089_Succeeds));
        var service = new ExpenseService(db);

        var flagged = await service.FlagClaimAsync("EXP-3089", "Manager Policy Review Required", "Admin");
        Assert.NotNull(flagged);
        Assert.Equal("Flagged", flagged.ComplianceStatus);
        Assert.Equal("Manager Policy Review Required", flagged.FlagReason);

        var inDb = await db.Expenses.FirstAsync(e => e.ClaimNumber == "EXP-3089");
        Assert.Equal("Flagged", inDb.ComplianceStatus);
    }

    [Fact]
    public async Task ExpenseService_RunComplianceSweep_AuditsAllClaims()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_RunComplianceSweep_AuditsAllClaims));
        var service = new ExpenseService(db);

        var sweepResult = await service.RunComplianceSweepAsync();
        Assert.Equal(4, sweepResult.ClaimsReviewed);
        Assert.Equal(1, sweepResult.CompliantClaims); // EXP-4012
        Assert.Equal(3, sweepResult.FlaggedClaimsCount); // EXP-3089 (+70), EXP-5104 (+350), EXP-2041 (+25)
        Assert.Equal(445.00m, sweepResult.TotalPolicyVariance);
    }

    [Fact]
    public async Task ExpenseService_GetTravelPolicyVariance_CalculatesCorrectVariance()
    {
        using var db = GetInMemoryDb(nameof(ExpenseService_GetTravelPolicyVariance_CalculatesCorrectVariance));
        var service = new ExpenseService(db);

        var varianceResult = await service.GetPolicyVarianceAsync("Travel");
        Assert.Equal(1, varianceResult.FlaggedCount); // EXP-3089
        Assert.Equal(70.00m, varianceResult.TotalVariance); // 320 - 250 = 70
    }
}
