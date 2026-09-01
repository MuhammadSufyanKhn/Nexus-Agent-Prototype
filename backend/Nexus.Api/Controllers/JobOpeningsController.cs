using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Tools.Automation;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/jobs")]
public class JobOpeningsController : ControllerBase
{
    private readonly NexusDbContext _db;
    private readonly IPythonAutomationService _pythonService;
    private readonly ILogger<JobOpeningsController> _logger;

    public JobOpeningsController(
        NexusDbContext db,
        IPythonAutomationService pythonService,
        ILogger<JobOpeningsController> logger)
    {
        _db = db;
        _pythonService = pythonService;
        _logger = logger;
    }

    public class CandidateApplicationDto
    {
        public int Id { get; set; }
        public int JobOpeningId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int ExperienceYears { get; set; }
        public string CoverNote { get; set; } = string.Empty;
        public string CvText { get; set; } = string.Empty;
        public string CvFileName { get; set; } = string.Empty;
        public string Status { get; set; } = "In Progress";
        public int? FitScore { get; set; }
        public string? AiEvaluationJson { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class JobOpeningDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Responsibilities { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string SalaryRange { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public int ApplicationsCount { get; set; }
        public List<CandidateApplicationDto> Applications { get; set; } = new();
    }

    public class CreateJobOpeningRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? Description { get; set; }
        public string? Responsibilities { get; set; }
        public string Requirements { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? SalaryRange { get; set; }
    }

    public class ApplyJobRequest
    {
        public string CandidateName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int ExperienceYears { get; set; }
        public string? CoverNote { get; set; }
        public string CvText { get; set; } = string.Empty;
        public string? CvFileName { get; set; }
        public string? CvPdfData { get; set; }
    }

    public class SendInterviewInvitationRequest
    {
        public string? InterviewDate { get; set; }
        public string? InterviewTime { get; set; }
        public string? Mode { get; set; } = "Online";
        public string? LocationOrLink { get; set; }
        public string? Notes { get; set; }
    }

    public class RejectCandidateRequest
    {
        public string? Stage { get; set; } = "Screening";
        public string? RejectionReason { get; set; }
    }

    /// <summary>
    /// GET /api/jobs - List all job openings with submitted resume/CV count
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<JobOpeningDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<JobOpeningDto>>> GetAll(CancellationToken ct)
    {
        var jobs = await _db.JobOpenings
            .Include(j => j.Applications)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobOpeningDto
            {
                Id = j.Id,
                Title = j.Title,
                Department = j.Department,
                Description = j.Description,
                Responsibilities = j.Responsibilities,
                Requirements = j.Requirements,
                Location = j.Location,
                SalaryRange = j.SalaryRange,
                Status = j.Status,
                CreatedAt = j.CreatedAt,
                ApplicationsCount = j.Applications.Count,
                Applications = j.Applications.OrderByDescending(a => a.SubmittedAt).Select(a => new CandidateApplicationDto
                {
                    Id = a.Id,
                    JobOpeningId = a.JobOpeningId,
                    CandidateName = a.CandidateName,
                    Email = a.Email,
                    Phone = a.Phone,
                    ExperienceYears = a.ExperienceYears,
                    CoverNote = a.CoverNote,
                    CvText = a.CvText,
                    CvFileName = a.CvFileName,
                    Status = a.Status,
                    FitScore = a.FitScore,
                    AiEvaluationJson = a.AiEvaluationJson,
                    SubmittedAt = a.SubmittedAt
                }).ToList()
            })
            .ToListAsync(ct);

        return Ok(jobs);
    }

    /// <summary>
    /// GET /api/jobs/{id} - Get single job opening and its candidate applications
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var job = await _db.JobOpenings
            .Include(j => j.Applications)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job == null) return NotFound(new { message = $"Job opening ID {id} not found." });

        return Ok(new
        {
            job.Id,
            job.Title,
            job.Department,
            job.Description,
            job.Responsibilities,
            job.Requirements,
            job.Location,
            job.SalaryRange,
            job.Status,
            job.CreatedAt,
            applicationsCount = job.Applications.Count,
            applications = job.Applications.OrderByDescending(a => a.SubmittedAt).Select(a => new
            {
                a.Id,
                a.JobOpeningId,
                a.CandidateName,
                a.Email,
                a.Phone,
                a.ExperienceYears,
                a.CoverNote,
                a.CvText,
                a.CvFileName,
                a.CvPdfData,
                a.Status,
                a.FitScore,
                a.AiEvaluationJson,
                a.SubmittedAt
            })
        });
    }

    /// <summary>
    /// POST /api/jobs - Create a new job opening
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobOpeningRequest req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Title))
        {
            return BadRequest(new { message = "Job title is required." });
        }

        var job = new JobOpening
        {
            Title = req.Title.Trim(),
            Department = string.IsNullOrWhiteSpace(req.Department) ? "IT" : req.Department.Trim(),
            Description = req.Description?.Trim() ?? $"Role opening for {req.Title.Trim()}",
            Responsibilities = req.Responsibilities?.Trim() ?? string.Empty,
            Requirements = string.IsNullOrWhiteSpace(req.Requirements) ? "Relevant technical skills and experience" : req.Requirements.Trim(),
            Location = string.IsNullOrWhiteSpace(req.Location) ? "Remote / Hybrid" : req.Location.Trim(),
            SalaryRange = string.IsNullOrWhiteSpace(req.SalaryRange) ? "$70,000 - $95,000" : req.SalaryRange.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        _db.JobOpenings.Add(job);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = job.Id }, new
        {
            message = $"Job opening '{job.Title}' created successfully in Job Opening tab.",
            job.Id,
            job.Title,
            job.Department,
            job.Requirements,
            job.Status,
            applicationLink = $"/apply/{job.Id}"
        });
    }

    /// <summary>
    /// DELETE /api/jobs/{id} - Delete or close job opening
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var job = await _db.JobOpenings.FindAsync(new object[] { id }, ct);
        if (job == null) return NotFound(new { message = $"Job opening {id} not found." });

        _db.JobOpenings.Remove(job);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = $"Job opening '{job.Title}' deleted successfully." });
    }

    /// <summary>
    /// POST /api/jobs/{id}/apply - Public candidate application endpoint
    /// Candidates enter details, attach/paste CV, and submit
    /// </summary>
    [HttpPost("{id:int}/apply")]
    public async Task<IActionResult> Apply(int id, [FromBody] ApplyJobRequest req, CancellationToken ct)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CandidateName) || string.IsNullOrWhiteSpace(req.Email))
        {
            return BadRequest(new { message = "Candidate full name and email are required." });
        }

        var job = await _db.JobOpenings.FindAsync(new object[] { id }, ct);
        if (job == null) return NotFound(new { message = $"Job opening ID {id} not found." });

        var cvContent = !string.IsNullOrWhiteSpace(req.CvText) ? req.CvText : $"Candidate: {req.CandidateName}\nEmail: {req.Email}\nExperience: {req.ExperienceYears} years\nCover Note: {req.CoverNote}";

        if (!string.IsNullOrWhiteSpace(req.CvPdfData))
        {
            try
            {
                var base64Part = req.CvPdfData;
                if (base64Part.Contains(","))
                {
                    base64Part = base64Part.Substring(base64Part.IndexOf(",") + 1);
                }
                var pdfBytes = Convert.FromBase64String(base64Part);
                using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfBytes);
                var sb = new StringBuilder();
                foreach (var page in doc.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                var extracted = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    cvContent = extracted;
                }
            }
            catch
            {
                // Fallback to provided text if PDF extraction fails
            }
        }

        var application = new CandidateApplication
        {
            JobOpeningId = id,
            CandidateName = req.CandidateName.Trim(),
            Email = req.Email.Trim(),
            Phone = req.Phone?.Trim() ?? string.Empty,
            ExperienceYears = req.ExperienceYears > 0 ? req.ExperienceYears : 1,
            CoverNote = req.CoverNote?.Trim() ?? string.Empty,
            CvText = cvContent,
            CvFileName = string.IsNullOrWhiteSpace(req.CvFileName) ? $"{req.CandidateName.Replace(" ", "_")}_Resume.pdf" : req.CvFileName,
            CvPdfData = req.CvPdfData,
            Status = "In Progress",
            SubmittedAt = DateTime.UtcNow
        };

        _db.CandidateApplications.Add(application);
        await _db.SaveChangesAsync(ct);

        // Get total count of applications for this job opening
        var totalCount = await _db.CandidateApplications.CountAsync(a => a.JobOpeningId == id, ct);

        // Trigger automated candidate application acknowledgment email via Python automation
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = application.CandidateName,
                    email = application.Email,
                    position = job.Title,
                    department = job.Department
                });
                await _pythonService.ExecuteOperationAsync("email.application_acknowledgment", payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send automated candidate acknowledgment email to {Email}", application.Email);
            }
        });

        return Ok(new
        {
            message = "Application submitted successfully! Your details and CV have been received for HR screening.",
            applicationId = application.Id,
            jobTitle = job.Title,
            candidateName = application.CandidateName,
            submittedAt = application.SubmittedAt,
            jobTotalApplicationsCount = totalCount
        });
    }

    /// <summary>
    /// GET /api/jobs/{id}/applications - Get candidate applications for a specific job
    /// </summary>
    [HttpGet("{id:int}/applications")]
    public async Task<IActionResult> GetApplications(int id, CancellationToken ct)
    {
        var applications = await _db.CandidateApplications
            .Where(a => a.JobOpeningId == id)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(ct);

        return Ok(applications);
    }

    /// <summary>
    /// GET /api/jobs/applications - Get all submitted candidate applications across all jobs
    /// </summary>
    [HttpGet("applications")]
    public async Task<IActionResult> GetAllApplications(CancellationToken ct)
    {
        var applications = await _db.CandidateApplications
            .Include(a => a.JobOpening)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new
            {
                a.Id,
                a.JobOpeningId,
                jobTitle = a.JobOpening != null ? a.JobOpening.Title : "General Opening",
                department = a.JobOpening != null ? a.JobOpening.Department : "IT",
                a.CandidateName,
                a.Email,
                a.Phone,
                a.ExperienceYears,
                a.CoverNote,
                a.CvText,
                a.CvFileName,
                a.CvPdfData,
                a.Status,
                a.FitScore,
                a.AiEvaluationJson,
                a.SubmittedAt
            })
            .ToListAsync(ct);

        return Ok(applications);
    }

    /// <summary>
    /// POST /api/jobs/applications/{id}/shortlist - Shortlist a candidate for interview
    /// </summary>
    [HttpPost("applications/{id:int}/shortlist")]
    public async Task<IActionResult> ShortlistCandidate(int id, CancellationToken ct)
    {
        var application = await _db.CandidateApplications
            .Include(a => a.JobOpening)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (application == null)
            return NotFound(new { message = $"Candidate application ID {id} not found." });

        application.Status = "Shortlisted";
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            message = $"Candidate {application.CandidateName} has been shortlisted for {application.JobOpening?.Title ?? "the position"}.",
            applicationId = application.Id,
            status = application.Status
        });
    }

    /// <summary>
    /// POST /api/jobs/applications/{id}/send-interview-invitation - Schedule interview and dispatch automated invitation email
    /// </summary>
    [HttpPost("applications/{id:int}/send-interview-invitation")]
    public async Task<IActionResult> SendInterviewInvitation(int id, [FromBody] SendInterviewInvitationRequest req, CancellationToken ct)
    {
        var application = await _db.CandidateApplications
            .Include(a => a.JobOpening)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (application == null)
            return NotFound(new { message = $"Candidate application ID {id} not found." });

        var job = application.JobOpening;
        var date = !string.IsNullOrWhiteSpace(req.InterviewDate) ? req.InterviewDate.Trim() : DateTime.UtcNow.AddDays(3).ToString("MMMM dd, yyyy");
        var time = !string.IsNullOrWhiteSpace(req.InterviewTime) ? req.InterviewTime.Trim() : "11:00 AM PKT";
        var mode = !string.IsNullOrWhiteSpace(req.Mode) ? req.Mode.Trim() : "Online";
        var locationOrLink = !string.IsNullOrWhiteSpace(req.LocationOrLink)
            ? req.LocationOrLink.Trim()
            : (mode.ToLower().Contains("online") ? "https://meet.google.com/nex-us-rec" : "Nexus Enterprise Tech Tower, Level 4, IT Wing");

        // Update application status
        application.Status = "Interview Scheduled";
        await _db.SaveChangesAsync(ct);

        // Execute Python email service asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = application.CandidateName,
                    email = application.Email,
                    position = job?.Title ?? "Open Position",
                    department = job?.Department ?? "IT",
                    interviewDate = date,
                    interviewTime = time,
                    mode = mode,
                    locationOrLink = locationOrLink,
                    notes = req.Notes?.Trim() ?? string.Empty
                });
                await _pythonService.ExecuteOperationAsync("email.interview_invitation", payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send interview invitation email to {Email}", application.Email);
            }
        });

        return Ok(new
        {
            message = $"Interview invitation successfully sent to {application.CandidateName} ({application.Email}).",
            applicationId = application.Id,
            status = application.Status,
            interviewDetails = new
            {
                date,
                time,
                mode,
                locationOrLink
            }
        });
    }

    /// <summary>
    /// POST /api/jobs/applications/{id}/reject - Reject a candidate application and dispatch rejection notification email
    /// </summary>
    [HttpPost("applications/{id:int}/reject")]
    public async Task<IActionResult> RejectCandidate(int id, [FromBody] RejectCandidateRequest? req, CancellationToken ct)
    {
        var application = await _db.CandidateApplications
            .Include(a => a.JobOpening)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (application == null)
            return NotFound(new { message = $"Candidate application ID {id} not found." });

        var stage = req?.Stage?.Trim().ToLowerInvariant() ?? "screening";
        application.Status = "Rejected";
        await _db.SaveChangesAsync(ct);

        var job = application.JobOpening;
        var operationName = stage.Contains("interview") ? "email.interview_rejection" : "email.screening_rejection";

        // Dispatch email notification asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    name = application.CandidateName,
                    email = application.Email,
                    position = job?.Title ?? "Open Position",
                    department = job?.Department ?? "IT",
                    stage = stage
                });
                await _pythonService.ExecuteOperationAsync(operationName, payload, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch rejection email to {Email}", application.Email);
            }
        });

        return Ok(new
        {
            message = $"Candidate {application.CandidateName} application rejected. Notification email dispatched to {application.Email}.",
            applicationId = application.Id,
            status = application.Status,
            stage = stage
        });
    }
}
