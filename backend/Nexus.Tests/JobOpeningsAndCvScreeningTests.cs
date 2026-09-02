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
    public async Task Parse_LeadCloudArchitect_CorrectlyClassifiedAsJobOpeningCreate()
    {
        var parser = CreateParser();
        var prompt = "Create a new job opening for Lead Cloud Architect in IT department with salary $95,000 - $125,000 requiring AWS, Kubernetes, Terraform, Docker, and CI/CD.";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.JOB_OPENING_CREATE, result.ParsedIntentType);
        Assert.True(result.RequiresApproval);
        Assert.Equal("IT", result.Entities["department"]);
        Assert.Equal("Lead Cloud Architect", result.Entities["title"]);
        Assert.Equal("$95,000 - $125,000", result.Entities["salaryRange"]);
        Assert.Equal("AWS, Kubernetes, Terraform, Docker, and CI/CD", result.Entities["requirements"]);
    }

    [Fact]
    public async Task Parse_JobOpeningCreate_WithRoleOverviewAndResponsibilities_ClassifiedAsJobOpeningCreate()
    {
        var parser = CreateParser();
        var prompt = "Create a new job opening for .NET Developer in IT department with location Remote / Hybrid, salary $50,000 - $60,000. Role Overview: Lead enterprise architecture, cloud modernization, and system scalability for IT department. Key Technical Requirements: ASP.NET, C#, Entity Framework, Web API development, Database Management, SQL, LINQ. Core Responsibilities: Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles. • Collaborate across multidisciplinary engineering, C#, and sql database. • Optimize query execution, conduct peer code reviews, and champion continuous automated testing.";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.JOB_OPENING_CREATE, result.ParsedIntentType);
        Assert.True(result.RequiresApproval);
        Assert.Equal(".NET Developer", result.Entities["title"]);
        Assert.Equal("IT", result.Entities["department"]);
        Assert.Equal("Remote / Hybrid", result.Entities["location"]);
        Assert.Equal("$50,000 - $60,000", result.Entities["salaryRange"]);
        Assert.Contains("enterprise architecture", result.Entities["description"]);
        Assert.Contains("Clean Architecture", result.Entities["responsibilities"]);
        Assert.Contains("Entity Framework", result.Entities["requirements"]);
    }

    [Fact]
    public async Task Parse_CvScreen_WithRoleAndScoreMatchFit_ExtractsTargetRoleCorrectly()
    {
        var parser = CreateParser();
        var prompt = "Screen submitted candidate CVs for Junior SQA Engineer and score match fit.";
        var result = await parser.ParseIntentAsync(prompt);

        Assert.Equal(IntentType.CV_SCREEN, result.ParsedIntentType);
        Assert.Equal("Junior SQA Engineer", result.Entities["jobTitle"]);
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
Email: ali.khan@nexus.local
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
    public void CvAnalysisTool_AccuratelyScoresLowForCandidateMissingRequiredCompetencies()
    {
        // Muhammad Sufyan Khan's CV (Junior .NET Developer) applying to Lead Cloud Architect
        var cvText = @"MUHAMMAD SUFYAN KHAN UNDERGRADUATE STUDENT
PROFESSIONAL OVERVIEW Dedicated .NET Developer and Computer Science student combining strong academic foundations with practical software engineering experience. Proficient in building robust applications using C#, .NET Core and modern database systems.
WORK EXPERIENCE
.NET Fullstack (.NET+ReactJS) intern 10Pearls Pakistan, Karachi | April 2026 – June 2026
• Developed a full-stack task management solution using ASP.NET Core Web API and React (Vite).
• Incorporated xUnit and Moq to perform unit testing.
TECHNICAL SKILLS
• ASP.NET • Entity Framework • Web API development • Database Management • Unit Testing • SQL";

        var analysis = CvAnalysisTool.AnalyzeCvText(
            cvText,
            "Lead Cloud Architect",
            "AWS, Kubernetes, Terraform, Docker, and CI/CD"
        );

        Assert.NotNull(analysis);
        // Candidate has 0 of the 5 required cloud competencies (AWS, K8s, Terraform, Docker, CI/CD)
        // Score must NOT be inflated to 88%
        Assert.True(analysis.MatchScore <= 35, $"Expected honest score <= 35 for 0 matching competencies, but got {analysis.MatchScore}");
        Assert.False(analysis.IsBestFit);
        Assert.Contains(analysis.FitCategory, new[] { "Not a Fit", "Moderate Fit" });
        Assert.Equal("Muhammad Sufyan Khan", analysis.CandidateName);
        Assert.NotEmpty(analysis.MissingSkills);
        Assert.All(analysis.RecommendedInterviewQuestions, q => Assert.DoesNotContain("TechCorp Solutions", q));
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
            Email = "ali.khan@nexus.local",
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
