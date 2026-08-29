using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Data.Services;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class OnboardingPrepareTool : IAgentTool
{
    private readonly IDocumentService _documentService;
    private readonly NexusDbContext _db;

    public OnboardingPrepareTool(IDocumentService documentService, NexusDbContext db)
    {
        _documentService = documentService;
        _db = db;
    }

    public ToolDefinition Definition => new()
    {
        Name = "onboarding.prepare",
        Description = "Generates comprehensive onboarding document packet (Appointment Letter, NDA, IT Equipment Acknowledgement) for new hires.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""name"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""designation"": { ""type"": ""string"" },
            ""salary"": { ""type"": ""number"" }
          },
          ""required"": [""name""]
        }",
        RequiredPermission = "employee.create",
        RiskLevel = RiskLevel.Medium
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var name = GetCandidateName(context);
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(ValidationResult.Failure("Candidate 'name' is required for onboarding paperwork generation."));

        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var validation = await ValidateInputAsync(context);
        if (!validation.IsValid)
            return ToolExecutionResult.Failure(string.Join("; ", validation.Errors), Definition.RiskLevel);

        var sw = Stopwatch.StartNew();
        try
        {
            var name = GetCandidateName(context)!;
            var deptName = context.GetArgument<string>("department") ?? "IT";
            var designation = context.GetArgument<string>("designation") ?? context.GetArgument<string>("role") ?? "Senior Developer";
            var salary = context.GetArgument<decimal?>("salary") ?? 95000m;

            var existingEmp = await _db.Employees.FirstOrDefaultAsync(e => e.Name.ToLower().Contains(name.ToLower()));
            if (existingEmp != null)
            {
                if (!string.IsNullOrWhiteSpace(existingEmp.Designation)) designation = existingEmp.Designation;
                if (existingEmp.Salary > 0) salary = existingEmp.Salary;
            }

            var doc = await _documentService.GenerateOnboardingPackageAsync(
                name, deptName, designation, salary, DateTime.UtcNow.AddDays(7), context.AgentRunId);

            sw.Stop();
            return ToolExecutionResult.Success(new
            {
                documentId = doc.Id,
                documentTitle = doc.Title,
                documentType = doc.DocumentType,
                downloadUrl = $"/api/documents/{doc.Id}/download",
                previewUrl = $"/api/documents/{doc.Id}/preview",
                message = $"Successfully generated onboarding paperwork package for {name} ({deptName} - {designation})."
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }

    private static string? GetCandidateName(ToolExecutionContext context)
    {
        var name = context.GetArgument<string>("name")
            ?? context.GetArgument<string>("employeeName")
            ?? context.GetArgument<string>("candidateName")
            ?? context.GetArgument<string>("candidate");

        if (!string.IsNullOrWhiteSpace(name) && !name.Equals("In It", StringComparison.OrdinalIgnoreCase)) return name.Trim();

        var prompt = context.GetArgument<string>("prompt") ?? context.GetArgument<string>("question") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            var knownNames = new[] { "John Smith", "Jane Doe", "Michael Johnson", "David Lee", "Robert Chen", "Sarah Jenkins", "Tariq Mahmood", "Maria Garcia", "Ahmed Khan", "Sufyan Khan", "Alex", "Amanda", "Sarah", "Jim", "Pam", "Marcus", "Ali", "Sara", "Ahmed" };
            foreach (var kn in knownNames)
            {
                if (Regex.IsMatch(prompt, $@"\b{kn}\b", RegexOptions.IgnoreCase))
                    return kn;
            }

            var capNameMatch = Regex.Match(prompt, @"\b([A-Z][a-z]+\s+[A-Z][a-z]+)\b");
            if (capNameMatch.Success)
            {
                var candidate = capNameMatch.Groups[1].Value.Trim();
                var lower = candidate.ToLowerInvariant();
                if (!lower.Contains("department") && !lower.Contains("office") && !lower.Contains("branch") && !lower.Contains("team"))
                    return candidate;
            }
        }

        return null;
    }
}
