using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Services;
using Nexus.Data.DTOs;

namespace Nexus.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetsController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BudgetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<BudgetDto>>> GetAll([FromQuery] int? departmentId, [FromQuery] string? quarter, [FromQuery] int? year)
    {
        var budgets = await _budgetService.GetAllAsync(departmentId, quarter, year);
        return Ok(budgets);
    }

    [HttpGet("master")]
    public async Task<IActionResult> GetMasterBudget([FromServices] NexusDbContext db)
    {
        try
        {
            var master = await db.MasterBudgets.FirstOrDefaultAsync(m => m.Year == 2026);
            decimal poolTotal = master?.TotalBudgetPool ?? 1_000_000_000m;
            decimal totalAllocated = await db.Budgets.SumAsync(b => b.AllocatedAmount);
            decimal remainingPool = poolTotal - totalAllocated;

            return Ok(new
            {
                year = 2026,
                fiscalYear = master?.FiscalYear ?? "2026-2027",
                totalBudgetPool = poolTotal,
                totalAllocatedAcrossDepartments = totalAllocated,
                remainingUnallocatedPool = remainingPool,
                updatedAt = master?.UpdatedAt ?? DateTime.UtcNow
            });
        }
        catch
        {
            decimal totalAllocated = 0m;
            try { totalAllocated = await db.Budgets.SumAsync(b => b.AllocatedAmount); } catch { }
            return Ok(new
            {
                year = 2026,
                fiscalYear = "2026-2027",
                totalBudgetPool = 1_000_000_000m,
                totalAllocatedAcrossDepartments = totalAllocated,
                remainingUnallocatedPool = 1_000_000_000m - totalAllocated,
                updatedAt = DateTime.UtcNow
            });
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetDto>> GetById(int id)
    {
        var budget = await _budgetService.GetByIdAsync(id);
        if (budget == null) return NotFound(new { message = $"Budget record with ID {id} not found." });
        return Ok(budget);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BudgetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BudgetDto>> Create([FromBody] CreateBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var created = await _budgetService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(BudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BudgetDto>> Update(int id, [FromBody] UpdateBudgetDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _budgetService.UpdateAsync(id, dto);
        if (updated == null) return NotFound(new { message = $"Budget record with ID {id} not found." });
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _budgetService.DeleteAsync(id);
        if (!deleted) return NotFound(new { message = $"Budget record with ID {id} not found." });
        return NoContent();
    }
}
