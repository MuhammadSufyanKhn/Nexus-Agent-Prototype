using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaveController : ControllerBase
{
    private readonly NexusDbContext _db;

    public LeaveController(NexusDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllLeaves([FromQuery] int? employeeId, [FromQuery] string? status)
    {
        var query = _db.Leaves.Include(l => l.Employee).ThenInclude(e => e.Department).AsNoTracking().AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(l => l.EmployeeId == employeeId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, true, out var lStatus))
            query = query.Where(l => l.Status == lStatus);

        var leaves = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

        return Ok(leaves.Select(l => new
        {
            l.Id,
            l.EmployeeId,
            EmployeeName = l.Employee?.Name ?? "Unknown",
            DepartmentName = l.Employee?.Department?.Name ?? "Unknown",
            LeaveType = l.LeaveType.ToString(),
            StartDate = l.StartDate.ToString("yyyy-MM-dd"),
            EndDate = l.EndDate.ToString("yyyy-MM-dd"),
            Status = l.Status.ToString(),
            l.ApprovedBy,
            l.Notes,
            l.CreatedAt
        }));
    }

    [HttpPost]
    public async Task<IActionResult> CreateLeave([FromBody] CreateLeaveRequest req)
    {
        var emp = await _db.Employees.FindAsync(req.EmployeeId);
        if (emp == null) return NotFound($"Employee with ID {req.EmployeeId} not found.");

        Enum.TryParse<LeaveType>(req.LeaveType, true, out var lType);

        var leave = new Leave
        {
            EmployeeId = emp.Id,
            LeaveType = lType,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Status = LeaveStatus.Approved,
            ApprovedBy = req.ApprovedBy ?? "HR Manager",
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };

        _db.Leaves.Add(leave);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAllLeaves), new { id = leave.Id }, new
        {
            leave.Id,
            EmployeeName = emp.Name,
            LeaveType = leave.LeaveType.ToString(),
            StartDate = leave.StartDate.ToString("yyyy-MM-dd"),
            EndDate = leave.EndDate.ToString("yyyy-MM-dd"),
            Status = leave.Status.ToString()
        });
    }

    [HttpPut("{id}/approve")]
    public async Task<IActionResult> ApproveLeave(int id, [FromQuery] string approvedBy = "HR Manager")
    {
        var leave = await _db.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == id);
        if (leave == null) return NotFound($"Leave request #{id} not found.");

        leave.Status = LeaveStatus.Approved;
        leave.ApprovedBy = approvedBy;
        leave.ProcessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { message = $"Leave request #{id} approved by {approvedBy}.", leave.Id, status = leave.Status.ToString() });
    }
}

public class CreateLeaveRequest
{
    public int EmployeeId { get; set; }
    public string LeaveType { get; set; } = "Sick";
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }
}
