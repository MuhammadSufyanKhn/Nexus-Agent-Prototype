using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Data.LLM;
using Xunit;

namespace Nexus.Tests;

public class IntentParserTests
{
    [Fact]
    public async Task IntentParser_Parses_Employee_Create_Successfully()
    {
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_CREATE",
            Entities = new Dictionary<string, string>
            {
                { "name", "Ahmed Khan" },
                { "department", "IT" },
                { "designation", "Developer" }
            },
            Confidence = 0.95,
            RequiresClarification = false
        });

        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var result = await parser.ParseIntentAsync("Create Ahmed Khan as an IT developer.");

        Assert.NotNull(result);
        Assert.Equal("EMPLOYEE_CREATE", result.Intent);
        Assert.Equal(IntentType.EMPLOYEE_CREATE, result.ParsedIntentType);
        Assert.False(result.RequiresClarification);
        Assert.Equal("Ahmed Khan", result.Entities["name"]);
        Assert.Equal("IT", result.Entities["department"]);
    }

    [Fact]
    public async Task IntentParser_Flags_Clarification_When_Required_Entities_Are_Missing()
    {
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "EMPLOYEE_CREATE",
            Entities = new Dictionary<string, string>(), // Empty entities
            Confidence = 0.90,
            RequiresClarification = false
        });

        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var result = await parser.ParseIntentAsync("Create an employee");

        Assert.True(result.RequiresClarification);
        Assert.NotNull(result.ClarificationPrompt);
        Assert.Contains("specify", result.ClarificationPrompt, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IntentParser_Parses_Budget_Analysis_Intent()
    {
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "BUDGET_ANALYSIS",
            Entities = new Dictionary<string, string> { { "quarter", "Q3" } },
            Confidence = 0.98
        });

        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        var result = await parser.ParseIntentAsync("Show me departments exceeding their allocated Q3 budget.");

        Assert.Equal(IntentType.BUDGET_ANALYSIS, result.ParsedIntentType);
        Assert.False(result.RequiresClarification);
    }

    [Fact]
    public async Task IntentParser_Falls_Back_Gracefully_On_Malformed_Or_Null_Response()
    {
        var mockLlm = new MockLLMService(null); // LLM fails or returns null
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Onboard Ahmed Khan in IT");

        Assert.NotNull(result);
        Assert.Equal(IntentType.EMPLOYEE_ONBOARDING, result.ParsedIntentType);
        Assert.Equal("Ahmed Khan", result.Entities["name"]);
        Assert.Equal("IT", result.Entities["department"]);
    }

    [Fact]
    public async Task IntentParser_Extracts_Department_When_Joining_Phrasing_Is_Used()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var prompt = "onboard new employee whose name is Sufyan Khan. his designation is junior .NET. His salary is set to 80k per month. he will be joining IT department.";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.NotNull(result);
        Assert.Equal(IntentType.EMPLOYEE_ONBOARDING, result.ParsedIntentType);
        Assert.Equal("Sufyan Khan", result.Entities["name"]);
        Assert.Equal("IT", result.Entities["department"]);
        Assert.Equal("Junior .NET", result.Entities["designation"]);
    }

    [Fact]
    public async Task IntentParser_Extracts_Target_Department_For_Department_Deletion()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var prompt = "remove the marketing department and transfer its budget to IT department";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.NotNull(result);
        Assert.Equal(IntentType.DEPARTMENT_DELETE, result.ParsedIntentType);
        Assert.Equal("Marketing", result.Entities["name"]);
        Assert.Equal("Marketing", result.Entities["department"]);
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
