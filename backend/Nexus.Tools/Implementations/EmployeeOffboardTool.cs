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
/// Handles employee offboarding, termination, exit clearance, and onboarding cancellations.
/// Sets status to Inactive/Terminated and creates clearance tasks.
/// </summary>
public class EmployeeOffboardTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<EmployeeOffboardTool> _logger;

    public EmployeeOffboardTool(NexusDbContext db, ILogger<EmployeeOffboardTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "employee.offboard",
        Description = "Offboard, terminate, or cancel onboarding for an employee. Initiates exit clearance procedures.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employeeId"":   { ""type"": ""integer"" },
            ""employeeName"": { ""type"": ""string"" },
            ""actionType"":   { ""type"": ""string"", ""enum"": [""Offboard"",""Terminate"",""ExitClearance"",""CancelOnboarding""] },
            ""reason"":       { ""type"": ""string"" },
            ""effectiveDate"":{ ""type"": ""string"" }
          },
          ""required"": []
        }",
        RequiredPermission = "employee.delete",
        RiskLevel = RiskLevel.High
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var empId   = context.GetArgument<int?>("employeeId");
        var empName = context.GetArgument<string>("employeeName");
        if (!empId.HasValue && string.IsNullOrWhiteSpace(empName))
            return Task.FromResult(ValidationResult.Failure("Employee name or ID is required."));
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var validation = await ValidateInputAsync(context);
        if (!validation.IsValid)
            return ToolExecutionResult.Failure(string.Join("; ", validation.Errors), Definition.RiskLevel);

        var sw = Stopwatch.StartNew();
        try
        {
            var empId      = context.GetArgument<int?>("employeeId");
            var empName    = context.GetArgument<string>("employeeName");
            var actionType = context.GetArgument<string>("actionType") ?? "Offboard";
            var reason     = context.GetArgument<string>("reason") ?? "Offboarding requested";

            Nexus.Data.Entities.Employee? employee = null;

            if (empId.HasValue && empId > 0)
            {
                employee = await _db.Employees.FindAsync(empId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(empName))
            {
                employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.Name.ToLower().Contains(empName.ToLower()));
            }

            if (employee == null)
                return ToolExecutionResult.Failure($"Employee '{empName ?? empId?.ToString()}' not found.", RiskLevel.Low);

            if (actionType.Equals("Terminate", StringComparison.OrdinalIgnoreCase))
            {
                employee.Status = EmployeeStatus.Terminated;
            }
            else if (actionType.Equals("CancelOnboarding", StringComparison.OrdinalIgnoreCase))
            {
                employee.Status = EmployeeStatus.Inactive;
            }
            else
            {
                employee.Status = EmployeeStatus.Inactive;
            }

            employee.UpdatedAt = DateTime.UtcNow;

            var clearanceTasks = new[]
            {
                ("IT Systems Deprovisioning", "Active Directory / LDAP / VPN Revocation"),
                ("Asset Recovery", "Laptop, Badge & Hardware Return"),
                ("Finance Final Settlement", "Payroll Clearance & Final Settlement"),
                ("HR Exit Interview", "Formal Exit Clearance Form")
            };

            foreach (var (taskName, desc) in clearanceTasks)
            {
                _db.OnboardingTasks.Add(new Nexus.Data.Entities.OnboardingTask
                {
                    EmployeeId   = employee.Id,
                    TaskName     = taskName,
                    SystemTarget = "EXIT_CLEARANCE",
                    Status       = OnboardingTaskStatus.Pending,
                    DetailsJson  = $"{{\"reason\":\"{reason}\",\"description\":\"{desc}\"}}",
                    CreatedAt    = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            sw.Stop();

            return ToolExecutionResult.Success(new
            {
                message      = $"Employee {employee.Name} status updated to {employee.Status}. Exit clearance tasks initiated.",
                employeeId   = employee.Id,
                employeeName = employee.Name,
                newStatus    = employee.Status.ToString(),
                actionType,
                clearanceTaskCount = clearanceTasks.Length
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmployeeOffboardTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
