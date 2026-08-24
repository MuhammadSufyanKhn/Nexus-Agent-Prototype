using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Data.LLM;
using Nexus.Data;
using Nexus.Security.Sql;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Nexus.Data.Entities;
using Xunit;

namespace Nexus.Tests;

public class SqlSecurityTests
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
    public void SqlValidator_Allows_Valid_Select_Queries()
    {
        var validator = new SqlValidator();
        var result = validator.ValidateQuery("SELECT Name, Salary FROM Employees WHERE DepartmentId = 1");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.Contains("TOP (100)", result.SanitizedQuery);
        Assert.Contains("Employees", result.DetectedTables);
    }

    [Fact]
    public void SqlValidator_Rejects_Delete_Statements()
    {
        var validator = new SqlValidator();
        var result1 = validator.ValidateQuery("DELETE FROM Employees WHERE Id = 1");
        Assert.False(result1.IsValid);
        Assert.Contains("Security Violation", result1.ErrorMessage);

        var result2 = validator.ValidateQuery("SELECT * FROM Employees DELETE");
        Assert.False(result2.IsValid);
        Assert.Contains("DELETE", result2.ErrorMessage);
    }

    [Fact]
    public void SqlValidator_Rejects_Drop_Statements()
    {
        var validator = new SqlValidator();
        var result = validator.ValidateQuery("DROP TABLE Departments;");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Security Violation", result.ErrorMessage);
    }

    [Fact]
    public void SqlValidator_Rejects_Unapproved_Tables()
    {
        var validator = new SqlValidator();
        var result = validator.ValidateQuery("SELECT * FROM sys.tables");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("whitelist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlValidator_Enforces_Row_Limit_Cap()
    {
        var validator = new SqlValidator();
        var result = validator.ValidateQuery("SELECT Name, Description FROM Departments");

        Assert.True(result.IsValid);
        Assert.StartsWith("SELECT TOP (100)", result.SanitizedQuery);
    }

    [Fact]
    public async Task SqlAnalyticsTool_Executes_Q3_Budget_Overflow_Query_Successfully()
    {
        using var db = GetInMemoryDbContext("SqlAnalyticsToolDb");
        var dept = db.Departments.FirstOrDefault(d => d.Name == "IT");
        if (dept == null)
        {
            dept = new Department { Name = "IT", Description = "IT Department" };
            db.Departments.Add(dept);
            db.SaveChanges();
        }
        db.Budgets.Add(new Budget { DepartmentId = dept.Id, Year = 2026, Quarter = "Q3", AllocatedAmount = 50000m, SpentAmount = 60000m });
        db.SaveChanges();

        var validator = new SqlValidator();
        var mockLlm = new MockLLMService(null);

        var tool = new SqlAnalyticsTool(validator, mockLlm, db, NullLogger<SqlAnalyticsTool>.Instance);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = @"{ ""question"": ""Show me departments exceeding their allocated Q3 budget."" }"
        };

        var result = await tool.ExecuteAsync(context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        var analyticsResult = result.Data as SqlAnalyticsResult;
        Assert.NotNull(analyticsResult);
        Assert.NotEmpty(analyticsResult!.Summary);
        Assert.Contains("IT", analyticsResult.Summary);
        Assert.Contains("exceeded", analyticsResult.Summary, StringComparison.OrdinalIgnoreCase);
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
            return Task.FromResult(LLMResponse.Success("SELECT d.Name AS DepartmentName, b.AllocatedAmount, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id", "mock-model", 10));
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
