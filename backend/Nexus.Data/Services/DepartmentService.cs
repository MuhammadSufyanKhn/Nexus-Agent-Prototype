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

        return departments.Select(d => new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            EmployeeCount = d.Employees.Count
        });
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id)
    {
        var d = await _db.Departments.Include(dep => dep.Employees)
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(dep => dep.Id == id);

        if (d == null) return null;

        return new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            EmployeeCount = d.Employees.Count
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
