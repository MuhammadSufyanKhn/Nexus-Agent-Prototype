using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.Services;
using Nexus.Data;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;
using Xunit;

namespace Nexus.Tests;

public class CrudServicesTests
{
    private NexusDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new NexusDbContext(options);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task EmployeeService_CRUD_Flow_Succeeds()
    {
        // Arrange
        using var db = GetInMemoryDbContext("EmployeeCrudDb");
        var service = new EmployeeService(db);

        // 1. Create
        var createDto = new CreateEmployeeDto
        {
            Name = "Ahmed Khan",
            Email = "ahmed.khan@nexus.local",
            DepartmentId = 1, // IT
            Designation = "Mid-Level .NET Developer",
            Salary = 68000.00m,
            ExperienceYears = 3,
            Status = EmployeeStatus.Onboarding
        };

        var created = await service.CreateAsync(createDto);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Ahmed Khan", created.Name);
        Assert.Equal("IT", created.DepartmentName);

        // 2. Read Collection
        var allEmployees = await service.GetAllAsync();
        Assert.Contains(allEmployees, e => e.Id == created.Id);

        // 3. Read Single
        var single = await service.GetByIdAsync(created.Id);
        Assert.NotNull(single);
        Assert.Equal(68000.00m, single!.Salary);

        // 4. Update
        var updateDto = new UpdateEmployeeDto
        {
            Name = "Ahmed Khan",
            Email = "ahmed.khan@nexus.local",
            DepartmentId = 1,
            Designation = "Mid-Level .NET Developer",
            Salary = 72000.00m,
            ExperienceYears = 3,
            Status = EmployeeStatus.Active
        };

        var updated = await service.UpdateAsync(created.Id, updateDto);
        Assert.NotNull(updated);
        Assert.Equal(72000.00m, updated!.Salary);
        Assert.Equal(EmployeeStatus.Active, updated.Status);

        // 5. Delete
        var deleted = await service.DeleteAsync(created.Id);
        Assert.True(deleted);

        var afterDelete = await service.GetByIdAsync(created.Id);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task DepartmentService_CRUD_Flow_Succeeds()
    {
        using var db = GetInMemoryDbContext("DepartmentCrudDb");
        var service = new DepartmentService(db);

        // Create
        var created = await service.CreateAsync(new CreateDepartmentDto
        {
            Name = "Research & Development",
            Description = "AI & Emerging Technologies R&D"
        });
        Assert.True(created.Id > 0);
        Assert.Equal("Research & Development", created.Name);

        // Get All
        var list = await service.GetAllAsync();
        Assert.Contains(list, d => d.Name == "Research & Development");

        // Update
        var updated = await service.UpdateAsync(created.Id, new UpdateDepartmentDto
        {
            Name = "AI Research & Development",
            Description = "Advanced AI Agents R&D"
        });
        Assert.Equal("AI Research & Development", updated!.Name);

        // Delete
        var deleted = await service.DeleteAsync(created.Id);
        Assert.True(deleted);
    }

    [Fact]
    public async Task BudgetService_Calculates_OverBudget_Correctly()
    {
        using var db = GetInMemoryDbContext("BudgetCrudDb");
        var service = new BudgetService(db);

        // Fetch seed IT budget (Allocated 50000, Spent 58500)
        var budgets = await service.GetAllAsync(departmentId: 1, quarter: "Q3");
        var itBudget = budgets.FirstOrDefault();

        Assert.NotNull(itBudget);
        Assert.True(itBudget!.IsOverBudget);
        Assert.Equal(8500.00m, itBudget.Variance);
    }

    [Fact]
    public async Task ExpenseService_CRUD_Flow_Succeeds()
    {
        using var db = GetInMemoryDbContext("ExpenseCrudDb");
        var service = new ExpenseService(db);

        // Create expense for employee 1 (Tariq)
        var created = await service.CreateAsync(new CreateExpenseDto
        {
            EmployeeId = 1,
            ExpenseType = ExpenseType.Equipment,
            Amount = 250.00m,
            Description = "Dual Monitor Stand for Development"
        });

        Assert.True(created.Id > 0);
        Assert.Equal(ExpenseStatus.Pending, created.Status);

        // Update status to Compliant
        var updated = await service.UpdateAsync(created.Id, new UpdateExpenseDto
        {
            ExpenseType = ExpenseType.Equipment,
            Amount = 250.00m,
            Status = ExpenseStatus.Compliant,
            Description = "Dual Monitor Stand for Development"
        });

        Assert.Equal(ExpenseStatus.Compliant, updated!.Status);
    }
}
