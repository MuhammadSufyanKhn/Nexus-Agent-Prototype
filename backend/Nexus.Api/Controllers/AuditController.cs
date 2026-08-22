using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly NexusDbContext _db;

    public AuditController(NexusDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/audit/logs - Retrieve cryptographically linked SHA-256 security audit logs
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs()
    {
        var logs = await _db.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .ToListAsync();

        var result = logs.Select(a => new
        {
            id = a.Id,
            user = string.IsNullOrEmpty(a.User?.Name) ? "HR Admin" : a.User.Name,
            action = a.Action,
            resource = a.Target,
            tool = a.ToolName,
            timestamp = a.Timestamp,
            hash = string.IsNullOrEmpty(a.CurrentHash) ? "0000000000000000000000000000000000000000000000000000000000000000" : a.CurrentHash
        });

        return Ok(result);
    }
}
