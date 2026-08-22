using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.ActionPlan;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.LLM;
using Nexus.Data.Services;
using Nexus.Security.Permissions;
using Nexus.Security.Risk;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class RiskAndApprovalTests
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
            new Employee { Id = 2, Name = "Sarah Jenkins", Email = "sarah@nexus.local", DepartmentId = 1, Designation = "Lead Architect", Salary = 95000.00m, ExperienceYears = 8, Status = EmployeeStatus.Active }
        });
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task LowRiskAction_ExecutesAutomatically_WithoutApproval()
    {
        using var db = GetInMemoryDbContext("LowRiskDb");
        var empService = new EmployeeService(db);
        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeReadTool(empService));

        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_READ",
            Entities = new Dictionary<string, string> { { "name", "Tariq" } },
            Confidence = 0.98
        });

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var riskEngine = new RiskEngine();
        var permissionService = new PermissionService();
        var orchestrator = new AgentOrchestrator(intentParser, registry, db, NullLogger<AgentOrchestrator>.Instance, riskEngine, permissionService);

        var result = await orchestrator.ExecuteAsync("Show employee Tariq", userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.False(result.RequiresApproval);
        Assert.NotNull(result.ResultData);
    }

    [Fact]
    public async Task HighRiskAction_Triggers_PlanOfAction_AwaitingApproval()
    {
        using var db = GetInMemoryDbContext("HighRiskDb");
        var empService = new EmployeeService(db);
        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeUpdateTool(empService));

        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_UPDATE",
            Entities = new Dictionary<string, string> { { "department", "IT" }, { "percentage", "10" } },
            Confidence = 0.95
        });

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var riskEngine = new RiskEngine();
        var permissionService = new PermissionService();
        var orchestrator = new AgentOrchestrator(intentParser, registry, db, NullLogger<AgentOrchestrator>.Instance, riskEngine, permissionService);

        var result = await orchestrator.ExecuteAsync("Increase salaries of all IT developers by 10%.", userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ActionPlan);
        Assert.Equal("AWAITING_APPROVAL", result.ActionPlan!.Status);
        Assert.Equal(RiskLevel.High, result.ActionPlan.RiskLevel);
    }

    [Fact]
    public async Task Rejection_Of_HighRiskAction_Cancels_Execution_With_Zero_DB_Changes()
    {
        using var db = GetInMemoryDbContext("RejectionDb");
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
            Reason = "Salary increase approval"
        });
        db.SaveChanges();

        var controller = new Nexus.Api.Controllers.ApprovalController(db);
        var request = new ApprovalDecisionRequest
        {
            ApprovalId = approvalId,
            Approved = false,
            ApprovedBy = "Compliance Officer",
            Reason = "Salary increase rejected"
        };

        var actionResult = await controller.DecideApproval(request);
        Assert.NotNull(actionResult as Microsoft.AspNetCore.Mvc.OkObjectResult);

        var tariq = await db.Employees.FindAsync(1);
        Assert.Equal(75000.00m, tariq!.Salary); // ZERO MUTATION

        var approvalInDb = await db.Approvals.FindAsync(approvalId);
        Assert.Equal(ApprovalStatus.Rejected, approvalInDb!.Status);
    }

    [Fact]
    public async Task UnauthorizedUser_IsBlocked_With_SecurityViolation()
    {
        using var db = GetInMemoryDbContext("UnauthorizedDb");
        var empService = new EmployeeService(db);
        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeDeleteTool(empService));

        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_DELETE",
            Entities = new Dictionary<string, string> { { "id", "1" } },
            Confidence = 0.95
        });

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var riskEngine = new RiskEngine();
        var permissionService = new PermissionService();
        var orchestrator = new AgentOrchestrator(intentParser, registry, db, NullLogger<AgentOrchestrator>.Instance, riskEngine, permissionService);

        // Execute prompt with "UnauthorizedUser" role
        var result = await orchestrator.ExecuteAsync("Delete employee ID 1", userId: 99, userRole: "UnauthorizedUser");

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("Security Violation", result.ErrorMessage);
    }

    [Fact]
    public async Task CriticalAction_EvaluatesToCriticalRiskLevel()
    {
        var riskEngine = new RiskEngine();
        var riskLevel = riskEngine.EvaluateToolRisk("employee.delete", "{\"id\": 1, \"action\": \"delete all\"}");

        Assert.Equal(RiskLevel.Critical, riskLevel);
        Assert.True(riskEngine.RequiresApproval(riskLevel, "Admin"));
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
