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
public class TicketController : ControllerBase
{
    private readonly NexusDbContext _db;

    public TicketController(NexusDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET /api/ticket
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Ticket>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Ticket>>> GetAll([FromQuery] string? status, [FromQuery] string? search)
    {
        var query = _db.Tickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Status.ToLower() == status.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(t => 
                t.TicketId.ToLower().Contains(searchLower) ||
                t.EmployeeName.ToLower().Contains(searchLower) ||
                t.Department.ToLower().Contains(searchLower) ||
                t.RequestType.ToLower().Contains(searchLower));
        }

        var tickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(tickets);
    }

    /// <summary>
    /// POST /api/ticket
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Ticket), StatusCodes.Status201Created)]
    public async Task<ActionResult<Ticket>> Create([FromBody] CreateTicketRequest request)
    {
        var year = DateTime.UtcNow.Year;
        var num = Random.Shared.Next(1000, 9999);

        var ticket = new Ticket
        {
            TicketId = $"TCK-{year}-{num}",
            EmployeeName = request.EmployeeName ?? "Unknown Employee",
            Department = request.Department ?? "IT",
            RequestType = request.RequestType ?? "Hardware & Software Provisioning",
            Priority = request.Priority ?? "High",
            Status = "Open",
            Details = request.Details ?? $"Provisioning ticket for {request.EmployeeName} ({request.Department}).",
            CreatedAt = DateTime.UtcNow
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = ticket.Id }, ticket);
    }

    /// <summary>
    /// PUT /api/ticket/{id}/status
    /// </summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(Ticket), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Ticket>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound(new { message = $"Ticket with ID {id} not found." });

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            ticket.Status = request.Status;
            await _db.SaveChangesAsync();
        }

        return Ok(ticket);
    }
}

public class CreateTicketRequest
{
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    public string? RequestType { get; set; }
    public string? Priority { get; set; }
    public string? Details { get; set; }
}

public class UpdateStatusRequest
{
    public string? Status { get; set; }
}
