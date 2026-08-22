using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;
using Nexus.Data.Enums;

namespace Nexus.Data.Services;

public interface IExpenseService
{
    Task<IEnumerable<ExpenseDto>> GetAllAsync(int? employeeId = null, ExpenseStatus? status = null);
    Task<ExpenseDto?> GetByIdAsync(int id);
    Task<ExpenseDto> CreateAsync(CreateExpenseDto dto);
    Task<ExpenseDto?> UpdateAsync(int id, UpdateExpenseDto dto);
    Task<bool> DeleteAsync(int id);
}

public class ExpenseService : IExpenseService
{
    private readonly NexusDbContext _db;

    public ExpenseService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ExpenseDto>> GetAllAsync(int? employeeId = null, ExpenseStatus? status = null)
    {
        var query = _db.Expenses.Include(e => e.Employee)
                                .ThenInclude(emp => emp!.Department)
                                .AsNoTracking()
                                .AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(e => e.EmployeeId == employeeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var expenses = await query.ToListAsync();

        return expenses.Select(e => new ExpenseDto
        {
            Id = e.Id,
            EmployeeId = e.EmployeeId,
            EmployeeName = e.Employee?.Name ?? "Unknown",
            DepartmentName = e.Employee?.Department?.Name ?? "Unknown",
            ExpenseType = e.ExpenseType,
            Amount = e.Amount,
            ExpenseDate = e.ExpenseDate,
            Status = e.Status,
            Description = e.Description
        });
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id)
    {
        var e = await _db.Expenses.Include(exp => exp.Employee)
                                  .ThenInclude(emp => emp!.Department)
                                  .AsNoTracking()
                                  .FirstOrDefaultAsync(exp => exp.Id == id);

        if (e == null) return null;

        return new ExpenseDto
        {
            Id = e.Id,
            EmployeeId = e.EmployeeId,
            EmployeeName = e.Employee?.Name ?? "Unknown",
            DepartmentName = e.Employee?.Department?.Name ?? "Unknown",
            ExpenseType = e.ExpenseType,
            Amount = e.Amount,
            ExpenseDate = e.ExpenseDate,
            Status = e.Status,
            Description = e.Description
        };
    }

    public async Task<ExpenseDto> CreateAsync(CreateExpenseDto dto)
    {
        var employee = await _db.Employees.Include(emp => emp.Department).FirstOrDefaultAsync(emp => emp.Id == dto.EmployeeId);
        if (employee == null)
        {
            throw new ArgumentException($"Employee with ID {dto.EmployeeId} does not exist.");
        }

        var expense = new Expense
        {
            EmployeeId = dto.EmployeeId,
            ExpenseType = dto.ExpenseType,
            Amount = dto.Amount,
            ExpenseDate = dto.ExpenseDate,
            Status = ExpenseStatus.Pending,
            Description = dto.Description
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        return new ExpenseDto
        {
            Id = expense.Id,
            EmployeeId = expense.EmployeeId,
            EmployeeName = employee.Name,
            DepartmentName = employee.Department?.Name ?? "Unknown",
            ExpenseType = expense.ExpenseType,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            Status = expense.Status,
            Description = expense.Description
        };
    }

    public async Task<ExpenseDto?> UpdateAsync(int id, UpdateExpenseDto dto)
    {
        var expense = await _db.Expenses.Include(e => e.Employee)
                                        .ThenInclude(emp => emp!.Department)
                                        .FirstOrDefaultAsync(e => e.Id == id);
        if (expense == null) return null;

        expense.ExpenseType = dto.ExpenseType;
        expense.Amount = dto.Amount;
        expense.Status = dto.Status;
        expense.Description = dto.Description;

        await _db.SaveChangesAsync();

        return new ExpenseDto
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
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null) return false;

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();
        return true;
    }
}
