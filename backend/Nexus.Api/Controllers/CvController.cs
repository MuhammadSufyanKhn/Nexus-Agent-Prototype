using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.LLM;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using UglyToad.PdfPig;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CvController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;
    private readonly NexusDbContext _db;
    private readonly ILLMService? _llmService;
    private readonly Microsoft.Extensions.Logging.ILogger<CvController>? _logger;

    public CvController(IToolRegistry toolRegistry, NexusDbContext db, ILLMService? llmService = null, Microsoft.Extensions.Logging.ILogger<CvController>? logger = null)
    {
        _toolRegistry = toolRegistry;
        _db = db;
        _llmService = llmService;
        _logger = logger;
    }

    public class CvAnalysisRequest
    {
        public string? CvContent { get; set; }
        public string? JobTitle { get; set; }
        public string? RequiredSkills { get; set; }
        public int? JobOpeningId { get; set; }
        public int? CandidateId { get; set; }
    }

    /// <summary>
    /// POST /api/cv/analyze - Parse CV content, score against job description, and evaluate Best Fit
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(CvCandidateAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeCv([FromBody] CvAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        string cvText = request?.CvContent ?? string.Empty;
        string jobTitle = request?.JobTitle ?? ".NET Developer";
        string requiredSkills = request?.RequiredSkills ?? "C#, .NET Core, SQL Server, Entity Framework, REST API";
        Nexus.Data.Entities.CandidateApplication? candidate = null;

        // If JobOpeningId is specified, fetch requirements and title from authoritative JobOpening
        if (request?.JobOpeningId is { } jId && jId > 0)
        {
            var job = await _db.JobOpenings.FindAsync(new object[] { jId }, cancellationToken);
            if (job != null)
            {
                jobTitle = job.Title;
                if (!string.IsNullOrWhiteSpace(job.Requirements))
                {
                    requiredSkills = job.Requirements;
                }
            }
        }

        // If CandidateId is specified, fetch CV text from CandidateApplication
        if (request?.CandidateId is { } cId && cId > 0)
        {
            candidate = await _db.CandidateApplications.FindAsync(new object[] { cId }, cancellationToken);
            if (candidate != null)
            {
                if (string.IsNullOrWhiteSpace(cvText))
                {
                    cvText = candidate.CvText;
                }

                if (string.IsNullOrWhiteSpace(cvText) && !string.IsNullOrWhiteSpace(candidate.CvPdfData))
                {
                    try
                    {
                        var base64Part = candidate.CvPdfData;
                        if (base64Part.Contains(",")) base64Part = base64Part.Substring(base64Part.IndexOf(",") + 1);
                        var pdfBytes = Convert.FromBase64String(base64Part);
                        using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
                        var sb = new StringBuilder();
                        foreach (var page in doc.GetPages()) sb.AppendLine(page.Text);
                        var extracted = sb.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(extracted)) cvText = extracted;
                    }
                    catch {}
                }
            }
        }

        if (string.IsNullOrWhiteSpace(cvText))
        {
            return BadRequest(new { message = "CV text content is required for analysis." });
        }

        CvCandidateAnalysisResult? analysis = null;

        // 1. Invoke Gemini LLM for dynamic, deep CV evaluation & tailored role-specific questions
        if (_llmService != null)
        {
            try
            {
                var systemPrompt = @"You are NEXUS ENTERPRISE AI TALENT ASSESSOR powered by Google Gemini.
Analyze the candidate's CV strictly and truthfully against the target job title and required technical competencies.

CRITICAL ASSESSMENT GUIDELINES:
1. Honest Gap-Aware Scoring:
   - Compare candidate's verified skills and experience duration against the REQUIRED COMPETENCIES.
   - If the candidate lacks all or most required competencies (for example: applying for Lead Cloud Architect with AWS, Kubernetes, Terraform, Docker, CI/CD, but the candidate is a student/intern with only .NET/React skills and NO cloud infrastructure experience), matchScore MUST be low (0% to 30%), fitCategory must be 'Not a Fit', isBestFit must be false, and recommendation 'NOT RECOMMENDED'.
   - If the candidate satisfies 75%+ of required competencies with relevant years of experience, matchScore should be 80% to 95%, fitCategory 'Best Fit' or 'Strong Match', and recommendation 'RECOMMENDED'.
2. Role-Specific Dynamic Questions:
   - Formulate EXACTLY 5 tailored technical interview questions.
   - Deeply reference the candidate's actual projects, technologies mentioned, and address their specific gaps for this target position.
   - Do NOT use generic or recycled questions.

Return ONLY a valid JSON object matching this schema:
{
  ""candidateName"": string,
  ""email"": string,
  ""targetPosition"": string,
  ""experienceYears"": number,
  ""extractedSkills"": string[],
  ""matchScore"": number,
  ""recommendation"": ""RECOMMENDED"" | ""CONSIDER"" | ""NOT RECOMMENDED"",
  ""fitCategory"": ""Best Fit"" | ""Strong Match"" | ""Moderate Fit"" | ""Not a Fit"",
  ""isBestFit"": boolean,
  ""fitSummary"": string,
  ""strengths"": string[],
  ""weaknesses"": string[],
  ""missingSkills"": string[],
  ""recommendedInterviewQuestions"": string[]
}";

                var prompt = $"TARGET ROLE: {jobTitle}\nREQUIRED COMPETENCIES: {requiredSkills}\n\nCANDIDATE CV CONTENT:\n---\n{cvText}\n---\n\nEvaluate matchScore honestly based on required competencies and generate 5 dynamic interview questions.";

                analysis = await _llmService.GenerateJsonAsync<CvCandidateAnalysisResult>(prompt, systemPrompt, cancellationToken);
                if (analysis != null)
                {
                    _logger?.LogInformation("Gemini CV evaluation completed successfully for candidate '{Name}', MatchScore={Score}%", analysis.CandidateName, analysis.MatchScore);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Gemini LLM CV evaluation failed; invoking fallback tool.");
            }
        }

        // 2. Deterministic fallback if LLM call was null or failed
        if (analysis == null)
        {
            analysis = CvAnalysisTool.AnalyzeCvText(cvText, jobTitle, requiredSkills);
        }

        // Ensure real candidate name & email if available from DB
        if (candidate != null)
        {
            if (string.IsNullOrWhiteSpace(analysis.CandidateName) || analysis.CandidateName.Equals("Candidate", StringComparison.OrdinalIgnoreCase))
            {
                analysis.CandidateName = candidate.CandidateName;
            }
            if (string.IsNullOrWhiteSpace(analysis.Email) && !string.IsNullOrWhiteSpace(candidate.Email))
            {
                analysis.Email = candidate.Email;
            }
            if (analysis.ExperienceYears <= 0 && candidate.ExperienceYears > 0)
            {
                analysis.ExperienceYears = candidate.ExperienceYears;
            }

            candidate.FitScore = analysis.MatchScore;
            candidate.Status = analysis.IsBestFit ? "Best Fit" : analysis.MatchScore >= 60 ? "Shortlisted" : "Reviewed";
            candidate.AiEvaluationJson = JsonSerializer.Serialize(analysis);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(analysis);
    }

    public class RefreshQuestionsRequest
    {
        public string? CvContent { get; set; }
        public string? JobTitle { get; set; }
        public string? RequiredSkills { get; set; }
        public int? JobOpeningId { get; set; }
        public int? CandidateId { get; set; }
        public List<string>? ExistingQuestions { get; set; }
    }

    public class RefreshQuestionsResult
    {
        public List<string> Questions { get; set; } = new();
    }

    /// <summary>
    /// POST /api/cv/refresh-questions - Dynamically generate 5 fresh interview questions avoiding previously asked ones
    /// </summary>
    [HttpPost("refresh-questions")]
    [ProducesResponseType(typeof(RefreshQuestionsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshQuestions([FromBody] RefreshQuestionsRequest request, CancellationToken cancellationToken = default)
    {
        string cvText = request?.CvContent ?? string.Empty;
        string jobTitle = request?.JobTitle ?? ".NET Developer";
        string requiredSkills = request?.RequiredSkills ?? "C#, .NET Core, SQL Server, Entity Framework, REST API";

        if (request?.JobOpeningId is { } jId && jId > 0)
        {
            var job = await _db.JobOpenings.FindAsync(new object[] { jId }, cancellationToken);
            if (job != null)
            {
                jobTitle = job.Title;
                if (!string.IsNullOrWhiteSpace(job.Requirements)) requiredSkills = job.Requirements;
            }
        }

        if (request?.CandidateId is { } cId && cId > 0 && string.IsNullOrWhiteSpace(cvText))
        {
            var cand = await _db.CandidateApplications.FindAsync(new object[] { cId }, cancellationToken);
            if (cand != null && !string.IsNullOrWhiteSpace(cand.CvText)) cvText = cand.CvText;
        }

        var existing = request?.ExistingQuestions ?? new List<string>();
        var existingListStr = existing.Count > 0 ? string.Join("\n- ", existing) : "None";

        List<string>? newQuestions = null;

        if (_llmService != null)
        {
            try
            {
                var systemPrompt = @"You are NEXUS ENTERPRISE AI TALENT ASSESSOR powered by Google Gemini.
Your task is to generate 5 completely NEW, fresh, role-specific technical interview questions.
CRITICAL CONSTRAINT: Do NOT repeat, rephrase, or duplicate any of the previously generated questions.
Explore other deep topics such as:
- Production troubleshooting & real-world outage debugging
- High-concurrency architecture and edge-case system design
- Database transactions, distributed caching, and state synchronization
- Automated CI/CD pipelines, container orchestration, and security vulnerabilities
- Practical coding tradeoffs based on the candidate's actual projects.

Return ONLY a valid JSON object matching this schema:
{
  ""questions"": string[] (EXACTLY 5 unique questions)
}";

                var prompt = $"TARGET ROLE: {jobTitle}\nREQUIRED COMPETENCIES: {requiredSkills}\n\nCANDIDATE RESUME CONTENT:\n---\n{cvText}\n---\n\nPREVIOUSLY ASKED QUESTIONS (DO NOT DUPLICATE THESE):\n- {existingListStr}\n\nGenerate 5 brand new, highly specific technical interview questions.";

                var res = await _llmService.GenerateJsonAsync<RefreshQuestionsResult>(prompt, systemPrompt, cancellationToken);
                if (res?.Questions != null && res.Questions.Count > 0)
                {
                    newQuestions = res.Questions;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Gemini failed to refresh questions; falling back to dynamic generator.");
            }
        }

        if (newQuestions == null || newQuestions.Count == 0)
        {
            newQuestions = CvAnalysisTool.GenerateAlternativeInterviewQuestions(cvText, jobTitle, requiredSkills, existing);
        }

        return Ok(new RefreshQuestionsResult { Questions = newQuestions });
    }
}
