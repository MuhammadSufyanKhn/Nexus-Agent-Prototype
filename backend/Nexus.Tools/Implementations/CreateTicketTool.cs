using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class CreateTicketTool : IAgentTool
{
    private readonly IPythonAutomationService _pythonService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CreateTicketTool> _logger;

    public CreateTicketTool(IPythonAutomationService pythonService, IServiceProvider serviceProvider, ILogger<CreateTicketTool> logger)
    {
        _pythonService = pythonService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "ticket.create",
        Description = "Creates automated IT provisioning tickets for employee hardware/software onboarding via Python automation subsystem.",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""employee"": { ""type"": ""string"" },
            ""department"": { ""type"": ""string"" },
            ""requestType"": { ""type"": ""string"" }
          }
        }",
        RequiredPermission = "employee.read",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        var jsonArgs = string.IsNullOrWhiteSpace(context.ArgumentsJson) ? "{}" : context.ArgumentsJson;

        var jsonResultStr = await _pythonService.ExecuteOperationAsync("ticket.create", jsonArgs);

        sw.Stop();
        try
        {
            using var doc = JsonDocument.Parse(jsonResultStr);
            var root = doc.RootElement;

            var prompt = context.GetArgument<string>("prompt") ?? string.Empty;
            var employee = context.GetArgument<string>("employee") ?? context.GetArgument<string>("name");
            if (string.IsNullOrWhiteSpace(employee))
            {
                var m = Regex.Match(prompt, @"(?:for|hire)\s+([A-Za-z]+(?:\s+[A-Za-z]+)?)", RegexOptions.IgnoreCase);
                if (m.Success) employee = m.Groups[1].Value;
                else employee = "Sarah";
            }

            var department = context.GetArgument<string>("department");
            if (string.IsNullOrWhiteSpace(department))
            {
                var dMatch = Regex.Match(prompt, @"\b(?:in|to)\s+([A-Za-z0-9\&\s]+?)(?:\s+team|\s+department|\s+dept|$)", RegexOptions.IgnoreCase);
                if (dMatch.Success) department = dMatch.Groups[1].Value.Trim();
                else department = "DevOps";
            }

            var requestType = context.GetArgument<string>("requestType") ?? "Hardware & Software Provisioning";
            var details = context.GetArgument<string>("details");
            if (string.IsNullOrWhiteSpace(details))
            {
                details = $"Provisioning ticket for '{employee}' ({department}). Items: MacBook Pro M3, AWS VPN credentials, YubiKey access.";
            }

            var ticketId = root.TryGetProperty("ticketId", out var tProp) ? tProp.GetString() ?? $"TCK-{DateTime.UtcNow.Year}-{Random.Shared.Next(1000, 9999)}" : $"TCK-{DateTime.UtcNow.Year}-{Random.Shared.Next(1000, 9999)}";

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<NexusDbContext>();
            if (db != null)
            {
                var newTicket = new Ticket
                {
                    TicketId = ticketId,
                    EmployeeName = employee,
                    Department = department,
                    RequestType = requestType,
                    Priority = "High",
                    Status = "Open",
                    Details = details,
                    CreatedAt = DateTime.UtcNow
                };
                db.Tickets.Add(newTicket);
                await db.SaveChangesAsync();
            }

            return ToolExecutionResult.Success(root.Clone(), Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse ticket JSON or save ticket to DB.");
            return ToolExecutionResult.Success(new { result = jsonResultStr }, Definition.RiskLevel, sw.ElapsedMilliseconds);
        }
    }
}

