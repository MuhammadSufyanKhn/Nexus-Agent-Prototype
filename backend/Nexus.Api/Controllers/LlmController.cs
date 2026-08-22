using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Data.LLM;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LlmController : ControllerBase
{
    private readonly ILLMService _llmService;

    public LlmController(ILLMService llmService)
    {
        _llmService = llmService;
    }

    /// <summary>
    /// GET /api/llm/health - Health check for local open-source LLM runtime
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(LLMHealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LLMHealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth()
    {
        var health = await _llmService.CheckHealthAsync();
        return Ok(health);
    }

    /// <summary>
    /// POST /api/llm/test - Controlled test prompt to local open-source model
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(LLMResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LLMResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestPrompt([FromBody] TestPromptRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var response = await _llmService.GenerateCompletionAsync(
            prompt: request.Prompt,
            systemPrompt: request.SystemPrompt ?? "You are NEXUS-AGENT LITE local reasoning assistant. Keep responses brief and precise.",
            requestJson: request.RequestJson
        );

        if (!response.IsSuccess)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }
}

public class TestPromptRequest
{
    [Required]
    public string Prompt { get; set; } = string.Empty;

    public string? SystemPrompt { get; set; }

    public bool RequestJson { get; set; } = false;
}
