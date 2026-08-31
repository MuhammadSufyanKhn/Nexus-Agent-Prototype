using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Intent;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.LLM;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class JobOpeningsAndCvScreeningTests
{
    private IntentParser CreateParser()
    {
        var mockLlm = new MockLLMService(new ParsedIntentResult
        {
            Intent = "UNKNOWN",
            Confidence = 0.3
        });
        return new IntentParser(mockLlm, NullLogger<IntentParser>.Instance);
    }

    [Fact]
    public async Task Parse_JobOpeningCreate_IdentifiesIntentAndRole()
    {
        var parser = CreateParser();
        var prompt = "we want one new candidate for web dev and requirements are below: React, C#, SQL Server";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.JOB_OPENING_CREATE, result.ParsedIntentType);
        Assert.False(result.RequiresClarification);
        Assert.True(result.Entities.ContainsKey("title") || result.Parameters.ContainsKey("title"));
    }

    [Fact]
    public async Task Parse_TicketResolution_RoutesToTicketUpdate()
    {
        var parser = CreateParser();
        var prompt = "Mark service desk ticket TCK-IT-001 as resolved";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.TICKET_UPDATE, result.ParsedIntentType);
        Assert.False(result.RequiresClarification);
    }

    [Fact]
    public async Task Parse_ExecutiveOverview_RoutesToDashboardAnalytics()
    {
        var parser = CreateParser();
        var prompt = "Show an executive overview of active workforce metrics and department headcount";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.DASHBOARD_ANALYTICS, result.ParsedIntentType);
    }

    [Fact]
    public async Task Parse_PolicyDeleteWithCode_DoesNotRequireClarification()
    {
        var parser = CreateParser();
        var prompt = "Delete policy POL-FIN-002";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.POLICY_DELETE, result.ParsedIntentType);
        Assert.False(result.RequiresClarification);
        Assert.Equal("POL-FIN-002", result.Entities["policyCode"]);
    }

    [Fact]
    public async Task Parse_EquipmentProvisioning_RoutesToTicketCreate()
    {
        var parser = CreateParser();
        var prompt = "Need MacBook Pro M3 and AWS VPN access for new hire Sarah in DevOps";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.TICKET_CREATE, result.ParsedIntentType);
    }

    [Fact]
    public async Task Parse_ScreenCandidateFit_RoutesToCvScreen()
    {
        var parser = CreateParser();
        var prompt = "Screen candidate resume fit score for Senior Full Stack Developer position";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.CV_SCREEN, result.ParsedIntentType);
    }

    [Fact]
    public void CvAnalysisTool_CalculatesBestFitAndGeneratesQuestions()
    {
        var cvText = @"CANDIDATE: Ali Khan
Email: ali.khan@devmail.com
4+ years experience in C#, .NET Core, ASP.NET Core, React, SQL Server, Entity Framework, REST APIs, Microservices, Docker.
Designed Web APIs, optimized SQL Server queries by 45%, implemented RBAC security.";

        var analysis = CvAnalysisTool.AnalyzeCvText(
            cvText, 
            "Senior Full Stack Developer", 
            "React, C#, .NET Core, SQL Server, TypeScript, Entity Framework, REST APIs"
        );

        Assert.NotNull(analysis);
        Assert.True(analysis.MatchScore >= 80, $"Expected match score >= 80, got {analysis.MatchScore}");
        Assert.True(analysis.IsBestFit);
        Assert.Equal("Best Fit", analysis.FitCategory);
        Assert.NotEmpty(analysis.Strengths);
        Assert.NotEmpty(analysis.RecommendedInterviewQuestions);
        Assert.True(analysis.RecommendedInterviewQuestions.Count >= 3);
    }

    [Fact]
    public async Task DbContext_JobOpeningAndCandidateApplications_PersistAndCountCorrectly()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new NexusDbContext(options);

        var job = new JobOpening
        {
            Title = "Senior Full Stack Developer",
            Department = "IT",
            Description = "Lead backend API and frontend React development.",
            Requirements = "C#, .NET Core, React, SQL Server",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        db.JobOpenings.Add(job);
        await db.SaveChangesAsync();

        var app = new CandidateApplication
        {
            JobOpeningId = job.Id,
            CandidateName = "Ali Khan",
            Email = "ali.khan@devmail.com",
            Phone = "+92-300-1234567",
            ExperienceYears = 4,
            CvText = "Experienced .NET and React developer.",
            Status = "Submitted",
            SubmittedAt = DateTime.UtcNow
        };

        db.CandidateApplications.Add(app);
        await db.SaveChangesAsync();

        var loadedJob = await db.JobOpenings
            .Include(j => j.Applications)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        Assert.NotNull(loadedJob);
        Assert.Single(loadedJob.Applications);
        Assert.Equal("Ali Khan", loadedJob.Applications.First().CandidateName);
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
