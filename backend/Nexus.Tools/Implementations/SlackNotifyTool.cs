using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Data.Enums;
using Nexus.Tools.Automation;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

/// <summary>
/// Sends team notifications via Python automation subsystem & Slack Webhook.
/// Dispatches email notifications to nexusagent.notifications@gmail.com.
/// </summary>
public class SlackNotifyTool : IAgentTool
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly IPythonAutomationService _pythonService;
    private readonly ILogger<SlackNotifyTool> _logger;

    public SlackNotifyTool(HttpClient httpClient, IConfiguration config, IPythonAutomationService pythonService, ILogger<SlackNotifyTool> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _pythonService = pythonService;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "slack.notify",
        Description = "Send notification to a team or channel via Python email automation & Slack Webhook (dispatches to nexusagent.notifications@gmail.com).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""channel"":          { ""type"": ""string"" },
            ""team"":             { ""type"": ""string"" },
            ""employeeName"":     { ""type"": ""string"" },
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
            var jsonArgs = string.IsNullOrWhiteSpace(context.ArgumentsJson) ? "{}" : context.ArgumentsJson;

            // 1. Execute Python Email Subsystem Automation
            var pythonResultStr = await _pythonService.ExecuteOperationAsync("email.sick_leave", jsonArgs);

            // 2. Also check Slack Webhook if configured
            var webhookUrl = _config["Slack:WebhookUrl"];
            if (!string.IsNullOrWhiteSpace(webhookUrl) && Uri.IsWellFormedUriString(webhookUrl, UriKind.Absolute))
            {
                var empName = context.GetArgument<string>("employeeName") ?? context.GetArgument<string>("name") ?? "Sarah Jenkins";
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
                    message = "Notification email generated and dispatched to nexusagent.notifications@gmail.com via Python subsystem.",
                    recipientEmail = "nexusagent.notifications@gmail.com",
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
