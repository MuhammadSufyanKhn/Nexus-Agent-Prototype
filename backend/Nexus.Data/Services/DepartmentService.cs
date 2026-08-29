using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public interface IDepartmentService
{
    Task<IEnumerable<DepartmentDto>> GetAllAsync();
    Task<DepartmentDto?> GetByIdAsync(int id);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto);
    Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentDto dto);
    Task<bool> DeleteAsync(int id);
}

public class DepartmentService : IDepartmentService
{
    private readonly NexusDbContext _db;

    public DepartmentService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        var departments = await _db.Departments.Include(d => d.Employees)
                                                .AsNoTracking()
                                                .ToListAsync();

        var budgets = await _db.Budgets.Include(b => b.Department).AsNoTracking().ToListAsync();

        Dictionary<string, (decimal Allocated, decimal Spent)> rawBudgets = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DepartmentBudgets') SELECT DepartmentName, SUM(AllocatedAmount) AS Allocated, SUM(SpentAmount) AS Spent FROM DepartmentBudgets GROUP BY DepartmentName";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var alloc = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                var spent = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                rawBudgets[name] = (alloc, spent);
            }
        }
        catch { }

        return departments.Select(d =>
        {
            var b = budgets.FirstOrDefault(b => b.DepartmentId == d.Id || (b.Department != null && b.Department.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase)));
            var allocated = b?.AllocatedAmount ?? 0m;
            var spent = b?.SpentAmount ?? 0m;

            if (allocated == 0m && rawBudgets.Count > 0)
            {
                var matchingKey = rawBudgets.Keys.FirstOrDefault(k => k.Equals(d.Name, StringComparison.OrdinalIgnoreCase) || k.Contains(d.Name, StringComparison.OrdinalIgnoreCase) || d.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
                if (matchingKey != null)
                {
                    allocated = rawBudgets[matchingKey].Allocated;
                    spent = rawBudgets[matchingKey].Spent;
                }
            }

            var remaining = allocated - spent;

            var managerEmp = d.Employees.FirstOrDefault(e => e.Designation.Contains("Head", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Manager", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Lead", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Director", StringComparison.OrdinalIgnoreCase));
            string head = managerEmp?.Name ?? d.Employees.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ManagerName))?.ManagerName ?? d.Employees.FirstOrDefault()?.Name ?? "Unassigned";

            return new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                EmployeeCount = d.Employees.Count,
                AllocatedBudget = allocated,
                ActualSpent = spent,
                RemainingBudget = remaining,
                HeadOfDepartment = head
            };
        });
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id)
    {
        var d = await _db.Departments.Include(dep => dep.Employees)
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(dep => dep.Id == id);

        if (d == null) return null;

        var b = await _db.Budgets.AsNoTracking().FirstOrDefaultAsync(b => b.DepartmentId == d.Id);
        var allocated = b?.AllocatedAmount ?? 0m;
        var spent = b?.SpentAmount ?? 0m;
        var remaining = allocated - spent;

        var managerEmp = d.Employees.FirstOrDefault(e => e.Designation.Contains("Head", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Manager", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Lead", StringComparison.OrdinalIgnoreCase) || e.Designation.Contains("Director", StringComparison.OrdinalIgnoreCase));
        string head = managerEmp?.Name ?? d.Employees.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ManagerName))?.ManagerName ?? d.Employees.FirstOrDefault()?.Name ?? "Unassigned";

        return new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            EmployeeCount = d.Employees.Count,
            AllocatedBudget = allocated,
            ActualSpent = spent,
            RemainingBudget = remaining,
            HeadOfDepartment = head
        };
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto)
    {
        var department = new Department
        {
            Name = dto.Name,
            Description = dto.Description
        };

        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            EmployeeCount = 0
        };
    }

    public async Task<DepartmentDto?> UpdateAsync(int id, UpdateDepartmentDto dto)
    {
        var department = await _db.Departments.FindAsync(id);
        if (department == null) return null;

        department.Name = dto.Name;
        department.Description = dto.Description;

        await _db.SaveChangesAsync();

        var employeeCount = await _db.Employees.CountAsync(e => e.DepartmentId == id);

        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            EmployeeCount = employeeCount
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var department = await _db.Departments.FindAsync(id);
        if (department == null) return false;

        _db.Departments.Remove(department);
        await _db.SaveChangesAsync();
        return true;
    }
}
