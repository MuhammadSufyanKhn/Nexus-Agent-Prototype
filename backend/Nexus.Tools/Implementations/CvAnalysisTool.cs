using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Nexus.Data.Enums;
using Nexus.Security.Permissions;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class CvCandidateAnalysisResult
{
    public string CandidateName { get; set; } = "Unknown Candidate";
    public string Email { get; set; } = string.Empty;
    public string TargetPosition { get; set; } = ".NET Developer";
    public int ExperienceYears { get; set; } = 3;
    public List<string> ExtractedSkills { get; set; } = new();
    public int MatchScore { get; set; } = 85;
    public string Recommendation { get; set; } = "RECOMMENDED";
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public ProposedCandidateRecord ProposedRecord { get; set; } = new();
}

public class ProposedCandidateRecord
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = "IT";
    public string Designation { get; set; } = ".NET Developer";
    public decimal SuggestedSalary { get; set; } = 85000m;
}

/// <summary>
/// Tool for analyzing uploaded CV text, extracting candidate skills/experience,
/// scoring fit against job descriptions, and generating a candidate creation proposal.
/// </summary>
public class CvAnalysisTool : IAgentTool
{
    public ToolDefinition Definition => new()
    {
        Name = "cv.analyze",
        Description = "Parse CV text, score against job description, extract candidate info, and generate employee candidate draft.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""cvContent"": { ""type"": ""string"" },
            ""jobTitle"": { ""type"": ""string"" },
            ""requiredSkills"": { ""type"": ""string"" }
          }
        }",
        RequiredPermission = "employee.create",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();

        string cvText = context.GetArgument<string>("cvContent") ?? context.ArgumentsJson ?? "";
        string jobTitle = context.GetArgument<string>("jobTitle") ?? ".NET Developer";
        string reqSkillsStr = context.GetArgument<string>("requiredSkills") ?? "C#, .NET Core, SQL Server, Entity Framework, REST API";

        if (string.IsNullOrWhiteSpace(cvText))
        {
            return Task.FromResult(ToolExecutionResult.Failure("CV text content is required."));
        }

        try
        {
            var analysis = AnalyzeCvText(cvText, jobTitle, reqSkillsStr);
            sw.Stop();
            return Task.FromResult(ToolExecutionResult.Success(analysis, Definition.RiskLevel, sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Task.FromResult(ToolExecutionResult.Failure($"Failed to analyze CV: {ex.Message}"));
        }
    }

    private static CvCandidateAnalysisResult AnalyzeCvText(string cvText, string jobTitle, string requiredSkillsStr)
    {
        var text = cvText.ToLowerInvariant();
        var requiredSkills = requiredSkillsStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        // 1. Candidate Name Extraction
        string candidateName = "Candidate";
        var nameMatch = Regex.Match(cvText, @"\b(?:name|candidate|resume|cv)\s*[:\-]?\s*([A-Za-z]+(?:\s+[A-Za-z]+)?)\b", RegexOptions.IgnoreCase);
        if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
        {
            candidateName = nameMatch.Groups[1].Value.Trim();
        }
        else
        {
            var firstLine = cvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.Length < 40 && !firstLine.Contains("@"))
            {
                candidateName = firstLine.Trim();
            }
        }

        // 2. Email Extraction
        string email = $"{candidateName.ToLower().Replace(" ", ".")}@nexus.local";
        var emailMatch = Regex.Match(cvText, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");
        if (emailMatch.Success)
        {
            email = emailMatch.Value;
        }

        // 3. Experience Extraction
        int experienceYears = 3;
        var expMatch = Regex.Match(cvText, @"\b(\d{1,2})\s*\+?\s*(?:years?|yrs?)\b", RegexOptions.IgnoreCase);
        if (expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out var exp))
        {
            experienceYears = exp;
        }

        // 4. Technical Skill Matching
        var knownTech = new[] { "C#", ".NET", ".NET Core", "ASP.NET", "SQL Server", "Entity Framework", "React", "TypeScript", "JavaScript", "Azure", "Docker", "REST API", "Git" };
        var foundSkills = new List<string>();

        foreach (var tech in knownTech)
        {
            if (text.Contains(tech.ToLowerInvariant()))
            {
                foundSkills.Add(tech);
            }
        }

        var missingSkills = new List<string>();
        int matchedCount = 0;

        foreach (var req in requiredSkills)
        {
            if (text.Contains(req.ToLowerInvariant()))
            {
                matchedCount++;
            }
            else
            {
                missingSkills.Add(req);
            }
        }

        int score = requiredSkills.Count > 0 ? (int)Math.Round((double)matchedCount / requiredSkills.Count * 100.0) : 85;
        if (foundSkills.Count >= 4 && score < 75) score = 80;
        if (score > 100) score = 100;

        string recommendation = score >= 80 ? "RECOMMENDED" : score >= 60 ? "SHORTLISTED" : "NOT_RECOMMENDED";

        var strengths = new List<string>();
        if (foundSkills.Count > 0) strengths.Add($"Demonstrated proficiency in {string.Join(", ", foundSkills.Take(4))}");
        if (experienceYears >= 3) strengths.Add($"Strong industry experience ({experienceYears} years in software development)");
        strengths.Add("Strong alignment with enterprise C# / .NET architecture stack");

        var weaknesses = new List<string>();
        if (missingSkills.Count > 0) weaknesses.Add($"Missing explicit experience keywords: {string.Join(", ", missingSkills)}");
        if (experienceYears < 2) weaknesses.Add("Junior-level experience duration");

        decimal suggestedSalary = experienceYears >= 5 ? 120000m : experienceYears >= 3 ? 85000m : 65000m;

        return new CvCandidateAnalysisResult
        {
            CandidateName = candidateName,
            Email = email,
            TargetPosition = jobTitle,
            ExperienceYears = experienceYears,
            ExtractedSkills = foundSkills,
            MatchScore = score,
            Recommendation = recommendation,
            Strengths = strengths,
            Weaknesses = weaknesses,
            MissingSkills = missingSkills,
            ProposedRecord = new ProposedCandidateRecord
            {
                Name = candidateName,
                Email = email,
                Department = "IT",
                Designation = jobTitle,
                SuggestedSalary = suggestedSalary
            }
        };
    }
}
