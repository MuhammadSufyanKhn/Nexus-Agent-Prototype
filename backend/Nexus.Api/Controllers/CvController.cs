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

    public CvController(IToolRegistry toolRegistry, NexusDbContext db, ILLMService? llmService = null)
    {
        _toolRegistry = toolRegistry;
        _db = db;
        _llmService = llmService;
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
            var candidate = await _db.CandidateApplications.FindAsync(new object[] { cId }, cancellationToken);
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
                var systemPrompt = @"You are NEXUS ENTERPRISE AI TALENT ASSESSOR.
Analyze the candidate's CV strictly against the specified target job title and required technical competencies.
Generate a comprehensive, truthful JSON assessment adhering to this exact schema:
{
  ""candidateName"": string,
  ""email"": string,
  ""targetPosition"": string,
  ""experienceYears"": number,
  ""extractedSkills"": string[],
  ""matchScore"": number (0 to 100),
  ""recommendation"": ""RECOMMENDED"" | ""CONSIDER"" | ""NOT RECOMMENDED"",
  ""fitCategory"": ""Best Fit"" | ""Strong Match"" | ""Moderate Fit"" | ""Not a Fit"",
  ""isBestFit"": boolean (true if matchScore >= 80 and candidate satisfies core skills),
  ""fitSummary"": string (2-3 detailed paragraphs analyzing experience, achievements, and position alignment),
  ""strengths"": string[] (3-5 concrete strengths identified directly in CV),
  ""weaknesses"": string[] (1-3 minor weaknesses or risk areas),
  ""missingSkills"": string[] (skills required by job that are missing or weak in CV),
  ""recommendedInterviewQuestions"": string[] (EXACTLY 5 deep, role-specific questions based on candidate's actual projects, claims, and skill gaps — NO generic questions)
}";

                var prompt = $"TARGET ROLE: {jobTitle}\nREQUIRED COMPETENCIES: {requiredSkills}\n\nCANDIDATE CV CONTENT:\n---\n{cvText}\n---\n\nReturn only valid JSON matching the requested structure.";

                analysis = await _llmService.GenerateJsonAsync<CvCandidateAnalysisResult>(prompt, systemPrompt, cancellationToken);
            }
            catch
            {
                // Fall through to deterministic fallback if LLM is unavailable or rate-limited
            }
        }

        // 2. Deterministic fallback if LLM call was null or failed
        if (analysis == null)
        {
            analysis = CvAnalysisTool.AnalyzeCvText(cvText, jobTitle, requiredSkills);
        }

        if (request?.CandidateId is { } candId && candId > 0)
        {
            var candidate = await _db.CandidateApplications.FindAsync(new object[] { candId }, cancellationToken);
            if (candidate != null)
            {
                candidate.FitScore = analysis.MatchScore;
                candidate.Status = analysis.IsBestFit ? "Best Fit" : analysis.MatchScore >= 60 ? "Shortlisted" : "Reviewed";
                candidate.AiEvaluationJson = JsonSerializer.Serialize(analysis);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(analysis);
    }
}
