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

    [Fact]
    public async Task IntentParser_Parses_Budget_Reallocation_And_Overview_Commands()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var reallocResult = await parser.ParseIntentAsync("Reallocate 20000 budget from Marketing to IT department for Q3.");
        Assert.Equal(IntentType.BUDGET_REALLOCATE, reallocResult.ParsedIntentType);
        Assert.Equal("Marketing", reallocResult.Entities["sourceDepartment"]);
        Assert.Equal("IT", reallocResult.Entities["targetDepartment"]);

        var overviewResult = await parser.ParseIntentAsync("Generates an immediate overview of total allocated funds versus actual spend.");
        Assert.Equal(IntentType.BUDGET_ANALYSIS, overviewResult.ParsedIntentType);
    }

    [Fact]
    public async Task IntentParser_Parses_Department_Create_With_Head_And_Budget()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Create Finance department with head Sufyan and budget 500000");
        Assert.Equal(IntentType.DEPARTMENT_CREATE, result.ParsedIntentType);
        Assert.Equal("Finance", result.Entities["name"]);
        Assert.Equal("Sufyan", result.Entities["head"]);
        Assert.Equal("500000", result.Entities["budgetAmount"]);
    }

    [Theory]
    [InlineData("Log Ali sick day today and notify his team on gmail")]
    [InlineData("Log a sick day for Ali and notify on gmail")]
    public async Task IntentParser_Parses_Leave_Create_Correctly(string prompt)
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync(prompt);
        Assert.Equal(IntentType.LEAVE_CREATE, result.ParsedIntentType);
        Assert.Equal("Ali", result.Entities["name"]);
    }

    [Fact]
    public async Task IntentParser_Parses_Average_Salary_Command()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Calculate average employee salary in the IT department");
        Assert.Equal(IntentType.BUDGET_ANALYSIS, result.ParsedIntentType);
        Assert.Equal("IT", result.Entities["department"]);
        Assert.Equal("AVERAGE_SALARY", result.Parameters["query"]?.ToString());
    }

    [Fact]
    public async Task IntentParser_Parses_Filter_Employee_Directory()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Filter employee directory by Marketing department and show assigned managers");
        Assert.Equal(IntentType.EMPLOYEE_READ, result.ParsedIntentType);
        Assert.Equal("Marketing", result.Entities["department"]);
    }

    [Fact]
    public async Task IntentParser_Parses_Salary_Increase_Review()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Review proposed salary increase for Ali Khan from $80,000 to $90,000");
        Assert.Equal(IntentType.UPDATE_SALARY, result.ParsedIntentType);
        Assert.Equal("Ali Khan", result.Entities["name"]);
        Assert.Equal("80000", result.Entities["old_salary"]);
        Assert.Equal("90000", result.Entities["salary"]);
    }

    [Fact]
    public async Task IntentParser_Parses_Policy_Delete_Command()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Delete policy POL-FIN-002");
        Assert.Equal(IntentType.POLICY_DELETE, result.ParsedIntentType);
        Assert.Equal("POL-FIN-002", result.Entities["policyCode"]);
    }

    [Fact]
    public async Task IntentParser_Parses_Appoint_Acting_Head()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Appoint Ali as Acting Head of Admission department");
        Assert.Equal(IntentType.EMPLOYEE_PROMOTE, result.ParsedIntentType);
        Assert.Equal("EMPLOYEE", result.TargetEntity);
        Assert.Equal("UPDATE", result.Operation);
        Assert.Equal("Ali", result.Entities["name"]);
        Assert.Equal("Admission", result.Entities["department"]);
        Assert.Equal("Acting Head of Admission", result.Entities["designation"]);
        Assert.Equal("Leadership Appointment", result.Entities["appointment_type"]);
    }

    [Fact]
    public async Task IntentParser_Parses_Show_Active_Employees_Engineering()
    {
        var mockLlm = new MockLLMService(null);
        var parser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);

        var result = await parser.ParseIntentAsync("Show all active employees in the Engineering department");
        Assert.Equal(IntentType.EMPLOYEE_READ, result.ParsedIntentType);
        Assert.Equal("EMPLOYEE", result.TargetEntity);
        Assert.Equal("READ", result.Operation);
        Assert.Equal("Engineering", result.Entities["department"]);
        Assert.Equal("Active", result.Entities["status"]);
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
