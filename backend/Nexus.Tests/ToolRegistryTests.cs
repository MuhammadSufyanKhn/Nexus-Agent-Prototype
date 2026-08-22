using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexus.Data.Services;
using Nexus.Data;
using Nexus.Data.DTOs;
using Nexus.Data.Enums;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;
using Xunit;

namespace Nexus.Tests;

public class ToolRegistryTests
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
    public void ToolRegistry_Registers_And_Retrieves_Tools_Correctly()
    {
        using var db = GetInMemoryDbContext("ToolRegDb1");
        var empService = new EmployeeService(db);
        var registry = new ToolRegistry();

        var tool = new EmployeeReadTool(empService);
        registry.RegisterTool(tool);

        Assert.True(registry.HasTool("employee.read"));
        Assert.NotNull(registry.GetTool("employee.read"));
        Assert.Equal("employee.read", registry.GetTool("employee.read")!.Definition.Name);
    }

    [Fact]
    public void ToolRegistry_Handles_Unknown_Tool_Gracefully()
    {
        var registry = new ToolRegistry();
        Assert.False(registry.HasTool("unknown.tool"));
        Assert.Null(registry.GetTool("unknown.tool"));
    }

    [Fact]
    public void Tool_Permission_And_Risk_Metadata_Is_Accurate()
    {
        using var db = GetInMemoryDbContext("ToolMetadataDb");
        var empService = new EmployeeService(db);

        var createTool = new EmployeeCreateTool(empService);
        var readTool = new EmployeeReadTool(empService);
        var updateTool = new EmployeeUpdateTool(empService);
        var deleteTool = new EmployeeDeleteTool(empService);

        Assert.Equal("employee.create", createTool.Definition.RequiredPermission);
        Assert.Equal(RiskLevel.Medium, createTool.Definition.RiskLevel);

        Assert.Equal("employee.read", readTool.Definition.RequiredPermission);
        Assert.Equal(RiskLevel.Low, readTool.Definition.RiskLevel);

        Assert.Equal("employee.update", updateTool.Definition.RequiredPermission);
        Assert.Equal(RiskLevel.High, updateTool.Definition.RiskLevel);

        Assert.Equal("employee.delete", deleteTool.Definition.RequiredPermission);
        Assert.Equal(RiskLevel.Critical, deleteTool.Definition.RiskLevel);
    }

    [Fact]
    public async Task Tool_Validates_Invalid_Input_Properly()
    {
        using var db = GetInMemoryDbContext("ToolInputValDb");
        var empService = new EmployeeService(db);
        var createTool = new EmployeeCreateTool(empService);

        // Missing required arguments
        var invalidContext = new ToolExecutionContext
        {
            ArgumentsJson = @"{ ""name"": """" }"
        };

        var valResult = await createTool.ValidateInputAsync(invalidContext);
        Assert.False(valResult.IsValid);
        Assert.NotEmpty(valResult.Errors);

        var execResult = await createTool.ExecuteAsync(invalidContext);
        Assert.False(execResult.IsSuccess);
        Assert.NotNull(execResult.ErrorMessage);
    }

    [Fact]
    public async Task EmployeeCreateTool_Executes_Service_Layer_Successfully()
    {
        using var db = GetInMemoryDbContext("ToolExecCreateDb");
        var empService = new EmployeeService(db);
        var createTool = new EmployeeCreateTool(empService);

        var context = new ToolExecutionContext
        {
            ArgumentsJson = @"{
                ""name"": ""Ahmed Khan"",
                ""email"": ""ahmed.khan@nexus.local"",
                ""departmentId"": 1,
                ""designation"": ""Mid-Level .NET Developer"",
                ""salary"": 68000,
                ""experienceYears"": 3
            }"
        };

        var result = await createTool.ExecuteAsync(context);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(RiskLevel.Medium, result.RiskLevel);

        var createdDto = result.Data as EmployeeDto;
        Assert.NotNull(createdDto);
        Assert.Equal("Ahmed Khan", createdDto!.Name);
        Assert.Equal("IT", createdDto.DepartmentName);
    }

    [Fact]
    public async Task All_Seven_Tools_Register_And_Execute_Successfully()
    {
        using var db = GetInMemoryDbContext("AllToolsDb");
        var empService = new EmployeeService(db);
        var deptService = new DepartmentService(db);
        var budgetService = new BudgetService(db);
        var expenseService = new ExpenseService(db);

        var registry = new ToolRegistry();
        registry.RegisterTool(new EmployeeCreateTool(empService));
        registry.RegisterTool(new EmployeeReadTool(empService));
        registry.RegisterTool(new EmployeeUpdateTool(empService));
        registry.RegisterTool(new EmployeeDeleteTool(empService));
        registry.RegisterTool(new DepartmentReadTool(deptService));
        registry.RegisterTool(new BudgetReadTool(budgetService));
        registry.RegisterTool(new ExpenseReadTool(expenseService));

        Assert.Equal(7, registry.GetAllTools().Count());

        // Test department.read
        var deptTool = registry.GetTool("department.read");
        Assert.NotNull(deptTool);
        var deptRes = await deptTool!.ExecuteAsync(new ToolExecutionContext());
        Assert.True(deptRes.IsSuccess);

        // Test budget.read
        var budgetTool = registry.GetTool("budget.read");
        Assert.NotNull(budgetTool);
        var budgetRes = await budgetTool!.ExecuteAsync(new ToolExecutionContext());
        Assert.True(budgetRes.IsSuccess);

        // Test expense.read
        var expenseTool = registry.GetTool("expense.read");
        Assert.NotNull(expenseTool);
        var expenseRes = await expenseTool!.ExecuteAsync(new ToolExecutionContext());
        Assert.True(expenseRes.IsSuccess);
    }
}
