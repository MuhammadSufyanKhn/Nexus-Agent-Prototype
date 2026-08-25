using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Enums;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PayrollController : ControllerBase
{
    private readonly NexusDbContext _db;

    public PayrollController(NexusDbContext db)
    {
        _db = db;
    }

    [HttpPost("hold")]
    public async Task<IActionResult> HoldPayroll([FromBody] PayrollHoldRequest req)
    {
        var count = await _db.Employees.CountAsync(e => e.Status == EmployeeStatus.Active);
        return Ok(new
        {
            message = $"Payroll hold applied for division '{req.Division ?? "ALL"}'. Affected employees: {count}.",
            action = "PAYROLL_HOLD",
            division = req.Division ?? "ALL",
            reason = req.Reason ?? "Payroll hold directive"
        });
    }

    [HttpPost("bonus")]
    public async Task<IActionResult> DistributeBonus([FromBody] PayrollBonusRequest req)
    {
        var count = await _db.Employees.CountAsync(e => e.Status == EmployeeStatus.Active);
        var totalAmount = count * (req.BonusAmount ?? 1000m);
        return Ok(new
        {
            message = $"Bonus distribution of ${req.BonusAmount ?? 1000m:N2} processed for {count} employee(s). Total payout: ${totalAmount:N2}.",
            action = "BULK_BONUS",
            affectedCount = count,
            totalAmount
        });
    }
}

public class PayrollHoldRequest
{
    public string? Division { get; set; }
    public string? Reason { get; set; }
}

public class PayrollBonusRequest
{
    public string? Division { get; set; }
    public decimal? BonusAmount { get; set; }
    public decimal? BonusPercent { get; set; }
}
