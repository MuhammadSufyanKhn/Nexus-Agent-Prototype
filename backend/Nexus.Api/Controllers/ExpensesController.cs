using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;
using Nexus.Data.Services;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExpenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetAll(
        [FromQuery] int? employeeId, 
        [FromQuery] ExpenseStatus? status,
        [FromQuery] string? complianceStatus)
    {
        var expenses = await _expenseService.GetAllAsync(employeeId, status, complianceStatus);
        return Ok(expenses);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> GetById(int id)
    {
        var expense = await _expenseService.GetByIdAsync(id);
        if (expense == null) return NotFound(new { message = $"Expense claim with ID {id} not found." });
        return Ok(expense);
    }

    [HttpGet("claim/{claimNumber}")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> GetByClaimNumber(string claimNumber)
    {
        var expense = await _expenseService.GetByClaimNumberAsync(claimNumber);
        if (expense == null) return NotFound(new { message = $"Expense claim {claimNumber} not found." });
        return Ok(expense);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpenseDto>> Create([FromBody] CreateExpenseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await _expenseService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> Update(int id, [FromBody] UpdateExpenseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _expenseService.UpdateAsync(id, dto);
        if (updated == null) return NotFound(new { message = $"Expense claim with ID {id} not found." });
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id)
    {
        var deleted = await _expenseService.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Expense claim with ID {id} not found." });
        return NoContent();
    }

    [HttpPost("audit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> AuditExpensesWithAI()
    {
        var result = await _expenseService.RunComplianceSweepAsync();
        return Ok(result);
    }

    [HttpGet("analytics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMonthlyAnalytics([FromQuery] DateTime? date)
    {
        var result = await _expenseService.GetMonthlyAnalyticsAsync(date);
        return Ok(result);
    }

    [HttpGet("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetCategoryAnalytics()
    {
        var result = await _expenseService.GetCategoryAnalyticsAsync();
        return Ok(result);
    }

    [HttpGet("variance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPolicyVariance([FromQuery] string category = "Travel")
    {
        var result = await _expenseService.GetPolicyVarianceAsync(category);
        return Ok(result);
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> UpdateStatus(int id, [FromBody] UpdateExpenseStatusRequest request)
    {
        var existing = await _expenseService.GetByIdAsync(id);
        if (existing == null) return NotFound(new { message = $"Expense claim with ID {id} not found." });

        ExpenseStatus status = ExpenseStatus.Pending;
        string compliance = "Pending";

        if (request.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            status = ExpenseStatus.Approved;
            compliance = "Compliant";
        }
        else if (request.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            status = ExpenseStatus.Rejected;
            compliance = "Rejected";
        }
        else if (request.Status.Equals("Flagged", StringComparison.OrdinalIgnoreCase) || request.Status.Equals("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            status = ExpenseStatus.NonCompliant;
            compliance = "Flagged";
        }

        var updated = await _expenseService.UpdateAsync(id, new UpdateExpenseDto
        {
            Status = status,
            ComplianceStatus = compliance,
            FlagReason = request.Reason ?? (status == ExpenseStatus.Approved ? "Approved by Manager" : (status == ExpenseStatus.Rejected ? "Policy violation" : "Flagged for manager review")),
            ReviewedBy = "System Administrator",
            ReviewedDate = DateTime.UtcNow
        });

        return Ok(updated);
    }
}

public class UpdateExpenseStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
