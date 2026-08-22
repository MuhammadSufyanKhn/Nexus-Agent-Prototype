using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Agent.Events;
using Nexus.Data.Events;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentFeedController : ControllerBase
{
    private readonly IAgentEventBroadcaster _eventBroadcaster;

    public AgentFeedController(IAgentEventBroadcaster eventBroadcaster)
    {
        _eventBroadcaster = eventBroadcaster;
    }

    /// <summary>
    /// GET /api/agentfeed/runs/{runId}/events - Retrieve real-time activity events for an agent run
    /// </summary>
    [HttpGet("runs/{runId}/events")]
    [ProducesResponseType(typeof(IEnumerable<AgentActivityEvent>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentActivityEvent>>> GetRunEvents(Guid runId)
    {
        var events = await _eventBroadcaster.GetRunEventsAsync(runId);
        return Ok(events);
    }
}
