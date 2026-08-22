using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Events;
using Nexus.Agent.Intent;
using Nexus.Agent.Orchestration;
using Nexus.Data;
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

public class AgentActivityFeedTests
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
        context.Employees.Add(new Employee
        {
            Id = 1,
            Name = "Ahmed Khan",
            Email = "ahmed.khan@nexus.local",
            DepartmentId = 1,
            Designation = "Developer",
            Salary = 68000.00m,
            ExperienceYears = 3,
            Status = EmployeeStatus.Active
        });
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task AgentOrchestrator_Produces_Activity_Events_During_Run()
    {
        using var db = GetInMemoryDbContext("EventProductionDb");
        var empService = new EmployeeService(db);
        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeReadTool(empService));

        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_READ",
            Entities = new Dictionary<string, string> { { "name", "Ahmed" } },
            Confidence = 0.95
        });

        var intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var riskEngine = new RiskEngine();
        var permissionService = new PermissionService();
        var broadcaster = new AgentEventBroadcaster(NullLogger<AgentEventBroadcaster>.Instance);

        var orchestrator = new AgentOrchestrator(
            intentParser,
            registry,
            db,
            NullLogger<AgentOrchestrator>.Instance,
            riskEngine,
            permissionService,
            broadcaster);

        var result = await orchestrator.ExecuteAsync("Find employee Ahmed", userId: 1, userRole: "Admin");

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        var events = (await broadcaster.GetRunEventsAsync(result.RunId)).ToList();
        Assert.NotEmpty(events);

        var eventTypes = events.Select(e => e.EventType).ToList();
        Assert.Contains("REQUEST_RECEIVED", eventTypes);
        Assert.Contains("INTENT_PARSED", eventTypes);
        Assert.Contains("PLAN_GENERATED", eventTypes);
        Assert.Contains("TOOL_STARTED", eventTypes);
        Assert.Contains("TOOL_COMPLETED", eventTypes);
        Assert.Contains("AUDIT_RECORDED", eventTypes);
        Assert.Contains("EXECUTION_COMPLETED", eventTypes);
    }

    [Fact]
    public async Task AgentActivityEvents_Do_Not_Expose_Private_Chain_Of_Thought()
    {
        using var db = GetInMemoryDbContext("ChainOfThoughtDb");
        var empService = new EmployeeService(db);
        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeReadTool(empService));

        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_READ",
            Entities = new Dictionary<string, string> { { "name", "Ahmed" } },
            Confidence = 0.95
        });

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

        var result = await orchestrator.ExecuteAsync("Find employee Ahmed");

        var events = (await broadcaster.GetRunEventsAsync(result.RunId)).ToList();

        foreach (var evt in events)
        {
            Assert.DoesNotContain("chain_of_thought", evt.Message.ToLower());
            Assert.DoesNotContain("scratchpad", evt.Message.ToLower());
            Assert.DoesNotContain("internal_reasoning", evt.Message.ToLower());
        }
    }

    [Fact]
    public async Task AgentFeedController_Retrieves_Activity_Events_Via_REST()
    {
        var broadcaster = new AgentEventBroadcaster(NullLogger<AgentEventBroadcaster>.Instance);
        var runId = Guid.NewGuid();

        await broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 0, "REQUEST_RECEIVED", "Test Request"));
        await broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 1, "EXECUTION_COMPLETED", "Test Finished"));

        var controller = new Nexus.Api.Controllers.AgentFeedController(broadcaster);
        var actionResult = await controller.GetRunEvents(runId);

        var okResult = actionResult.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        Assert.NotNull(okResult);

        var events = okResult!.Value as IEnumerable<Nexus.Data.Events.AgentActivityEvent>;
        Assert.NotNull(events);
        Assert.Equal(2, events!.Count());
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
