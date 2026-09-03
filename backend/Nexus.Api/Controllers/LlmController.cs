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

    /// <summary>
    /// POST /api/llm/suggestions - Generates Gemini AI-powered prompt completions
    /// </summary>
    [HttpPost("suggestions")]
    public async Task<IActionResult> GetGeminiSuggestions([FromBody] SuggestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
            return Ok(Array.Empty<object>());

        var systemPrompt = @"You are Gemini AI Copilot for Nexus HR Enterprise. 
Given a user's partial prompt input, generate 3 high-quality, professional, policy-compliant HR/Finance command completions.
Return ONLY a JSON array of objects with keys: ""label"", ""completedText"", ""category"".
Categories must be one of: ""Talent Acquisition"", ""Budget Management"", ""Employee Management"", ""Expense Review"", ""HR Governance"".
Example format:
[
  { ""label"": ""Onboard New Employee"", ""completedText"": ""Onboard Sarah Jenkins as Senior Developer in IT at $120,000"", ""category"": ""Talent Acquisition"" }
]";

        var response = await _llmService.GenerateCompletionAsync(
            prompt: $"Generate 3 Copilot completions for input: '{request.Input}'",
            systemPrompt: systemPrompt,
            requestJson: true
        );

        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.Content))
        {
            return Content(response.Content, "application/json");
        }

        return Ok(Array.Empty<object>());
    }
}

public class TestPromptRequest
{
    [Required]
    public string Prompt { get; set; } = string.Empty;

    public string? SystemPrompt { get; set; }

    public bool RequestJson { get; set; } = false;
}

public class SuggestionRequest
{
    public string Input { get; set; } = string.Empty;
}
