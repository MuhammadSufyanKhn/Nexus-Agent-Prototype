using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Data.Policies;
using Nexus.Data;
using Nexus.Data.Entities;
using Xunit;

namespace Nexus.Tests;

public class PolicyRuleEngineTests
{
    private NexusDbContext GetInMemoryDbContext(string dbName, bool addHrPolicy = true, bool addConflict = false)
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new NexusDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        context.Policies.RemoveRange(context.Policies);
        context.SaveChanges();

        if (addHrPolicy)
        {
            context.Policies.Add(new Policy
            {
                Code = "POL-HR-001",
                Title = "IT Salary Band Policy",
                Category = "HR",
                DocumentPath = "policies/hr_policy.pdf",
                ContentSummary = "Section 3.2: Mid-Level .NET Developer salary bands range between $60,000 and $75,000.",
                IsActive = true,
                UpdatedAt = System.DateTime.UtcNow
            });
        }

        if (addConflict)
        {
            context.Policies.Add(new Policy
            {
                Code = "POL-HR-009",
                Title = "Conflicting Regional Directive",
                Category = "HR",
                DocumentPath = "policies/hr_conflict_policy.pdf",
                ContentSummary = "Section 1.1: Conflict override directive for salary caps.",
                IsActive = true,
                UpdatedAt = System.DateTime.UtcNow
            });
        }

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task PolicyRuleEngine_Evaluates_Valid_Onboarding_Policy()
    {
        using var db = GetInMemoryDbContext("ValidPolicyDb");
        var engine = new PolicyRuleEngine(db, NullLogger<PolicyRuleEngine>.Instance);

        var context = new PolicyEvaluationContext
        {
            Role = "Mid-Level .NET Developer",
            Department = "IT",
            ExperienceYears = 3
        };

        var result = await engine.EvaluateOnboardingPolicyAsync(context);

        Assert.NotNull(result);
        Assert.Equal(PolicyDecisionStatus.Approved, result.Decision);
        Assert.Equal(68000.00m, result.ProposedValue);
        Assert.Equal("POL-HR-001", result.PolicyCode);
        Assert.Contains("Section 3.2", result.PolicyEvidence);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task PolicyRuleEngine_Returns_InsufficientInfo_When_No_Policy_Exists()
    {
        using var db = GetInMemoryDbContext("NoPolicyDb", addHrPolicy: false);
        var engine = new PolicyRuleEngine(db, NullLogger<PolicyRuleEngine>.Instance);

        var context = new PolicyEvaluationContext
        {
            Role = "Mid-Level .NET Developer",
            Department = "IT"
        };

        var result = await engine.EvaluateOnboardingPolicyAsync(context);

        Assert.Equal(PolicyDecisionStatus.InsufficientInformation, result.Decision);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Missing policy", result.Warnings[0]);
    }

    [Fact]
    public async Task PolicyRuleEngine_Detects_Conflicting_Policy_Rules()
    {
        using var db = GetInMemoryDbContext("ConflictPolicyDb", addHrPolicy: true, addConflict: true);
        var engine = new PolicyRuleEngine(db, NullLogger<PolicyRuleEngine>.Instance);

        var context = new PolicyEvaluationContext
        {
            Role = "Mid-Level .NET Developer",
            Department = "IT"
        };

        var result = await engine.EvaluateOnboardingPolicyAsync(context);

        Assert.Equal(PolicyDecisionStatus.ConflictDetected, result.Decision);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("conflicts", result.Warnings[0], System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PolicyRuleEngine_Flags_Missing_Employee_Information()
    {
        using var db = GetInMemoryDbContext("MissingInfoDb");
        var engine = new PolicyRuleEngine(db, NullLogger<PolicyRuleEngine>.Instance);

        var context = new PolicyEvaluationContext
        {
            Role = "", // Empty role
            Department = "IT"
        };

        var result = await engine.EvaluateOnboardingPolicyAsync(context);

        Assert.Equal(PolicyDecisionStatus.InsufficientInformation, result.Decision);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Missing required candidate role", result.Warnings[0]);
    }

    [Fact]
    public async Task PolicyRuleEngine_Flags_Salary_Outside_Allowed_Range()
    {
        using var db = GetInMemoryDbContext("ExceedSalaryDb");
        var engine = new PolicyRuleEngine(db, NullLogger<PolicyRuleEngine>.Instance);

        var context = new PolicyEvaluationContext
        {
            Role = "Mid-Level .NET Developer",
            Department = "IT",
            RequestedSalary = 85000.00m // Exceeds $75,000 max ceiling
        };

        var result = await engine.EvaluateOnboardingPolicyAsync(context);

        Assert.Equal(PolicyDecisionStatus.Flagged, result.Decision);
        Assert.Equal(75000.00m, result.ProposedValue);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("exceeds allowed band", result.Warnings[0]);
    }
}
