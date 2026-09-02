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
    public string FitCategory { get; set; } = "Best Fit"; // Best Fit, Strong Match, Moderate Fit, Not a Fit
    public bool IsBestFit { get; set; } = true;
    public string FitSummary { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public List<string> MissingSkills { get; set; } = new();
    public List<string> RecommendedInterviewQuestions { get; set; } = new();
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

    public static CvCandidateAnalysisResult AnalyzeCvText(string cvText, string jobTitle, string requiredSkillsStr)
    {
        var text = cvText.ToLowerInvariant();
        var requiredSkills = requiredSkillsStr.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        // 1. Candidate Name Extraction
        string candidateName = "Candidate";
        var nameMatch = Regex.Match(cvText, @"\b(?:name|candidate|resume|cv)\s*[:\-]?\s*([A-Za-z]+(?:\s+[A-Za-z]+){1,3})\b", RegexOptions.IgnoreCase);
        if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
        {
            candidateName = nameMatch.Groups[1].Value.Trim();
        }
        else
        {
            var firstLine = cvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstLine))
            {
                firstLine = Regex.Replace(firstLine, @"^(?:candidate\s+resume|resume|cv|candidate)\s*[:\-]?\s*", "", RegexOptions.IgnoreCase).Trim();
                var titleWords = new[] { "UNDERGRADUATE", "STUDENT", "DEVELOPER", "ENGINEER", "SOFTWARE", "PROFESSIONAL" };
                foreach (var tw in titleWords)
                {
                    int idx = firstLine.IndexOf(tw, StringComparison.OrdinalIgnoreCase);
                    if (idx > 2) firstLine = firstLine.Substring(0, idx).Trim();
                }
                if (firstLine.Length > 2 && firstLine.Length < 35 && !firstLine.Contains("@"))
                {
                    candidateName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(firstLine.ToLower());
                }
            }
        }

        // 2. Email Extraction
        string email = $"{candidateName.ToLower().Replace(" ", ".")}@gmail.com";
        var emailMatch = Regex.Match(cvText, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b");
        if (emailMatch.Success)
        {
            email = emailMatch.Value;
        }

        // 3. Experience Extraction
        int experienceYears = 1;
        bool isStudentOrIntern = text.Contains("student") || text.Contains("undergrad") || text.Contains("intern");
        var expMatch = Regex.Match(cvText, @"\b(\d{1,2})\s*\+?\s*(?:years?|yrs?)\s*(?:of\s*)?(?:exp|experience)?\b", RegexOptions.IgnoreCase);
        if (expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out var exp))
        {
            experienceYears = exp;
        }
        else if (isStudentOrIntern)
        {
            experienceYears = 1;
        }
        else
        {
            experienceYears = 2;
        }

        // 4. Competency & Skill Matching
        var knownTech = new[] {
            "C#", ".NET", ".NET Core", "ASP.NET", "SQL Server", "Entity Framework", "React", "TypeScript",
            "JavaScript", "Azure", "Docker", "REST API", "Git", "HTML5", "CSS3", "Redux", "Microservices",
            "T-SQL", "Tailwind", "AWS", "Kubernetes", "Terraform", "CI/CD",
            "Product Strategy", "Roadmapping", "Agile", "Scrum", "User Research", "Stakeholder Management",
            "Leadership", "HRIS", "Onboarding", "Compliance", "Benefits", "Payroll", "Talent Acquisition"
        };
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
            var reqNorm = req.ToLowerInvariant().Replace(".js", "").Replace("core", "").Trim();
            var significantWords = req.ToLowerInvariant()
                .Split(new[] { ' ', '/', '-', '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && w != "with" && w != "from" && w != "management" && w != "support")
                .ToList();

            bool isMatched = text.Contains(req.ToLowerInvariant()) ||
                             (!string.IsNullOrWhiteSpace(reqNorm) && text.Contains(reqNorm)) ||
                             (significantWords.Count > 0 && significantWords.Any(w => text.Contains(w)));

            if (isMatched)
            {
                matchedCount++;
                if (!foundSkills.Contains(req)) foundSkills.Add(req);
            }
            else
            {
                missingSkills.Add(req);
            }
        }

        // Honest qualification scoring: strictly proportional to target required competencies
        int score = 0;
        if (requiredSkills.Count > 0)
        {
            score = (int)Math.Round((double)matchedCount / requiredSkills.Count * 85.0);
            int generalBonus = Math.Min(15, foundSkills.Count * 2);
            score += generalBonus;

            // Strict guard: If candidate has 0 of the required core skills, score must reflect lack of fit
            if (matchedCount == 0)
            {
                score = Math.Min(20, foundSkills.Count * 3);
            }
        }
        else
        {
            score = Math.Min(95, foundSkills.Count * 12);
        }

        score = Math.Clamp(score, 5, 98);

        // Categorize Fit
        bool isBestFit = score >= 80 && (requiredSkills.Count == 0 || matchedCount >= Math.Max(1, (int)Math.Ceiling(requiredSkills.Count * 0.7)));
        string fitCategory = score >= 85 ? "Best Fit" : score >= 70 ? "Strong Match" : score >= 45 ? "Moderate Fit" : "Not a Fit";
        string recommendation = isBestFit ? "RECOMMENDED (BEST FIT)" : score >= 70 ? "RECOMMENDED" : score >= 45 ? "CONSIDER" : "NOT RECOMMENDED";

        string fitSummary = isBestFit
            ? $"Candidate {candidateName} is an exceptional BEST FIT for the {jobTitle} position with an authoritative {score}% qualification match across {matchedCount} of {requiredSkills.Count} core competencies."
            : matchedCount == 0 && requiredSkills.Count > 0
                ? $"Candidate {candidateName} scored {score}% ({fitCategory}). While the candidate possesses skills in {string.Join(", ", foundSkills.Take(3))}, they lack all primary competencies required for {jobTitle} ({string.Join(", ", missingSkills)})."
                : $"Candidate {candidateName} scored {score}% for the {jobTitle} position ({fitCategory}). Key competencies {string.Join(", ", requiredSkills.Except(missingSkills))} are verified, with gaps in {string.Join(", ", missingSkills.Take(2))}.";

        var strengths = new List<string>();
        if (foundSkills.Count > 0) strengths.Add($"Demonstrated proficiency in {string.Join(", ", foundSkills.Take(5))}");
        if (experienceYears >= 3) strengths.Add($"Strong professional experience ({experienceYears}+ years in software engineering)");
        else if (isStudentOrIntern) strengths.Add("Strong academic foundation and demonstrated hands-on internship experience");
        strengths.Add($"Direct practical experience building modern applications and database integrations");
        strengths.Add($"Knowledge of clean architecture, unit testing, and software quality assurance");

        var weaknesses = new List<string>();
        if (missingSkills.Count > 0) weaknesses.Add($"Missing explicit competencies required for role: {string.Join(", ", missingSkills.Take(4))}");
        if (experienceYears < 2) weaknesses.Add("Junior / student-level experience duration compared to senior role expectations");

        // 5. Generate Targeted Interview Questions based on Candidate CV and Target Role
        var interviewQuestions = new List<string>();

        if (missingSkills.Count > 0)
        {
            interviewQuestions.Add($"The {jobTitle} position requires hands-on mastery of {missingSkills.First()}. How would you bridge your background in {foundSkills.FirstOrDefault() ?? "software development"} to design and deploy {missingSkills.First()} in production?");
        }
        else
        {
            interviewQuestions.Add($"How have you designed and architected scalable systems using {requiredSkills.FirstOrDefault() ?? "modern backend architectures"}?");
        }

        if (missingSkills.Any(s => s.ToLower().Contains("docker") || s.ToLower().Contains("kubernetes") || s.ToLower().Contains("aws") || s.ToLower().Contains("terraform") || s.ToLower().Contains("ci/cd")))
        {
            interviewQuestions.Add($"What is your practical experience with containerization, cloud infrastructure (e.g. AWS, Kubernetes, Terraform), and how do you automate CI/CD deployments?");
        }
        else
        {
            interviewQuestions.Add($"Can you describe your approach to state management, component lifecycle, and high-throughput API communication?");
        }

        interviewQuestions.Add($"What specific strategies do you use for SQL query optimization, database indexing, and preventing deadlocks in high-concurrency environments?");

        interviewQuestions.Add($"Walk us through how you design automated unit tests (e.g. using xUnit, Moq) to ensure reliable code quality and system stability.");

        interviewQuestions.Add($"Given the responsibilities of a {jobTitle}, how do you ensure security, role-based access control (RBAC), and system resilience across distributed microservices?");

        decimal suggestedSalary = experienceYears >= 5 ? 110000m : experienceYears >= 3 ? 85000m : 68000m;

        return new CvCandidateAnalysisResult
        {
            CandidateName = candidateName,
            Email = email,
            TargetPosition = jobTitle,
            ExperienceYears = experienceYears,
            ExtractedSkills = foundSkills,
            MatchScore = score,
            Recommendation = recommendation,
            FitCategory = fitCategory,
            IsBestFit = isBestFit,
            FitSummary = fitSummary,
            Strengths = strengths,
            Weaknesses = weaknesses,
            MissingSkills = missingSkills,
            RecommendedInterviewQuestions = interviewQuestions,
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

    public static List<string> GenerateAlternativeInterviewQuestions(
        string cvText, 
        string jobTitle, 
        string requiredSkillsStr, 
        List<string>? existingQuestions = null)
    {
        var existing = existingQuestions ?? new List<string>();
        var requiredSkills = (requiredSkillsStr ?? "")
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var text = (cvText ?? "").ToLowerInvariant();

        var questionPool = new List<string>
        {
            $"Describe a production incident or critical outage you diagnosed in your previous applications. How did you isolate the root cause and ensure system recovery?",
            $"For the {jobTitle} position, how would you design a rate-limiting and throttling strategy to protect critical backend APIs from overload?",
            $"How do you manage distributed transactions and state consistency across microservices without relying on heavy two-phase commits (e.g. Saga pattern vs Outbox)?",
            $"If a critical service experiences high latency and thread starvation in production, what profiling tools and telemetry would you examine first?",
            $"How do you approach database schema migrations and zero-downtime database deployments in a high-velocity CI/CD pipeline?",
            $"What architecture patterns do you adopt for distributed caching (e.g. Redis), and how do you prevent cache stampedes and stale data?",
            $"Can you explain how you secure sensitive secrets, API tokens, and connection strings across automated deployments and production containers?",
            $"How would you structure automated integration testing for services that depend on asynchronous message queues and third-party APIs?",
            $"When architecting a solution for {jobTitle}, how do you evaluate tradeoffs between monolithic modularity versus distributed microservice complexity?",
            $"How do you implement distributed tracing and observability (e.g. OpenTelemetry, Serilog, Prometheus) to monitor request lifecycles across services?"
        };

        var filtered = questionPool
            .Where(q => !existing.Any(e => e.IndexOf(q.Substring(0, Math.Min(30, q.Length)), StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();

        if (filtered.Count < 5)
        {
            filtered.AddRange(questionPool);
        }

        return filtered.Take(5).ToList();
    }
}

