using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class JobOpeningTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<JobOpeningTool> _logger;

    public ToolDefinition Definition => new()
    {
        Name = "job_opening.create",
        Description = "Creates candidate job openings and requisitions in database.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""title"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""description"": { ""type"": ""string"" },
            ""requirements"": { ""type"": ""string"" },
            ""salaryRange"": { ""type"": ""string"" }
          },
          ""required"": []
        }",
        RequiredPermission = "employee.update",
        RiskLevel = RiskLevel.Medium
    };

    public JobOpeningTool(NexusDbContext db, ILogger<JobOpeningTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        var title = context.GetArgument<string>("title") ?? "Senior Software Engineer";
        var dept = context.GetArgument<string>("department") ?? "IT";
        var desc = context.GetArgument<string>("description") ?? $"Requisition for {title} in {dept}.";
        var reqs = context.GetArgument<string>("requirements") ?? "C#, ASP.NET Core, React, SQL Server";
        var salary = context.GetArgument<string>("salaryRange") ?? context.GetArgument<string>("salary_range") ?? "$80,000 - $110,000";

        var job = new JobOpening
        {
            Title = title,
            Department = dept,
            Description = desc,
            Requirements = reqs,
            Location = "Remote / Hybrid",
            SalaryRange = salary,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        _db.JobOpenings.Add(job);
        await _db.SaveChangesAsync();
        sw.Stop();

        return ToolExecutionResult.Success(new
        {
            message = $"Job opening for '{title}' in {dept} department posted successfully (Requisition #{job.Id}).",
            jobId = job.Id,
            title,
            department = dept,
            status = "Active"
        }, Definition.RiskLevel, sw.ElapsedMilliseconds);
    }
}
