using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Services;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;

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
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> GetAll([FromQuery] int? employeeId, [FromQuery] ExpenseStatus? status)
    {
        var expenses = await _expenseService.GetAllAsync(employeeId, status);
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

    [HttpPost("audit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> AuditExpensesWithAI([FromServices] NexusDbContext db)
    {
        var expenses = await db.Expenses.Include(e => e.Employee).ToListAsync();
        var flagged = new List<object>();
        int compliantCount = 0;
        int violationCount = 0;

        foreach (var exp in expenses)
        {
            // Corporate policy POL-FIN-002 rules:
            // Meal cap: $50.00, Travel cap: $250.00, Software/Equipment cap: $500.00
            decimal limit = exp.ExpenseType switch
            {
                ExpenseType.Meal => 50.00m,
                ExpenseType.Travel => 250.00m,
                ExpenseType.Software => 500.00m,
                ExpenseType.Equipment => 500.00m,
                _ => 100.00m
            };

            bool isViolation = exp.Amount > limit;
            if (isViolation)
            {
                exp.Status = ExpenseStatus.NonCompliant;
                violationCount++;
                flagged.Add(new
                {
                    expenseId = exp.Id,
                    employeeName = exp.Employee?.Name ?? $"Employee #{exp.EmployeeId}",
                    amount = exp.Amount,
                    policyLimit = limit,
                    overflow = exp.Amount - limit,
                    policyCode = "POL-FIN-002",
                    reason = $"Claim of ${exp.Amount:F2} exceeds allowed {exp.ExpenseType} policy cap of ${limit:F2}."
                });
            }
            else
            {
                if (exp.Status == ExpenseStatus.Pending)
                {
                    exp.Status = ExpenseStatus.Compliant;
                }
                compliantCount++;
            }
        }

        // Add Audit Log
        db.AuditLogs.Add(new AuditLog
        {
            Action = "EXPENSE_AI_AUDIT_SWEEP",
            ToolName = "ExpenseReadTool",
            Target = "Expenses Table",
            Result = $"Audited {expenses.Count} expenses: {compliantCount} compliant, {violationCount} policy violations flagged against POL-FIN-002.",
            Timestamp = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        return Ok(new
        {
            totalAudited = expenses.Count,
            compliantCount,
            violationCount,
            flaggedClaims = flagged,
            summary = $"Nexus Agent completed automated audit of {expenses.Count} claims against POL-FIN-002. Flagged {violationCount} non-compliant claims."
        });
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(ExpenseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpenseDto>> UpdateStatus(int id, [FromBody] UpdateExpenseStatusRequest request, [FromServices] NexusDbContext db)
    {
        var expense = await db.Expenses.Include(e => e.Employee).ThenInclude(emp => emp!.Department).FirstOrDefaultAsync(e => e.Id == id);
        if (expense == null) return NotFound(new { message = $"Expense claim with ID {id} not found." });

        if (Enum.TryParse<ExpenseStatus>(request.Status, true, out var parsedStatus))
        {
            expense.Status = parsedStatus;
        }
        else if (request.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            expense.Status = ExpenseStatus.Approved;
        }
        else if (request.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            expense.Status = ExpenseStatus.Rejected;
        }
        else if (request.Status.Equals("Flagged", StringComparison.OrdinalIgnoreCase) || request.Status.Equals("NonCompliant", StringComparison.OrdinalIgnoreCase))
        {
            expense.Status = ExpenseStatus.NonCompliant;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = $"EXPENSE_STATUS_UPDATE_{expense.Status}",
            ToolName = "ExpenseUpdateTool",
            Target = $"Expense #{id} ({expense.Employee?.Name})",
            Result = $"Status updated to {expense.Status}. Reason: {request.Reason ?? "Manual/Agent Action"}",
            Timestamp = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        return Ok(new ExpenseDto
        {
            Id = expense.Id,
            EmployeeId = expense.EmployeeId,
            EmployeeName = expense.Employee?.Name ?? "Unknown",
            DepartmentName = expense.Employee?.Department?.Name ?? "Unknown",
            ExpenseType = expense.ExpenseType,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            Status = expense.Status,
            Description = expense.Description
        });
    }
}

public class UpdateExpenseStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

