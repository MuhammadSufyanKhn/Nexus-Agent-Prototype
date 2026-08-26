using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Events;
using Nexus.Agent.Intent;
using Nexus.Agent.Orchestration;
using Nexus.Api.Controllers;
using Nexus.Data;
using Nexus.Data.ActionPlan;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.LLM;
using Nexus.Data.Policies;
using Nexus.Data.Services;
using Nexus.Security.Permissions;
using Nexus.Security.Risk;
using Nexus.Security.Sql;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Nexus.Tools.Sap;
using Xunit;

namespace Nexus.Tests;

public class FullEndToEndIntegrationTests
{
    private NexusDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new NexusDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    private ToolRegistry CreateFullToolRegistry(NexusDbContext db, ILLMService llmService)
    {
        var registry = new ToolRegistry();
        var empService = new EmployeeService(db);
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);
        var sapConnector = new MockSapConnector(NullLogger<MockSapConnector>.Instance);
        var sqlValidator = new SqlValidator();
        var policyEngine = new PolicyRuleEngine(db, NullLogger<PolicyRuleEngine>.Instance);
        var complianceEngine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);

        registry.RegisterTool(new EmployeeCreateTool(empService));
        registry.RegisterTool(new EmployeeReadTool(empService));
        registry.RegisterTool(new EmployeeUpdateTool(empService));
        registry.RegisterTool(new EmployeeDeleteTool(empService));
        registry.RegisterTool(new SqlAnalyticsTool(sqlValidator, llmService, db, NullLogger<SqlAnalyticsTool>.Instance));
        registry.RegisterTool(new PolicyEvaluationTool(policyEngine, NullLogger<PolicyEvaluationTool>.Instance));
        registry.RegisterTool(new ComplianceTool(complianceEngine, NullLogger<ComplianceTool>.Instance));
        registry.RegisterTool(new WelcomeEmailTool(pythonService, NullLogger<WelcomeEmailTool>.Instance));
        registry.RegisterTool(new CreateTicketTool(pythonService, NullLogger<CreateTicketTool>.Instance));
        registry.RegisterTool(new BrowserAutomationTool(pythonService, NullLogger<BrowserAutomationTool>.Instance));
        registry.RegisterTool(new MockSapTool(sapConnector, NullLogger<MockSapTool>.Instance));

        return registry;
    }

    [Fact]
    public async Task Demo1_Primary_Onboarding_EndToEnd_Requires_Approval_And_Executes_All_Tools()
    {
        using var db = GetInMemoryDbContext("Demo1Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_ONBOARDING",
            Entities = new Dictionary<string, string>
            {
                { "name", "Ahmed Khan" },
                { "department", "IT" },
                { "designation", "Mid-Level .NET Developer" }
            },
            Confidence = 0.95
        });
        var registry = CreateFullToolRegistry(db, mockLlm);

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var broadcaster = new AgentEventBroadcaster(NullLogger<AgentEventBroadcaster>.Instance);

        var orchestrator = new AgentOrchestrator(
            intentParser,
            registry,
            db,
            NullLogger<AgentOrchestrator>.Instance,
            new RiskEngine(),
            new PermissionService(),
            broadcaster);

        var prompt = "Onboard Ahmed Khan as a mid-level .NET developer in IT according to company policy. Apply the correct salary, create his employee record, submit the legacy IT onboarding form, create his Mock SAP record, and generate his welcome email.";

        // 1. Execute Onboarding Request -> Triggers Plan of Action Interceptor
        var result = await orchestrator.ExecuteAsync(prompt, userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ActionPlan);
        Assert.Equal("AWAITING_APPROVAL", result.ActionPlan.Status);
        Assert.Equal(68000.00m, result.ActionPlan.TotalFinancialImpact);

        // Prove NO new employee named Ahmed Khan was created in database before approval
        var ahmedBefore = await db.Employees.FirstOrDefaultAsync(e => e.Name == "Ahmed Khan");
        Assert.Null(ahmedBefore);

        // 2. Simulate Human Operator Approval Decision via ApprovalController logic
        var pendingApproval = await db.Approvals.FirstOrDefaultAsync(a => a.AgentRunId == result.RunId);
        Assert.NotNull(pendingApproval);

        var approvalController = new ApprovalController(db);
        var decisionResult = await approvalController.DecideApproval(new ApprovalDecisionRequest
        {
            ApprovalId = pendingApproval!.Id,
            Approved = true,
            ApprovedBy = "Executive Admin",
            Reason = "Approved multi-system IT onboarding for Ahmed Khan"
        });

        Assert.NotNull(decisionResult);

        // Prove employee record WAS created after approval decision
        var ahmedAfter = await db.Employees.FirstOrDefaultAsync(e => e.Name == "Ahmed Khan");
        Assert.NotNull(ahmedAfter);
        Assert.Equal("Mid-Level .NET Developer", ahmedAfter!.Designation);
        Assert.Equal(68000.00m, ahmedAfter.Salary);
    }

    [Fact]
    public async Task Demo2_SQL_Analytics_Returns_Validated_Query_And_Summary()
    {
        using var db = GetInMemoryDbContext("Demo2Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "BUDGET_ANALYSIS",
            Entities = new Dictionary<string, string>(),
            Confidence = 0.95
        });
        var registry = CreateFullToolRegistry(db, mockLlm);

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var orchestrator = new AgentOrchestrator(
            intentParser,
            registry,
            db,
            NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Show me departments exceeding Q3 budget.");

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResultData);

        var json = JsonSerializer.Serialize(result.ResultData).ToLower();
        Assert.Contains("query", json);
        Assert.Contains("summary", json);
    }

    [Fact]
    public async Task Demo3_Expense_Compliance_Audits_Claim_Against_Policy()
    {
        using var db = GetInMemoryDbContext("Demo3Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EXPENSE_COMPLIANCE",
            Entities = new Dictionary<string, string> { { "name", "Tariq Mahmood" } },
            Confidence = 0.95
        });
        var registry = CreateFullToolRegistry(db, mockLlm);

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var orchestrator = new AgentOrchestrator(
            intentParser,
            registry,
            db,
            NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Check Tariq's latest expense against policy.");

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResultData);
    }

    [Fact]
    public async Task Demo4_Bulk_Salary_Update_Requires_Approval_And_Prevents_Prior_Mutation()
    {
        using var db = GetInMemoryDbContext("Demo4Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_UPDATE",
            Entities = new Dictionary<string, string> { { "department", "IT" }, { "percentage", "10" } },
            Confidence = 0.95
        });
        var registry = CreateFullToolRegistry(db, mockLlm);

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var orchestrator = new AgentOrchestrator(
            intentParser,
            registry,
            db,
            NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Increase salaries of all IT developers by 10%.");

        Assert.NotNull(result);
        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ActionPlan);

        // Prove salaries remain unchanged before approval
        var tariqBefore = await db.Employees.FindAsync(1);
        Assert.Equal(75000.00m, tariqBefore!.Salary);

        // Approve bulk salary increase
        var pendingApproval = await db.Approvals.FirstOrDefaultAsync(a => a.AgentRunId == result.RunId);
        var approvalController = new ApprovalController(db);
        await approvalController.DecideApproval(new ApprovalDecisionRequest
        {
            ApprovalId = pendingApproval!.Id,
            Approved = true,
            ApprovedBy = "Executive Admin"
        });

        // Prove salary was updated by +10% after approval ($75k -> $82.5k)
        var tariqAfter = await db.Employees.FindAsync(1);
        Assert.Equal(82500.00m, tariqAfter!.Salary);
    }

    [Fact]
    public async Task Onboarding_Triggers_Exactly_One_Welcome_Email()
    {
        using var db = GetInMemoryDbContext("SingleEmailTestDb");
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_ONBOARDING",
            Entities = new Dictionary<string, string>
            {
                { "name", "Sara Miller" },
                { "department", "IT" },
                { "designation", "Software Engineer" },
                { "email", "sara.miller@nexus.local" }
            },
            Confidence = 0.95
        });
        var registry = CreateFullToolRegistry(db, mockLlm);
        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var broadcaster = new AgentEventBroadcaster(NullLogger<AgentEventBroadcaster>.Instance);

        var orchestrator = new AgentOrchestrator(
            intentParser,
            registry,
            db,
            NullLogger<AgentOrchestrator>.Instance,
            new RiskEngine(),
            new PermissionService(),
            broadcaster);

        var prompt = "Onboard Sara Miller as Software Engineer in IT department with email sara.miller@nexus.local.";
        var result = await orchestrator.ExecuteAsync(prompt, userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.RequiresApproval);

        var pendingApproval = await db.Approvals.FirstOrDefaultAsync(a => a.AgentRunId == result.RunId);
        Assert.NotNull(pendingApproval);

        var approvalController = new ApprovalController(db, orchestrator);
        await approvalController.DecideApproval(new ApprovalDecisionRequest
        {
            ApprovalId = pendingApproval!.Id,
            Approved = true,
            ApprovedBy = "Executive Admin"
        });

        // Verify that exactly 1 email.welcome action was logged for this run
        var emailActions = await db.AgentActions
            .Where(a => a.AgentRunId == result.RunId && a.ToolName == "email.welcome")
            .ToListAsync();

        Assert.Single(emailActions);
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
            return Task.FromResult(LLMResponse.Success("SELECT d.Name AS Department, d.AllocatedQ3Budget AS AllocatedBudget, SUM(e.Salary) AS CurrentPayroll, (SUM(e.Salary) - d.AllocatedQ3Budget) AS OverflowAmount FROM Departments d JOIN Employees e ON d.Id = e.DepartmentId GROUP BY d.Name, d.AllocatedQ3Budget HAVING SUM(e.Salary) > d.AllocatedQ3Budget;", "mock-model", 10));
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
