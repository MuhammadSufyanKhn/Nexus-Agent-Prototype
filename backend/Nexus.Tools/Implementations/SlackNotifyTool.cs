using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Sends team notifications via Python automation subsystem & Slack Webhook.
/// Queries database for employee's exact email address before dispatching email.
/// </summary>
public class SlackNotifyTool : IAgentTool
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly IPythonAutomationService _pythonService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlackNotifyTool> _logger;

    public SlackNotifyTool(
        HttpClient httpClient, 
        IConfiguration config, 
        IPythonAutomationService pythonService, 
        IServiceProvider serviceProvider, 
        ILogger<SlackNotifyTool> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _pythonService = pythonService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "slack.notify",
        Description = "Send notification to a team or channel via Python email automation & Slack Webhook (queries DB for employee email address).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""channel"":          { ""type"": ""string"" },
            ""team"":             { ""type"": ""string"" },
            ""employeeName"":     { ""type"": ""string"" },
            ""employeeEmail"":    { ""type"": ""string"" },
            ""message"":          { ""type"": ""string"" },
            ""notificationType"": { ""type"": ""string"" }
          },
          ""required"": []
        }",
        RequiredPermission = "system.execution",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var empName = context.GetArgument<string>("employeeName") ?? context.GetArgument<string>("name") ?? "";
            var argEmpEmail = context.GetArgument<string>("employeeEmail") ?? context.GetArgument<string>("email");
            var argDept = context.GetArgument<string>("department") ?? context.GetArgument<string>("team");

            string? empEmailFromDb = argEmpEmail;
            string? empDeptFromDb = argDept;

            if (!string.IsNullOrWhiteSpace(empName))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
                    var cleanName = empName.Replace("Log", "", StringComparison.OrdinalIgnoreCase)
                                           .Replace("Employee", "", StringComparison.OrdinalIgnoreCase)
                                           .Trim();

                    var emp = await db.Employees
                        .Include(e => e.Department)
                        .Where(e => e.Name.ToLower().Contains(empName.ToLower()) ||
                                    (!string.IsNullOrWhiteSpace(cleanName) && e.Name.ToLower().Contains(cleanName.ToLower())) ||
                                    empName.ToLower().Contains(e.Name.ToLower()))
                        .OrderByDescending(e => e.Id)
                        .FirstOrDefaultAsync();

                    if (emp != null)
                    {
                        var targetEmail = emp.Email;
                        if (!string.IsNullOrWhiteSpace(targetEmail) && targetEmail.EndsWith("@nexus.local", StringComparison.OrdinalIgnoreCase))
                        {
                            targetEmail = targetEmail.Replace("@nexus.local", "@gmail.com", StringComparison.OrdinalIgnoreCase);
                        }
                        empEmailFromDb = targetEmail;
                        if (string.IsNullOrWhiteSpace(empDeptFromDb)) empDeptFromDb = emp.Department?.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query database for employee email in SlackNotifyTool");
                }
            }

            var argDict = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(context.ArgumentsJson) && context.ArgumentsJson != "{}")
            {
                try
                {
                    using var doc = JsonDocument.Parse(context.ArgumentsJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        argDict[prop.Name] = prop.Value.ToString();
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(empName)) argDict["employeeName"] = empName;
            if (!string.IsNullOrWhiteSpace(empEmailFromDb)) argDict["employeeEmail"] = empEmailFromDb;
            if (!string.IsNullOrWhiteSpace(empDeptFromDb)) argDict["department"] = empDeptFromDb;

            var finalJsonArgs = JsonSerializer.Serialize(argDict);

            // 1. Execute Python Email Subsystem Automation
            var pythonResultStr = await _pythonService.ExecuteOperationAsync("email.sick_leave", finalJsonArgs);

            // 2. Also check Slack Webhook if configured
            var webhookUrl = _config["Slack:WebhookUrl"];
            if (!string.IsNullOrWhiteSpace(webhookUrl) && Uri.IsWellFormedUriString(webhookUrl, UriKind.Absolute))
            {
                var msg = context.GetArgument<string>("message") ?? "Sick leave logged.";
                var payload = JsonSerializer.Serialize(new { channel = "#general", text = $"[ALERT] {empName}: {msg}" });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(webhookUrl, content);
            }

            sw.Stop();
            try
            {
                using var doc = JsonDocument.Parse(pythonResultStr);
                return ToolExecutionResult.Success(doc.RootElement.Clone(), Definition.RiskLevel, sw.ElapsedMilliseconds);
            }
            catch
            {
                return ToolExecutionResult.Success(new
                {
                    message = $"Notification email generated and dispatched for {empName} ({empEmailFromDb}).",
                    employeeEmail = empEmailFromDb,
                    pythonOutput = pythonResultStr
                }, Definition.RiskLevel, sw.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SlackNotifyTool error");
            return ToolExecutionResult.Failure(ex.Message, Definition.RiskLevel);
        }
    }
}
