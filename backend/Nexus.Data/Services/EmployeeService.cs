using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.DTOs;
using Nexus.Data.Entities;

namespace Nexus.Data.Services;

public interface IEmployeeService
{
    Task<IEnumerable<EmployeeDto>> GetAllAsync(int? departmentId = null, string? search = null);
    Task<EmployeeDto?> GetByIdAsync(int id);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto);
    Task<EmployeeDto?> UpdateAsync(int id, UpdateEmployeeDto dto);
    Task<bool> DeleteAsync(int id);
}

public class EmployeeService : IEmployeeService
{
    private readonly NexusDbContext _db;

    public EmployeeService(NexusDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<EmployeeDto>> GetAllAsync(int? departmentId = null, string? search = null)
    {
        var query = _db.Employees.Include(e => e.Department).AsNoTracking().AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(searchLower) ||
                                     e.Email.ToLower().Contains(searchLower) ||
                                     e.Designation.ToLower().Contains(searchLower));
        }

        var employees = await query.ToListAsync();

        return employees.Select(e => new EmployeeDto
        {
            Id = e.Id,
            Name = e.Name,
            Email = e.Email,
            DepartmentId = e.DepartmentId,
            DepartmentName = e.Department?.Name ?? "Unknown",
            Designation = e.Designation,
            Salary = e.Salary,
            ExperienceYears = e.ExperienceYears,
            Status = e.Status,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        });
    }

    public async Task<EmployeeDto?> GetByIdAsync(int id)
    {
        var e = await _db.Employees.Include(emp => emp.Department)
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(emp => emp.Id == id);

        if (e == null) return null;

        return new EmployeeDto
        {
            Id = e.Id,
            Name = e.Name,
            Email = e.Email,
            DepartmentId = e.DepartmentId,
            DepartmentName = e.Department?.Name ?? "Unknown",
            Designation = e.Designation,
            Salary = e.Salary,
            ExperienceYears = e.ExperienceYears,
            Status = e.Status,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto)
    {
        var department = await _db.Departments.FindAsync(dto.DepartmentId);
        if (department == null)
        {
            var targetDeptName = string.IsNullOrWhiteSpace(dto.DepartmentName) ? "IT" : dto.DepartmentName;
            department = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower() == targetDeptName.ToLower());
            if (department == null)
            {
                department = new Department { Name = targetDeptName, Description = $"{targetDeptName} Department" };
                _db.Departments.Add(department);
                await _db.SaveChangesAsync();
            }
            dto.DepartmentId = department.Id;
        }

        var employee = new Employee
        {
            Name = dto.Name,
            Email = dto.Email,
            DepartmentId = dto.DepartmentId,
            Designation = dto.Designation,
            Salary = dto.Salary,
            ExperienceYears = dto.ExperienceYears,
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        return new EmployeeDto
        {
            Id = employee.Id,
            Name = employee.Name,
            Email = employee.Email,
            DepartmentId = employee.DepartmentId,
            DepartmentName = department.Name,
            Designation = employee.Designation,
            Salary = employee.Salary,
            ExperienceYears = employee.ExperienceYears,
            Status = employee.Status,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }

    public async Task<EmployeeDto?> UpdateAsync(int id, UpdateEmployeeDto dto)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee == null) return null;

        var department = await _db.Departments.FindAsync(dto.DepartmentId);
        if (department == null)
        {
            throw new ArgumentException($"Department with ID {dto.DepartmentId} does not exist.");
        }

        employee.Name = dto.Name;
        employee.Email = dto.Email;
        employee.DepartmentId = dto.DepartmentId;
        employee.Designation = dto.Designation;
        employee.Salary = dto.Salary;
        employee.ExperienceYears = dto.ExperienceYears;
        employee.Status = dto.Status;
        employee.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new EmployeeDto
        {
            Id = employee.Id,
            Name = employee.Name,
            Email = employee.Email,
            DepartmentId = employee.DepartmentId,
            DepartmentName = department.Name,
            Designation = employee.Designation,
            Salary = employee.Salary,
            ExperienceYears = employee.ExperienceYears,
            Status = employee.Status,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee == null) return false;

        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();
        return true;
    }
}
