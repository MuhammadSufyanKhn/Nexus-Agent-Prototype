using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Agent.Orchestration;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Data.LLM;
using Nexus.Data.Services;
using Nexus.Security.Permissions;
using Nexus.Security.Risk;
using Nexus.Security.Sql;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class DynamicScreeningAndOnboardingTests : IDisposable
{
    private readonly NexusDbContext _db;
    private readonly string _dbName;
    private readonly AgentOrchestrator _orchestrator;
    private readonly IntentParser _intentParser;
    private readonly SqlValidator _sqlValidator;

    public DynamicScreeningAndOnboardingTests()
    {
        _dbName = "DynamicTests_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _db = new NexusDbContext(options);

        // Seed test data
        var dept = new Department { Id = 1, Name = "Engineering", Description = "Core Tech Pod" };
        _db.Departments.Add(dept);

        var emp = new Employee
        {
            Id = 1,
            Name = "Ahmed Khan",
            Email = "ahmed.khan@company.com",
            DepartmentId = 1,
            Designation = "Senior .NET Developer",
            Salary = 98000m,
            ExperienceYears = 5,
            Status = EmployeeStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-3)
        };
        _db.Employees.Add(emp);

        var emp2 = new Employee
        {
            Id = 2,
            Name = "Ali Khan",
            Email = "ali.khan@company.com",
            DepartmentId = 1,
            Designation = "Full Stack Engineer",
            Salary = 90000m,
            ExperienceYears = 4,
            Status = EmployeeStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-1)
        };
        _db.Employees.Add(emp2);

        var job = new JobOpening
        {
            Id = 1,
            Title = "Product Manager",
            Department = "Engineering",
            Requirements = "Product Strategy, Roadmapping, Stakeholder Management, Agile / Scrum ceremonies, and User Research."
        };
        _db.JobOpenings.Add(job);

        var cand = new CandidateApplication
        {
            Id = 1,
            JobOpeningId = 1,
            CandidateName = "Sarah Jenkins",
            Email = "sarah.j@example.com",
            ExperienceYears = 6,
            CvText = @"CANDIDATE: Sarah Jenkins
6+ years experience in Product Strategy, Roadmapping, Stakeholder Management, Agile / Scrum ceremonies, and User Research.
Led cross-functional team delivery of B2B analytics portal. Coordinated sprint planning, mentored junior PMs.",
            Status = "In Progress",
            SubmittedAt = DateTime.UtcNow.AddDays(-2)
        };
        _db.CandidateApplications.Add(cand);

        var cand2 = new CandidateApplication
        {
            Id = 2,
            JobOpeningId = 1,
            CandidateName = "Daniël Gan",
            Email = "daniel.g@example.com",
            ExperienceYears = 3,
            CvText = @"CANDIDATE: Daniël Gan
3+ years of Frontend Development with React, TypeScript, Tailwind CSS, Redux, HTML5, and Responsive Web Design.
Built modern UI component library and optimized rendering performance.",
            Status = "In Progress",
            SubmittedAt = DateTime.UtcNow.AddDays(-1)
        };
        _db.CandidateApplications.Add(cand2);

        _db.SaveChanges();

        // Dependencies
        var docService = new PdfDocumentService(_db);
        var mockLlm = new MockLLMService(null);
        _intentParser = new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
        _sqlValidator = new SqlValidator();

        var registry = new ToolRegistry();
        registry.RegisterTool(new CvAnalysisTool());
        registry.RegisterTool(new StubTool("email.welcome"));
        registry.RegisterTool(new OnboardingPrepareTool(docService, _db));
        registry.RegisterTool(new SqlAnalyticsTool(_sqlValidator, mockLlm, _db, NullLogger<SqlAnalyticsTool>.Instance));

        _orchestrator = new AgentOrchestrator(
            _intentParser,
            registry,
            _db,
            NullLogger<AgentOrchestrator>.Instance,
            new RiskEngine(),
            new PermissionService(),
            null,
            null,
            docService);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task Candidate_FitScore_ForProductManager_DoesNotUseFullStackCsharp()
    {
        var prompt = "Evaluate candidate resume fit score for Product Manager position for Sarah Jenkins.";
        var result = await _orchestrator.ExecuteAsync(prompt);

        Assert.True(result.IsSuccess, $"Failed with error: {result.ErrorMessage}, State: {result.State}, Msg: {result.UserMessage}");
        Assert.NotNull(result.ResultData);

        var summary = result.UserMessage;
        Assert.Contains("Sarah Jenkins", summary);
        Assert.Contains("Product Manager", summary);

        var json = JsonSerializer.Serialize(result.ResultData);
        using var doc = JsonDocument.Parse(json);
        var fitScore = doc.RootElement.GetProperty("fitScore").GetInt32();
        Assert.True(fitScore >= 50, $"FitScore was {fitScore}, expected >= 50. JSON: {json}");
    }

    [Fact]
    public async Task Candidate_SkillAnalysis_ExtractsRequestedSkillsOnly()
    {
        var prompt = "Analyze candidate technical skills in React, TypeScript, and C# .NET for candidate Daniël Gan.";
        var result = await _orchestrator.ExecuteAsync(prompt);

        Assert.True(result.IsSuccess, $"Failed with error: {result.ErrorMessage}, State: {result.State}, Msg: {result.UserMessage}");

        var json = JsonSerializer.Serialize(result.ResultData);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Daniël Gan", doc.RootElement.GetProperty("candidateName").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("totalSkills").GetInt32());
        // React and TypeScript are found, C# .NET is Not Found
        Assert.Equal(2, doc.RootElement.GetProperty("skillsFound").GetInt32());
    }

    [Fact]
    public async Task Candidate_LeadershipAnalysis_IdentifiesLeadershipEvidence()
    {
        var prompt = "Screen candidate resume for leadership experience and project management for Sarah Jenkins.";
        var result = await _orchestrator.ExecuteAsync(prompt);

        Assert.True(result.IsSuccess, $"Failed with error: {result.ErrorMessage}, State: {result.State}, Msg: {result.UserMessage}");

        var json = JsonSerializer.Serialize(result.ResultData);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Sarah Jenkins", doc.RootElement.GetProperty("candidateName").GetString());
        var rating = doc.RootElement.GetProperty("leadershipRating").GetString();
        Assert.True(rating != null && rating.Contains("Demonstrated Leadership"), $"Rating was: '{rating}', JSON: {json}");
    }

    [Fact]
    public async Task Candidate_NoCandidateSpecified_ReturnsClarificationPrompt()
    {
        var prompt = "Analyze candidate technical skills in React and TypeScript.";
        var result = await _orchestrator.ExecuteAsync(prompt);

        Assert.False(result.IsSuccess);
        Assert.Equal("CLARIFICATION_REQUIRED", result.State);
        Assert.Contains("Please select a candidate CV", result.UserMessage);
    }

    [Fact]
    public async Task Onboarding_EmailResend_DoesNotCreateNewEmployee()
    {
        var initialEmpCount = await _db.Employees.CountAsync();

        var prompt = "Resend official onboarding welcome email to Ahmed Khan at ahmed.khan@company.com.";
        var result = await _orchestrator.ExecuteAsync(prompt);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Contains("Ahmed Khan", result.UserMessage);
        Assert.Contains("ahmed.khan@company.com", result.UserMessage);

        // Crucial test: No new employee was created!
        var afterEmpCount = await _db.Employees.CountAsync();
        Assert.Equal(initialEmpCount, afterEmpCount);
    }

    [Fact]
    public async Task Onboarding_DocumentGenerate_CreatesDocument()
    {
        var prompt = "Generate complete Onboarding Package document for new hire Ali Khan.";
        var result = await _orchestrator.ExecuteAsync(prompt);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        var json = JsonSerializer.Serialize(result.ResultData);
        using var doc = JsonDocument.Parse(json);
        Assert.NotEmpty(doc.RootElement.GetProperty("documentId").GetString() ?? "");
        Assert.Contains("Ali Khan", doc.RootElement.GetProperty("employeeName").GetString());
        Assert.Contains("/api/documents/", doc.RootElement.GetProperty("downloadUrl").GetString());
        Assert.NotEmpty(doc.RootElement.GetProperty("contentHtml").GetString() ?? "");
    }

    [Fact]
    public void SqlValidator_AllowsTicketsAndMasterBudgets()
    {
        var ticketQuery = "SELECT TicketId, EmployeeName, Status FROM Tickets WHERE Status = 'Open'";
        var ticketRes = _sqlValidator.ValidateQuery(ticketQuery);
        Assert.True(ticketRes.IsValid, ticketRes.ErrorMessage);

        var budgetQuery = "SELECT TotalBudgetPool, RemainingBalance FROM MasterBudgets WHERE Year = 2026";
        var budgetRes = _sqlValidator.ValidateQuery(budgetQuery);
        Assert.True(budgetRes.IsValid, budgetRes.ErrorMessage);
    }

    private class StubTool : IAgentTool
    {
        public ToolDefinition Definition { get; }
        public StubTool(string name) => Definition = new ToolDefinition { Name = name, Description = name, RiskLevel = RiskLevel.Low };
        public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context) => Task.FromResult(ValidationResult.Success());
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context) => Task.FromResult(ToolExecutionResult.Success(new { status = "OK" }, RiskLevel.Low));
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
