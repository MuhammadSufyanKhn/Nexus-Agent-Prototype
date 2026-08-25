using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Handles bulk employee role, designation, and status updates across a department or entire company.
/// </summary>
public class BulkEmployeeUpdateTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<BulkEmployeeUpdateTool> _logger;

    public BulkEmployeeUpdateTool(NexusDbContext db, ILogger<BulkEmployeeUpdateTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.bulk_update",
        Description = "Bulk update employee designations, roles, or departments based on filter criteria.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""filterDepartment"":  { ""type"": ""string"" },
            ""filterRole"":        { ""type"": ""string"" },
            ""targetDesignation"": { ""type"": ""string"" },
            ""targetDepartment"":  { ""type"": ""string"" },
            ""scope"":             { ""type"": ""string"", ""enum"": [""SINGLE"",""ALL""] }
          },
          ""required"": []
        }",
        RequiredPermission = "employee.update",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
        => Task.FromResult(ValidationResult.Success());

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var filterDept  = context.GetArgument<string>("filterDepartment");
            var filterRole  = context.GetArgument<string>("filterRole");
            var targetDesig = context.GetArgument<string>("targetDesignation");

            var query = _db.Employees.Include(e => e.Department).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filterDept))
                query = query.Where(e => e.Department != null && e.Department.Name.ToLower().Contains(filterDept.ToLower()));

            if (!string.IsNullOrWhiteSpace(filterRole))
                query = query.Where(e => e.Designation.ToLower().Contains(filterRole.ToLower()));

            var employees = await query.ToListAsync();

            if (employees.Count == 0)
                return ToolExecutionResult.Failure("No employee records matched the bulk update criteria.", RiskLevel.Low);

            foreach (var emp in employees)
            {
                if (!string.IsNullOrWhiteSpace(targetDesig))
                    emp.Designation = targetDesig;

                emp.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message       = $"Bulk update completed across {employees.Count} employee record(s).",
                affectedCount = employees.Count,
                newDesignation = targetDesig
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BulkEmployeeUpdateTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
