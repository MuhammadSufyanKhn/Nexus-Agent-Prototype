using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Data.LLM;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;
using Nexus.Data.Services;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class AgentOrchestratorTests
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

    [Fact]
    public async Task AgentOrchestrator_Executes_End_To_End_Flow_For_Ahmed_Khan_Creation()
    {
        // 1. Arrange DB and Services
        using var db = GetInMemoryDbContext("AgentOrchestratorDb");
        var empService = new EmployeeService(db);

        // ToolRegistry
        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeCreateTool(empService));
        registry.RegisterTool(new EmployeeReadTool(empService));

        // Mock LLM returning parsed intent for Ahmed Khan
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_CREATE",
            Entities = new Dictionary<string, string>
            {
                { "name", "Ahmed Khan" },
                { "department", "IT" },
                { "designation", "Mid-Level .NET Developer" }
            },
            Confidence = 0.98,
            RequiresClarification = false
        });

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var orchestrator = new AgentOrchestrator(intentParser, registry, db, NullLogger<AgentOrchestrator>.Instance);

        // 2. Act
        var prompt = "Create Ahmed Khan as an IT developer.";
        var result = await orchestrator.ExecuteAsync(prompt);

        // 3. Assert Result Payload
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, result.ErrorMessage ?? "Result is not success");
        Assert.Equal("EMPLOYEE_CREATE", result.Intent);
        Assert.Equal(prompt, result.OriginalPrompt);
        Assert.NotNull(result.ResultData);

        var createdEmp = result.ResultData as EmployeeDto;
        Assert.NotNull(createdEmp);
        Assert.Equal("Ahmed Khan", createdEmp!.Name);
        Assert.Equal("IT", createdEmp.DepartmentName);

        // Assert Execution Feed Trace Events
        Assert.NotEmpty(result.ExecutionFeed);
        Assert.Equal("REQUEST_RECEIVED", result.ExecutionFeed[0].EventType);
        Assert.Equal("INTENT_PARSED", result.ExecutionFeed[1].EventType);
        Assert.Equal("TOOL_SELECTED", result.ExecutionFeed[2].EventType);
        Assert.Equal("TOOL_STARTED", result.ExecutionFeed[3].EventType);
        Assert.Equal("TOOL_COMPLETED", result.ExecutionFeed[4].EventType);
        Assert.Equal("EXECUTION_COMPLETED", result.ExecutionFeed[5].EventType);

        // 4. Assert Database Persistence
        var agentRun = await db.AgentRuns.Include(r => r.Actions).FirstOrDefaultAsync(r => r.Id == result.RunId);
        Assert.NotNull(agentRun);
        Assert.Equal(AgentRunStatus.Completed, agentRun!.Status);
        Assert.Equal("EMPLOYEE_CREATE", agentRun.Intent);
        Assert.Single(agentRun.Actions);

        var action = agentRun.Actions.First();
        Assert.Equal("employee.create", action.ToolName);
        Assert.Equal(AgentActionStatus.Completed, action.Status);

        // Assert Employee in DB
        var empInDb = await db.Employees.FirstOrDefaultAsync(e => e.Name == "Ahmed Khan");
        Assert.NotNull(empInDb);

        // Assert AuditLog recorded
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.AgentRunId == result.RunId);
        Assert.NotNull(audit);
        Assert.Equal("employee.create", audit!.ToolName);
        Assert.NotNull(audit.CurrentHash);
    }

    private class MockLLMService : ILLMService
    {
        private readonly object? _responseObject;

        public MockLLMService(object? responseObject)
        {
            _responseObject = responseObject;
        }

        public Task<LLMResponse> GenerateCompletionAsync(string prompt, string? systemPrompt = null, bool requestJson = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LLMResponse.Success("{}", "mock-model", 10));
        }

        public Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default)
        {
            if (_responseObject is T typed)
            {
                return Task.FromResult<T?>(typed);
            }
            return Task.FromResult<T?>(default);
        }

        public Task<LLMHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMHealthStatus { IsAvailable = true });
        }
    }
}
