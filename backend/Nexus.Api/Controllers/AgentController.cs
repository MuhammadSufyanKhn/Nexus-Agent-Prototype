using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Agent.Orchestration;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;

    public AgentController(IAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// POST /api/agent/execute - Execute natural language prompt through Nexus Agent Orchestrator
    /// </summary>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(AgentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentResult>> ExecutePrompt([FromBody] ExecuteAgentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _orchestrator.ExecuteAsync(request.Prompt, request.UserId);
        return Ok(result);
    }
}
