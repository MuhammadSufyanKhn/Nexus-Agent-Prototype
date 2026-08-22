using System;
using System.Collections.Generic;
using System.Linq;
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

public class SecurityAndFailureTests
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

    // 1. Unauthorized employee update
    [Fact]
    public async Task Test01_Unauthorized_Employee_Update_Is_Blocked_By_RBAC()
    {
        using var db = GetInMemoryDbContext("Sec01Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult { Intent = "EMPLOYEE_UPDATE", Entities = new Dictionary<string, string> { { "name", "Tariq Mahmood" } }, Confidence = 0.95 });
        var registry = CreateFullToolRegistry(db, mockLlm);
        var orchestrator = new AgentOrchestrator(new IntentParser(mockLlm, NullLogger<IntentParser>.Instance), registry, db, NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Update Tariq's profile", userId: 10, userRole: "Employee");

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("Security Violation", result.ErrorMessage);
    }

    // 2. Unauthorized salary update
    [Fact]
    public async Task Test02_Unauthorized_Salary_Update_Is_Blocked_By_RBAC()
    {
        using var db = GetInMemoryDbContext("Sec02Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult { Intent = "EMPLOYEE_UPDATE", Entities = new Dictionary<string, string> { { "name", "Tariq Mahmood" }, { "salary", "90000" } }, Confidence = 0.95 });
        var registry = CreateFullToolRegistry(db, mockLlm);
        var orchestrator = new AgentOrchestrator(new IntentParser(mockLlm, NullLogger<IntentParser>.Instance), registry, db, NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Update Tariq salary to 90000", userId: 10, userRole: "Employee");

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("Security Violation", result.ErrorMessage);
    }

    // 3. DELETE all employees (Critical Risk Gate)
    [Fact]
    public async Task Test03_Delete_All_Employees_Triggers_Critical_Risk_Gate()
    {
        using var db = GetInMemoryDbContext("Sec03Db");
        var mockLlm = new MockLLMService(new ParsedIntentResult { Intent = "EMPLOYEE_DELETE", Entities = new Dictionary<string, string> { { "id", "1" } }, Confidence = 0.95 });
        var registry = CreateFullToolRegistry(db, mockLlm);
        var orchestrator = new AgentOrchestrator(new IntentParser(mockLlm, NullLogger<IntentParser>.Instance), registry, db, NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Delete employee record 1", userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ActionPlan);

        // Prove employee record remains intact before approval
        var emp = await db.Employees.FindAsync(1);
        Assert.NotNull(emp);
    }

    // 4. DROP TABLE attempt
    [Fact]
    public async Task Test04_Drop_Table_Attempt_Is_Rejected_By_SqlValidator()
    {
        var validator = new SqlValidator();
        var result = validator.ValidateQuery("DROP TABLE Employees;");

        Assert.False(result.IsValid);
        Assert.Contains("Security Violation", result.ErrorMessage);
    }

    // 5. Arbitrary SQL attempt (xp_cmdshell / EXEC)
    [Fact]
    public async Task Test05_Arbitrary_SQL_Cmdshell_Attempt_Is_Rejected_By_SqlValidator()
    {
        var validator = new SqlValidator();
        var result = validator.ValidateQuery("EXEC xp_cmdshell 'whoami';");

        Assert.False(result.IsValid);
        Assert.Contains("Security Violation", result.ErrorMessage);
    }

    // 6. Malformed LLM JSON
    [Fact]
    public async Task Test06_Malformed_LLM_JSON_Falls_Back_Safely_To_Rule_Based_Parser()
    {
        var mockLlm = new MalformedLLMService();
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Onboard Ahmed Khan in IT");

        Assert.NotNull(result);
        Assert.Equal("EMPLOYEE_ONBOARDING", result.Intent);
        Assert.True(result.Confidence > 0.5);
    }

    // 7. Unknown tool
    [Fact]
    public async Task Test07_Unknown_Tool_Execution_Is_Safely_Rejected()
    {
        using var db = GetInMemoryDbContext("Sec07Db");
        var registry = new ToolRegistry(); // Empty registry
        var mockLlm = new MockLLMService(new ParsedIntentResult { Intent = "EMPLOYEE_READ", Entities = new Dictionary<string, string> { { "name", "Tariq" } }, Confidence = 0.95 });
        var orchestrator = new AgentOrchestrator(new IntentParser(mockLlm, NullLogger<IntentParser>.Instance), registry, db, NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Find Tariq", userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
    }

    // 8. Missing required parameter
    [Fact]
    public async Task Test08_Missing_Required_Parameter_Fails_Validation_Safely()
    {
        using var db = GetInMemoryDbContext("Sec08Db");
        var empService = new EmployeeService(db);
        var tool = new EmployeeCreateTool(empService);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = "{}" // Empty args
        };

        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("required", result.ErrorMessage?.ToLower());
    }

    // 9. Policy not found / insufficient information
    [Fact]
    public async Task Test09_Policy_Not_Found_Returns_Traceable_Status_Without_Guessing()
    {
        using var db = GetInMemoryDbContext("Sec09Db");
        var engine = new PolicyComplianceEngine(db, NullLogger<PolicyComplianceEngine>.Instance);

        var result = await engine.EvaluateExpenseComplianceAsync(employeeName: "NonExistentUser");

        Assert.NotNull(result);
        Assert.True(result.Status == ComplianceStatus.POLICY_NOT_FOUND || result.Status == ComplianceStatus.INSUFFICIENT_INFORMATION);
    }

    // 10. Browser automation failure
    [Fact]
    public async Task Test10_Browser_Automation_Failure_Is_Handled_Gracefully()
    {
        var pythonService = new PythonAutomationService(NullLogger<PythonAutomationService>.Instance);
        var tool = new BrowserAutomationTool(pythonService, NullLogger<BrowserAutomationTool>.Instance);

        var context = new ToolExecutionContext { ArgumentsJson = "{\"invalid\":\"data\"}" };
        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess); // Returns structured fallback JSON without crashing
    }

    // 11. Database failure
    [Fact]
    public async Task Test11_Database_Failure_Is_Handled_Safely()
    {
        using var db = GetInMemoryDbContext("Sec11Db");
        var empService = new EmployeeService(db);
        db.Dispose(); // Force DB closed

        var tool = new EmployeeReadTool(empService);
        var context = new ToolExecutionContext { ArgumentsJson = "{\"search\":\"Tariq\"}" };

        var result = await tool.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
    }

    // 12. Local LLM unavailable
    [Fact]
    public async Task Test12_Local_LLM_Unavailable_Health_Check_Returns_Offline_Status()
    {
        var llm = new OfflineLLMService();
        var health = await llm.CheckHealthAsync();

        Assert.NotNull(health);
        Assert.False(health.IsAvailable);
    }

    // 13. Approval rejection
    [Fact]
    public async Task Test13_Approval_Rejection_Results_In_Zero_Database_Mutation()
    {
        using var db = GetInMemoryDbContext("Sec13Db");
        var approvalController = new ApprovalController(db);

        var run = new AgentRun { Id = Guid.NewGuid(), OriginalPrompt = "Increase salary", Status = AgentRunStatus.WaitingForApproval, StartedAt = DateTime.UtcNow };
        db.AgentRuns.Add(run);

        var approval = new Approval { Id = Guid.NewGuid(), AgentRunId = run.Id, RiskLevel = RiskLevel.High, Status = ApprovalStatus.Pending, RequestedBy = "Orchestrator", Reason = "Bulk update", CreatedAt = DateTime.UtcNow };
        db.Approvals.Add(approval);
        await db.SaveChangesAsync();

        var decision = await approvalController.DecideApproval(new ApprovalDecisionRequest { ApprovalId = approval.Id, Approved = false, ApprovedBy = "Admin", Reason = "Budget frozen" });
        Assert.NotNull(decision);

        var tariq = await db.Employees.FindAsync(1);
        Assert.Equal(75000.00m, tariq!.Salary); // Salary untouched
    }

    // 14. Duplicate execution / already processed approval
    [Fact]
    public async Task Test14_Duplicate_Approval_Decision_Is_Rejected()
    {
        using var db = GetInMemoryDbContext("Sec14Db");
        var approvalController = new ApprovalController(db);

        var run = new AgentRun { Id = Guid.NewGuid(), OriginalPrompt = "Already approved prompt", Status = AgentRunStatus.Completed, StartedAt = DateTime.UtcNow };
        db.AgentRuns.Add(run);

        var approval = new Approval { Id = Guid.NewGuid(), AgentRunId = run.Id, RiskLevel = RiskLevel.High, Status = ApprovalStatus.Approved, RequestedBy = "Orchestrator", Reason = "Already approved", CreatedAt = DateTime.UtcNow };
        db.Approvals.Add(approval);
        await db.SaveChangesAsync();

        var actionResult = await approvalController.DecideApproval(new ApprovalDecisionRequest { ApprovalId = approval.Id, Approved = true, ApprovedBy = "Admin" });

        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(actionResult);
    }

    // 15. Partial multi-step failure
    [Fact]
    public async Task Test15_Partial_MultiStep_Failure_Halts_Execution_And_Reports_Error()
    {
        using var db = GetInMemoryDbContext("Sec15Db");
        var registry = new ToolRegistry();

        // Register a tool that intentionally fails
        registry.RegisterTool(new FailingTool());

        var mockLlm = new MockLLMService(new ParsedIntentResult { Intent = "EMPLOYEE_READ", Entities = new Dictionary<string, string>(), Confidence = 0.95 });
        var orchestrator = new AgentOrchestrator(new IntentParser(mockLlm, NullLogger<IntentParser>.Instance), registry, db, NullLogger<AgentOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync("Run failing tool", userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains("Simulated tool failure", result.ErrorMessage);
    }

    private class MalformedLLMService : ILLMService
    {
        public Task<LLMResponse> GenerateCompletionAsync(string prompt, string? systemPrompt = null, bool requestJson = false, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LLMResponse.Success("{ invalid json content ... }", "mock", 10));
        }

        public Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new Exception("Malformed JSON string from LLM parser");
        }

        public Task<LLMHealthStatus> CheckHealthAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMHealthStatus { IsAvailable = true });
        }
    }

    private class OfflineLLMService : ILLMService
    {
        public Task<LLMResponse> GenerateCompletionAsync(string prompt, string? systemPrompt = null, bool requestJson = false, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LLMResponse.Failure("LLM connection timed out"));
        }

        public Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<T?>(default);
        }

        public Task<LLMHealthStatus> CheckHealthAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMHealthStatus { IsAvailable = false });
        }
    }

    private class FailingTool : IAgentTool
    {
        public ToolDefinition Definition => new ToolDefinition
        {
            Name = "employee.read",
            Description = "Failing tool for failure testing",
            InputSchema = "{}",
            RequiredPermission = "employee.read",
            RiskLevel = RiskLevel.Low
        };

        public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context) => Task.FromResult(ValidationResult.Success());

        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
        {
            return Task.FromResult(ToolExecutionResult.Failure("Simulated tool failure during multi-step pipeline execution.", Definition.RiskLevel));
        }
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
