using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Creates an employee leave record (sick leave, PTO, vacation).
/// Resolves employee by name and calculates duration.
/// </summary>
public class LeaveTool : IAgentTool
{
    private readonly NexusDbContext _db;
    private readonly ILogger<LeaveTool> _logger;

    public LeaveTool(NexusDbContext db, ILogger<LeaveTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "leave.create",
        Description = "Record an employee leave request (sick leave, PTO, vacation, emergency leave).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employeeName"": { ""type"": ""string"" },
            ""employeeId"":   { ""type"": ""integer"" },
            ""leaveType"":    { ""type"": ""string"", ""enum"": [""Sick"",""PTO"",""Vacation"",""Maternity"",""Emergency""] },
            ""startDate"":    { ""type"": ""string"" },
            ""endDate"":      { ""type"": ""string"" },
            ""notes"":        { ""type"": ""string"" }
          },
          ""required"": []
        }",
        RequiredPermission = "employee.update",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var empName = context.GetArgument<string>("employeeName");
        var empId   = context.GetArgument<int?>("employeeId");
        if (string.IsNullOrWhiteSpace(empName) && !empId.HasValue)
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
            var empName   = context.GetArgument<string>("employeeName");
            var empId     = context.GetArgument<int?>("employeeId");
            var typeStr   = context.GetArgument<string>("leaveType") ?? "Sick";
            var startStr  = context.GetArgument<string>("startDate");
            var endStr    = context.GetArgument<string>("endDate");
            var notes     = context.GetArgument<string>("notes") ?? "Leave logged via AI Assistant";

            Employee? employee = null;
            if (empId.HasValue && empId > 0)
            {
                employee = await _db.Employees.FindAsync(empId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(empName))
            {
                employee = await _db.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Name.ToLower().Contains(empName.ToLower()));
            }

            if (employee == null)
                return ToolExecutionResult.Failure($"Employee '{empName ?? empId?.ToString()}' not found.", RiskLevel.Low);

            Enum.TryParse<LeaveType>(typeStr, true, out var lType);

            var start = DateTime.TryParse(startStr, out var sDt) ? sDt : DateTime.UtcNow.Date;
            var end   = DateTime.TryParse(endStr,   out var eDt) ? eDt : start;

            var leave = new Leave
            {
                EmployeeId  = employee.Id,
                LeaveType   = lType,
                StartDate   = start,
                EndDate     = end,
                Status      = LeaveStatus.Approved,
                ApprovedBy  = "Nexus AI (Auto-Approved)",
                Notes       = notes,
                CreatedAt   = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            };

            _db.Leaves.Add(leave);
            await _db.SaveChangesAsync();
            sw.Stop();

            var days = (leave.EndDate - leave.StartDate).Days + 1;
            return ToolExecutionResult.Success(new
            {
                message      = $"{leave.LeaveType} leave logged for {employee.Name} ({start:yyyy-MM-dd} to {end:yyyy-MM-dd}, {days} day(s)).",
                leaveId      = leave.Id,
                employeeId   = employee.Id,
                employeeName = employee.Name,
                department   = employee.Department?.Name ?? "Unknown",
                leaveType    = leave.LeaveType.ToString(),
                startDate    = leave.StartDate.ToString("yyyy-MM-dd"),
                endDate      = leave.EndDate.ToString("yyyy-MM-dd"),
                status       = leave.Status.ToString()
            }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LeaveTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
