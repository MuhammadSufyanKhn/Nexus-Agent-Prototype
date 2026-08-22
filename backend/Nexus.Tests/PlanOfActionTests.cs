using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Data.LLM;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.ActionPlan;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.Policies;
using Nexus.Data.Services;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class PlanOfActionTests
{
    private NexusDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new NexusDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        context.Employees.RemoveRange(context.Employees);
        context.Employees.AddRange(new[]
        {
            new Employee { Id = 1, Name = "Tariq Mahmood", Email = "tariq@nexus.local", DepartmentId = 1, Designation = "Senior Developer", Salary = 75000.00m, ExperienceYears = 5, Status = EmployeeStatus.Active },
            new Employee { Id = 2, Name = "Sarah Jenkins", Email = "sarah@nexus.local", DepartmentId = 1, Designation = "Lead Architect", Salary = 95000.00m, ExperienceYears = 8, Status = EmployeeStatus.Active },
            new Employee { Id = 5, Name = "Ahmed Khan", Email = "ahmed@nexus.local", DepartmentId = 1, Designation = "Mid Developer", Salary = 68000.00m, ExperienceYears = 3, Status = EmployeeStatus.Active }
        });
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task AgentOrchestrator_Generates_PlanOfAction_And_Proves_Zero_DB_Mutation_Before_Approval()
    {
        using var db = GetInMemoryDbContext("PlanOfActionDb");
        var empService = new EmployeeService(db);

        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeUpdateTool(empService));
        registry.RegisterTool(new EmployeeReadTool(empService));

        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_UPDATE",
            Entities = new Dictionary<string, string> { { "department", "IT" }, { "percentage", "10" } },
            Confidence = 0.95
        });

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var orchestrator = new AgentOrchestrator(intentParser, registry, db, NullLogger<AgentOrchestrator>.Instance);

        // 1. Act: Execute High-Risk Prompt
        var prompt = "Increase salaries of all IT developers by 10%.";
        var result = await orchestrator.ExecuteAsync(prompt);

        // 2. Assert ActionPlan Response
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ActionPlan);

        var plan = result.ActionPlan;
        Assert.Equal("AWAITING_APPROVAL", plan!.Status);
        Assert.Equal(3, plan.AffectedCount);
        Assert.True(plan.TotalFinancialImpact > 0);

        // Assert Change Preview calculations
        var tariqPreview = plan.AffectedRecords.First(r => r.PrimaryLabel == "Tariq Mahmood");
        Assert.Equal("$75,000.00", tariqPreview.Changes[0].OldValue);
        Assert.Equal("$82,500.00", tariqPreview.Changes[0].NewValue);

        // 3. PROVE ZERO DATABASE MUTATION OCCURRED PRIOR TO APPROVAL
        var tariqInDb = await db.Employees.FindAsync(1);
        var sarahInDb = await db.Employees.FindAsync(2);
        var ahmedInDb = await db.Employees.FindAsync(5);

        Assert.Equal(75000.00m, tariqInDb!.Salary); // UNCHANGED
        Assert.Equal(95000.00m, sarahInDb!.Salary); // UNCHANGED
        Assert.Equal(68000.00m, ahmedInDb!.Salary); // UNCHANGED

        // 4. Assert Pending Approval entity in DB
        var approval = await db.Approvals.FirstOrDefaultAsync(a => a.Id == plan.ApprovalId);
        Assert.NotNull(approval);
        Assert.Equal(ApprovalStatus.Pending, approval!.Status);
    }

    [Fact]
    public async Task ApprovalController_Executes_Changes_When_Approved()
    {
        using var db = GetInMemoryDbContext("ApproveDecisionDb");
        var runId = Guid.NewGuid();
        db.AgentRuns.Add(new AgentRun { Id = runId, OriginalPrompt = "Increase salary", Status = AgentRunStatus.WaitingForApproval });
        
        var approvalId = Guid.NewGuid();
        db.Approvals.Add(new Approval
        {
            Id = approvalId,
            AgentRunId = runId,
            RiskLevel = RiskLevel.High,
            RequestedBy = "System",
            Status = ApprovalStatus.Pending,
            Reason = "Plan of Action bulk salary increase"
        });
        db.SaveChanges();

        var controller = new Nexus.Api.Controllers.ApprovalController(db);
        var request = new ApprovalDecisionRequest
        {
            ApprovalId = approvalId,
            Approved = true,
            ApprovedBy = "Executive Admin"
        };

        var actionResult = await controller.DecideApproval(request);
        var okResult = actionResult as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(okResult);

        // Assert Salaries WERE mutated AFTER approval
        var tariqInDb = await db.Employees.FindAsync(1);
        Assert.Equal(82500.00m, tariqInDb!.Salary); // Mutated to $82,500 (+10%)

        var approvalInDb = await db.Approvals.FindAsync(approvalId);
        Assert.Equal(ApprovalStatus.Approved, approvalInDb!.Status);
        Assert.Equal("Executive Admin", approvalInDb.ApprovedBy);
    }

    [Fact]
    public async Task ApprovalController_Cancels_Changes_When_Rejected()
    {
        using var db = GetInMemoryDbContext("RejectDecisionDb");
        var runId = Guid.NewGuid();
        db.AgentRuns.Add(new AgentRun { Id = runId, OriginalPrompt = "Increase salary", Status = AgentRunStatus.WaitingForApproval });

        var approvalId = Guid.NewGuid();
        db.Approvals.Add(new Approval
        {
            Id = approvalId,
            AgentRunId = runId,
            RiskLevel = RiskLevel.High,
            RequestedBy = "System",
            Status = ApprovalStatus.Pending,
            Reason = "Plan of Action bulk salary increase"
        });
        db.SaveChanges();

        var controller = new Nexus.Api.Controllers.ApprovalController(db);
        var request = new ApprovalDecisionRequest
        {
            ApprovalId = approvalId,
            Approved = false,
            ApprovedBy = "Executive Admin",
            Reason = "Budget constraints"
        };

        var actionResult = await controller.DecideApproval(request);
        var okResult = actionResult as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(okResult);

        // Assert Salaries REMAINS UNTOUCHED after rejection
        var tariqInDb = await db.Employees.FindAsync(1);
        Assert.Equal(75000.00m, tariqInDb!.Salary); // UNCHANGED

        var approvalInDb = await db.Approvals.FindAsync(approvalId);
        Assert.Equal(ApprovalStatus.Rejected, approvalInDb!.Status);
    }

    private class MockLLMService : ILLMService
    {
        private readonly object? _responseObject;

        public MockLLMService(object? responseObject)
        {
            _responseObject = responseObject;
        }

        public Task<LLMResponse> GenerateCompletionAsync(string prompt, string? systemPrompt = null, bool requestJson = false, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LLMResponse.Success("{}", "mock-model", 10));
        }

        public Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_responseObject is T typed)
            {
                return Task.FromResult<T?>(typed);
            }
            return Task.FromResult<T?>(default);
        }

        public Task<LLMHealthStatus> CheckHealthAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMHealthStatus { IsAvailable = true });
        }
    }
}
