using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Agent.Intent;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IntentController : ControllerBase
{
    private readonly IIntentParser _intentParser;

    public IntentController(IIntentParser intentParser)
    {
        _intentParser = intentParser;
    }

    /// <summary>
    /// POST /api/intent/parse - Convert natural language request into structured validated intent
    /// </summary>
    [HttpPost("parse")]
    [ProducesResponseType(typeof(ParsedIntentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParsedIntentResult>> ParseIntent([FromBody] ParseIntentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _intentParser.ParseIntentAsync(request.Prompt);
        return Ok(result);
    }
}
