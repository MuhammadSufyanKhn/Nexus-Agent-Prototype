using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CvController : ControllerBase
{
    private readonly IToolRegistry _toolRegistry;

    public CvController(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public class CvAnalysisRequest
    {
        public string CvContent { get; set; } = string.Empty;
        public string? JobTitle { get; set; } = ".NET Developer";
        public string? RequiredSkills { get; set; } = "C#, .NET Core, SQL Server, Entity Framework, REST API";
    }

    /// <summary>
    /// POST /api/cv/analyze - Parse CV content and return structured candidate score & evaluation
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(CvCandidateAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeCv([FromBody] CvAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CvContent))
        {
            return BadRequest(new { message = "CV text content is required for analysis." });
        }

        var tool = _toolRegistry.GetTool("cv.analyze");
        if (tool == null)
        {
            return BadRequest(new { message = "CV Analysis tool 'cv.analyze' is not registered." });
        }

        var ctx = new ToolExecutionContext
        {
            AgentRunId = Guid.NewGuid(),
            UserId = 1,
            UserRole = "Admin",
            ArgumentsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                cvContent = request.CvContent,
                jobTitle = request.JobTitle ?? ".NET Developer",
                requiredSkills = request.RequiredSkills ?? "C#, .NET Core, SQL Server, Entity Framework, REST API"
            })
        };

        var result = await tool.ExecuteAsync(ctx);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.Data);
    }
}
