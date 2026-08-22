using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public interface IBudgetService
{
    Task<IEnumerable<BudgetDto>> GetAllAsync(int? departmentId = null, string? quarter = null, int? year = null);
    Task<BudgetDto?> GetByIdAsync(int id);
    Task<BudgetDto> CreateAsync(CreateBudgetDto dto);
    Task<BudgetDto?> UpdateAsync(int id, UpdateBudgetDto dto);
    Task<bool> DeleteAsync(int id);
}

public class BudgetService : IBudgetService
{
    private readonly NexusDbContext _db;

    public BudgetService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<BudgetDto>> GetAllAsync(int? departmentId = null, string? quarter = null, int? year = null)
    {
        var query = _db.Budgets.Include(b => b.Department).AsNoTracking().AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(b => b.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(quarter))
        {
            query = query.Where(b => b.Quarter.ToLower() == quarter.Trim().ToLower());
        }

        if (year.HasValue)
        {
            query = query.Where(b => b.Year == year.Value);
        }

        var budgets = await query.ToListAsync();

        return budgets.Select(b => new BudgetDto
        {
            Id = b.Id,
            DepartmentId = b.DepartmentId,
            DepartmentName = b.Department?.Name ?? "Unknown",
            Year = b.Year,
            Quarter = b.Quarter,
            AllocatedAmount = b.AllocatedAmount,
            SpentAmount = b.SpentAmount
        });
    }

    public async Task<BudgetDto?> GetByIdAsync(int id)
    {
        var b = await _db.Budgets.Include(bg => bg.Department)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(bg => bg.Id == id);

        if (b == null) return null;

        return new BudgetDto
        {
            Id = b.Id,
            DepartmentId = b.DepartmentId,
            DepartmentName = b.Department?.Name ?? "Unknown",
            Year = b.Year,
            Quarter = b.Quarter,
            AllocatedAmount = b.AllocatedAmount,
            SpentAmount = b.SpentAmount
        };
    }

    public async Task<BudgetDto> CreateAsync(CreateBudgetDto dto)
    {
        var department = await _db.Departments.FindAsync(dto.DepartmentId);
        if (department == null)
        {
            throw new ArgumentException($"Department with ID {dto.DepartmentId} does not exist.");
        }

        var budget = new Budget
        {
            DepartmentId = dto.DepartmentId,
            Year = dto.Year,
            Quarter = dto.Quarter,
            AllocatedAmount = dto.AllocatedAmount,
            SpentAmount = dto.SpentAmount
        };

        _db.Budgets.Add(budget);
        await _db.SaveChangesAsync();

        return new BudgetDto
        {
            Id = budget.Id,
            DepartmentId = budget.DepartmentId,
            DepartmentName = department.Name,
            Year = budget.Year,
            Quarter = budget.Quarter,
            AllocatedAmount = budget.AllocatedAmount,
            SpentAmount = budget.SpentAmount
        };
    }

    public async Task<BudgetDto?> UpdateAsync(int id, UpdateBudgetDto dto)
    {
        var budget = await _db.Budgets.Include(b => b.Department).FirstOrDefaultAsync(b => b.Id == id);
        if (budget == null) return null;

        budget.AllocatedAmount = dto.AllocatedAmount;
        budget.SpentAmount = dto.SpentAmount;

        await _db.SaveChangesAsync();

        return new BudgetDto
        {
            Id = budget.Id,
            DepartmentId = budget.DepartmentId,
            DepartmentName = budget.Department?.Name ?? "Unknown",
            Year = budget.Year,
            Quarter = budget.Quarter,
            AllocatedAmount = budget.AllocatedAmount,
            SpentAmount = budget.SpentAmount
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var budget = await _db.Budgets.FindAsync(id);
        if (budget == null) return false;

        _db.Budgets.Remove(budget);
        await _db.SaveChangesAsync();
        return true;
    }
}
