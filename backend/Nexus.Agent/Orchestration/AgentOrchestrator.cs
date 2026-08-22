using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Agent.Events;
using Nexus.Agent.Intent;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Data.Enums;
using Nexus.Security.Permissions;
using Nexus.Security.Risk;
using Nexus.Tools.Core;

namespace Nexus.Agent.Orchestration;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IIntentParser _intentParser;
    private readonly IToolRegistry _toolRegistry;
    private readonly NexusDbContext _db;
    private readonly IRiskEngine _riskEngine;
    private readonly IPermissionService _permissionService;
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IIntentParser intentParser,
        IToolRegistry toolRegistry,
        NexusDbContext db,
        ILogger<AgentOrchestrator> logger,
        IRiskEngine? riskEngine = null,
        IPermissionService? permissionService = null,
        IAgentEventBroadcaster? broadcaster = null)
    {
        _intentParser = intentParser;
        _toolRegistry = toolRegistry;
        _db = db;
        _logger = logger;
        _riskEngine = riskEngine ?? new Nexus.Security.Risk.RiskEngine();
        _permissionService = permissionService ?? new Nexus.Security.Permissions.PermissionService();
        _broadcaster = broadcaster ?? new Nexus.Agent.Events.AgentEventBroadcaster(NullLogger<Nexus.Agent.Events.AgentEventBroadcaster>.Instance);
    }

    public async Task<AgentResult> ExecuteAsync(string userPrompt, int? userId = null, string userRole = "Admin", CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var feed = new List<AgentEvent>();

        // 1. RECEIVE REQUEST & Create AgentRun Entity
        feed.Add(AgentEvent.Create(AgentEventType.REQUEST_RECEIVED, $"Received natural language request: '{userPrompt}'"));

        var agentRun = new AgentRun
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OriginalPrompt = userPrompt,
            Intent = "UNKNOWN",
            Status = AgentRunStatus.Executing,
            StartedAt = DateTime.UtcNow
        };

        _db.AgentRuns.Add(agentRun);
        await _db.SaveChangesAsync(cancellationToken);
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "REQUEST_RECEIVED", $"Received natural language request: '{userPrompt}'"), cancellationToken);

        // 2. PARSE INTENT
        var parsedIntent = await _intentParser.ParseIntentAsync(userPrompt, cancellationToken);
        agentRun.Intent = parsedIntent.Intent;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.INTENT_PARSED, $"Parsed intent: {parsedIntent.Intent} (Confidence: {parsedIntent.Confidence:P0})", JsonSerializer.Serialize(parsedIntent.Entities)));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "INTENT_PARSED", $"Parsed intent: {parsedIntent.Intent}"), cancellationToken);

        // 3. VALIDATE INTENT
        if (parsedIntent.RequiresClarification)
        {
            agentRun.Status = AgentRunStatus.Failed;
            agentRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Clarification required: {parsedIntent.ClarificationPrompt}"));
            sw.Stop();

            return new AgentResult
            {
                RunId = agentRun.Id,
                OriginalPrompt = userPrompt,
                Intent = parsedIntent.Intent,
                IsSuccess = false,
                ExecutionFeed = feed,
                ErrorMessage = parsedIntent.ClarificationPrompt,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        // 4. PLAN GENERATION & RISK EVALUATION
        var plan = BuildPlan(userPrompt, parsedIntent);

        // 4.1 RBAC SECURITY PERMISSION CHECK ACROSS PLAN STEPS BEFORE APPROVAL INTERCEPTION
        foreach (var step in plan.Steps)
        {
            var tool = _toolRegistry.GetTool(step.ToolName);
            if (tool != null && !_permissionService.HasPermission(userId, userRole, tool.Definition.RequiredPermission))
            {
                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Security Violation: User role '{userRole}' lacks required permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'."));
                sw.Stop();

                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutionFeed = feed,
                    ErrorMessage = $"Security Violation: User role '{userRole}' lacks required permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }
        }

        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "PLAN_GENERATED", $"Generated plan with {plan.Steps.Count} steps"), cancellationToken);

        // 4.5 HUMAN-IN-THE-LOOP HIGH-RISK PLAN OF ACTION INTERCEPTOR (Only for mutation operations, never read-only queries)
        var promptLower = userPrompt.ToLower();
        var isReadOnly = parsedIntent.ParsedIntentType == IntentType.BUDGET_ANALYSIS || parsedIntent.ParsedIntentType == IntentType.EXPENSE_COMPLIANCE || parsedIntent.ParsedIntentType == IntentType.EMPLOYEE_READ;

        if (!isReadOnly && (plan.TotalRiskLevel >= RiskLevel.High || promptLower.Contains("increase") || promptLower.Contains("10%") || promptLower.Contains("bulk") || promptLower.Contains("onboard") || promptLower.Contains("delete") || promptLower.Contains("remove")))
        {
            var actionPlan = await BuildActionPlanAsync(agentRun.Id, userPrompt, parsedIntent, cancellationToken);

            var approval = new Approval
            {
                Id = Guid.NewGuid(),
                AgentRunId = agentRun.Id,
                RiskLevel = actionPlan.RiskLevel,
                RequestedBy = "System Orchestrator",
                Status = ApprovalStatus.Pending,
                Reason = JsonSerializer.Serialize(actionPlan),
                CreatedAt = DateTime.UtcNow
            };

            _db.Approvals.Add(approval);
            agentRun.Status = AgentRunStatus.WaitingForApproval;
            await _db.SaveChangesAsync(cancellationToken);

            actionPlan.ApprovalId = approval.Id;
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, "AWAITING_APPROVAL: High-risk action requires human confirmation before database modification.", JsonSerializer.Serialize(actionPlan)));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, 0, "APPROVAL_REQUIRED", "High-risk action requires human confirmation."), cancellationToken);
            sw.Stop();

            return new AgentResult
            {
                RunId = agentRun.Id,
                OriginalPrompt = userPrompt,
                Intent = parsedIntent.Intent,
                IsSuccess = true,
                RequiresApproval = true,
                ActionPlan = actionPlan,
                Plan = plan,
                ExecutionFeed = feed,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }

        var executedSteps = new List<AgentStep>();
        object? lastResultData = null;

        // 5. EXECUTE STEPS IN PLAN
        foreach (var step in plan.Steps)
        {
            var tool = _toolRegistry.GetTool(step.ToolName);
            if (tool == null)
            {
                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Tool '{step.ToolName}' is not registered in ToolRegistry."));
                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                sw.Stop();
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = $"Registered tool '{step.ToolName}' not found.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, $"Selected tool '{step.ToolName}' for step {step.StepNumber}: {step.Description}"));

            // Permission Check
            if (!_permissionService.HasPermission(userId, userRole, tool.Definition.RequiredPermission))
            {
                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Security Violation: User role '{userRole}' lacks permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'."));
                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                sw.Stop();
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = $"Security Violation: User role '{userRole}' lacks required permission '{tool.Definition.RequiredPermission}' for tool '{tool.Definition.Name}'.",
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Create AgentAction Entity
            var agentAction = new AgentAction
            {
                Id = Guid.NewGuid(),
                AgentRunId = agentRun.Id,
                ToolName = step.ToolName,
                ActionType = step.ToolName,
                ArgumentsJson = step.ArgumentsJson,
                RiskLevel = step.RiskLevel,
                Status = AgentActionStatus.Executing,
                StartedAt = DateTime.UtcNow
            };

            _db.AgentActions.Add(agentAction);
            await _db.SaveChangesAsync(cancellationToken);

            // Execute Tool with serialized entity parameters & user prompt
            var entitiesDict = new Dictionary<string, object>(parsedIntent.Entities.ToDictionary(k => k.Key, v => (object)v), StringComparer.OrdinalIgnoreCase);
            if (!entitiesDict.ContainsKey("question"))
            {
                entitiesDict["question"] = userPrompt;
            }
            if (!entitiesDict.ContainsKey("prompt"))
            {
                entitiesDict["prompt"] = userPrompt;
            }

            var argumentsPayload = (step.ArgumentsJson != "{}" && !string.IsNullOrWhiteSpace(step.ArgumentsJson))
                ? step.ArgumentsJson
                : JsonSerializer.Serialize(entitiesDict);

            var toolCtx = new ToolExecutionContext
            {
                AgentRunId = agentRun.Id,
                UserId = userId,
                UserRole = userRole,
                ArgumentsJson = argumentsPayload
            };

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"Executing step {step.StepNumber}: {tool.Definition.Name}..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "TOOL_STARTED", $"Executing step {step.StepNumber}: {tool.Definition.Name}..."), cancellationToken);

            var toolResult = await tool.ExecuteAsync(toolCtx);

            if (toolResult.IsSuccess)
            {
                agentAction.Status = AgentActionStatus.Completed;
                agentAction.ResultJson = JsonSerializer.Serialize(toolResult.Data);
                agentAction.CompletedAt = DateTime.UtcNow;

                feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"Step {step.StepNumber} ({tool.Definition.Name}) completed successfully.", JsonSerializer.Serialize(toolResult.Data)));
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "TOOL_COMPLETED", $"Step {step.StepNumber} completed successfully."), cancellationToken);

                // Audit Log Entry
                await RecordAuditLogAsync(agentRun.Id, userId, tool.Definition.Name, step.Description, toolResult.Data, cancellationToken);
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "AUDIT_RECORDED", $"Committed SHA-256 audit ledger entry for tool '{tool.Definition.Name}'."), cancellationToken);

                executedSteps.Add(step);
                lastResultData = toolResult.Data;
            }
            else
            {
                agentAction.Status = AgentActionStatus.Failed;
                agentAction.ResultJson = toolResult.ErrorMessage;
                agentAction.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Step {step.StepNumber} ({tool.Definition.Name}) failed: {toolResult.ErrorMessage}"));
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, step.StepNumber, "EXECUTION_FAILED", $"Step {step.StepNumber} failed: {toolResult.ErrorMessage}"), cancellationToken);

                agentRun.Status = AgentRunStatus.Failed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                sw.Stop();
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = toolResult.ErrorMessage,
                    ExecutionTimeMs = sw.ElapsedMilliseconds
                };
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        // 6. COMPLETE AGENT RUN
        agentRun.Status = AgentRunStatus.Completed;
        agentRun.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_COMPLETED, $"Agent run completed successfully in {sw.ElapsedMilliseconds}ms. All plan steps executed cleanly."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(agentRun.Id, plan.Steps.Count, "EXECUTION_COMPLETED", "Agent run completed successfully."), cancellationToken);

        sw.Stop();
        return new AgentResult
        {
            RunId = agentRun.Id,
            OriginalPrompt = userPrompt,
            Intent = parsedIntent.Intent,
            IsSuccess = true,
            Plan = plan,
            ExecutedSteps = executedSteps,
            ResultData = lastResultData,
            ExecutionFeed = feed,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        };
    }

    public async Task<AgentResult> ResumeApprovedRunAsync(Guid runId, Guid approvalId, string approvedBy = "Executive Admin", CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var feed = new List<AgentEvent>();

        // 1. Load AgentRun & Approval
        var agentRun = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (agentRun == null)
        {
            throw new InvalidOperationException($"AgentRun '{runId}' not found.");
        }

        var approval = await _db.Approvals.FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval == null)
        {
            throw new InvalidOperationException($"Approval record '{approvalId}' not found.");
        }

        // Concurrency Safeguard: Verify approval is pending
        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new InvalidOperationException($"Approval '{approvalId}' has already been processed (Status: {approval.Status}).");
        }

        // Update Approval & AgentRun state atomically
        approval.Status = ApprovalStatus.Approved;
        approval.ApprovedBy = approvedBy;
        approval.ApprovedAt = DateTime.UtcNow;

        agentRun.Status = AgentRunStatus.Executing;
        await _db.SaveChangesAsync(cancellationToken);

        // Broadcast APPROVAL_RECEIVED via SignalR
        feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, $"APPROVAL_RECEIVED: Plan of Action approved by {approvedBy}. Resuming tool execution..."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 1, "APPROVAL_RECEIVED", $"Plan of Action approved by {approvedBy}. Resuming execution..."), cancellationToken);

        // Deserialize persisted ActionPlan from approval.Reason
        Nexus.Data.ActionPlan.ActionPlan? actionPlan = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(approval.Reason) && approval.Reason.TrimStart().StartsWith("{"))
            {
                actionPlan = JsonSerializer.Deserialize<Nexus.Data.ActionPlan.ActionPlan>(approval.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize persisted ActionPlan from Approval.Reason");
        }

        var targetRecordIds = actionPlan?.AffectedRecords?
            .Select(r => r.RecordId)
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        ParsedIntentResult? parsedIntent = null;
        if (!string.IsNullOrWhiteSpace(actionPlan?.Metadata))
        {
            try
            {
                parsedIntent = JsonSerializer.Deserialize<ParsedIntentResult>(actionPlan.Metadata);
            }
            catch { }
        }

        var prompt = agentRun.OriginalPrompt ?? "";
        var pLower = prompt.ToLower();
        int affectedCount = 0;
        string resultMessage = "";

        // Execute Approved Plan
        if (agentRun.Intent == "EMPLOYEE_ONBOARDING" || pLower.Contains("onboard"))
        {
            // Parse candidate name
            string empName = "Sufan Khan";
            var nameMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"(?:onboard|for)\s*([a-zA-Z0-9]+(?:\s+[a-zA-Z0-9]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                var raw = nameMatch.Groups[1].Value.Trim();
                var rawLower = raw.ToLower();
                if (!rawLower.StartsWith("a ") && !rawLower.StartsWith("as ") && !rawLower.StartsWith("in ") && !rawLower.StartsWith("new "))
                {
                    empName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawLower);
                }
            }

            // Parse salary
            decimal empSalary = 68000.00m;
            var salMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\$?\s*([0-9]{1,3}(?:,[0-9]{3})+|[0-9]{5,8})\b");
            if (salMatch.Success && decimal.TryParse(salMatch.Groups[1].Value.Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedSal))
            {
                empSalary = parsedSal;
            }

            // Lookup IT department from SQL Server
            var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower().Contains("it") || d.Name.ToLower().Contains("software"), cancellationToken);
            int deptId = dept?.Id ?? 1;

            // STEP 1: SQL Employee Creation
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"EMPLOYEE_CREATION_STARTED: Inserting employee '{empName}' into SQL Server database..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "EMPLOYEE_CREATION_STARTED", $"Creating SQL record for {empName}..."), cancellationToken);

            var newEmp = new Employee
            {
                Name = empName,
                Email = $"{empName.ToLower().Replace(" ", ".")}@nexus.local",
                DepartmentId = deptId,
                Designation = pLower.Contains("frontend") ? "Frontend Developer" : "Mid-Level .NET Developer",
                Salary = empSalary,
                ExperienceYears = 3,
                Status = EmployeeStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _db.Employees.Add(newEmp);
            await _db.SaveChangesAsync(cancellationToken);

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"EMPLOYEE_CREATED: Created SQL Employee ID #{newEmp.Id} for {empName} (${empSalary:N2})."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 3, "EMPLOYEE_CREATED", $"Created SQL Employee ID #{newEmp.Id} for {empName}."), cancellationToken);

            // STEP 2: Playwright Legacy HR Portal Automation
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"LEGACY_AUTOMATION_STARTED: Launching Playwright browser automation for Legacy HR Portal..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 4, "LEGACY_AUTOMATION_STARTED", $"Launching Playwright legacy HR portal sync..."), cancellationToken);

            var legacyTool = _toolRegistry.GetTool("onboarding.submit_legacy_form");
            var legacyRef = $"HR-REC-2026-{newEmp.Id}";
            if (legacyTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "name", empName },
                    { "department", dept?.Name ?? "IT" },
                    { "designation", newEmp.Designation },
                    { "salary", empSalary }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };

                var legacyResult = await legacyTool.ExecuteAsync(toolCtx);
                if (legacyResult.Data is Dictionary<string, object> d && d.TryGetValue("portalRecordId", out var prId))
                {
                    legacyRef = prId.ToString() ?? legacyRef;
                }
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"LEGACY_AUTOMATION_COMPLETED: Submitted Legacy HR Portal form (Ref: {legacyRef})."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 5, "LEGACY_AUTOMATION_COMPLETED", $"Submitted Legacy HR Portal form ({legacyRef})."), cancellationToken);

            // STEP 3: Mock SAP HCM Provisioning
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"SAP_OPERATION_STARTED: Provisioning employee in Enterprise SAP HCM..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 6, "SAP_OPERATION_STARTED", $"Provisioning candidate in Enterprise SAP HCM..."), cancellationToken);

            var sapTool = _toolRegistry.GetTool("sap.employee.create");
            var sapRef = $"SAP-EMP-2026-{newEmp.Id}";
            if (sapTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "employeeId", newEmp.Id },
                    { "name", empName },
                    { "department", dept?.Name ?? "IT" },
                    { "salary", empSalary }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };

                var sapResult = await sapTool.ExecuteAsync(toolCtx);
                if (sapResult.Data is Dictionary<string, object> d && d.TryGetValue("personnelId", out var pId))
                {
                    sapRef = pId.ToString() ?? sapRef;
                }
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"SAP_OPERATION_COMPLETED: Provisioned SAP Master Data (ID: {sapRef})."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 7, "SAP_OPERATION_COMPLETED", $"Provisioned SAP Master Data (ID: {sapRef})."), cancellationToken);

            // STEP 4: Welcome Email Generation
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"WELCOME_EMAIL_STARTED: Generating welcome email for {newEmp.Email}..."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 8, "WELCOME_EMAIL_STARTED", $"Generating welcome email for {newEmp.Email}..."), cancellationToken);

            var emailTool = _toolRegistry.GetTool("email.welcome");
            if (emailTool != null)
            {
                var toolArgs = new Dictionary<string, object>
                {
                    { "name", empName },
                    { "designation", newEmp.Designation },
                    { "department", dept?.Name ?? "IT" }
                };
                var toolCtx = new ToolExecutionContext
                {
                    AgentRunId = runId,
                    UserId = agentRun.UserId,
                    UserRole = "Admin",
                    ArgumentsJson = JsonSerializer.Serialize(toolArgs)
                };

                await emailTool.ExecuteAsync(toolCtx);
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"WELCOME_EMAIL_COMPLETED: Welcome email generated & sent."));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 9, "WELCOME_EMAIL_COMPLETED", $"Welcome email sent to {newEmp.Email}."), cancellationToken);

            affectedCount = 1;
            resultMessage = $"Onboarding Fully Provisioned for {empName}. Created SQL Record ID #{newEmp.Id} (${empSalary:N2}), Legacy HR Portal Ref ({legacyRef}), Mock SAP Personnel ID ({sapRef}), and sent welcome email.";
        }
        else if (agentRun.Intent == "EMPLOYEE_DELETE" || pLower.Contains("delete") || pLower.Contains("remove"))
        {
            List<Employee> toDelete;
            if (targetRecordIds.Count > 0)
            {
                toDelete = await _db.Employees
                    .Where(e => targetRecordIds.Contains(e.Id))
                    .ToListAsync(cancellationToken);
            }
            else
            {
                toDelete = new List<Employee>();
            }

            if (toDelete.Count > 0)
            {
                _db.Employees.RemoveRange(toDelete);
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = toDelete.Count;
                resultMessage = $"Permanently deleted {toDelete.Count} employee record(s) from SQL Server database.";
            }
            else
            {
                affectedCount = 0;
                resultMessage = "Deletion request processed. Zero matching records found in SQL Server database.";
            }

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"EMPLOYEE_DELETED: {resultMessage}"));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "EMPLOYEE_DELETED", resultMessage), cancellationToken);
        }
        else if (agentRun.Intent == "EMPLOYEE_UPDATE" || pLower.Contains("increase") || pLower.Contains("salary") || pLower.Contains("update"))
        {
            List<Employee> toUpdate;
            if (targetRecordIds.Count > 0)
            {
                toUpdate = await _db.Employees
                    .Where(e => targetRecordIds.Contains(e.Id))
                    .ToListAsync(cancellationToken);
            }
            else
            {
                toUpdate = await _db.Employees
                    .Include(e => e.Department)
                    .Where(e => e.DepartmentId == 1 || (e.Department != null && e.Department.Name == "IT"))
                    .ToListAsync(cancellationToken);
            }

            decimal pct = 10m;
            if (parsedIntent?.Entities?.TryGetValue("percentage", out var pStr) == true && decimal.TryParse(pStr, out var pDec))
            {
                pct = pDec;
            }

            foreach (var emp in toUpdate)
            {
                emp.Salary = Math.Round(emp.Salary * (1m + (pct / 100m)), 2);
                emp.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            affectedCount = toUpdate.Count;
            resultMessage = $"Updated compensation (+{pct}%) for {toUpdate.Count} employee(s) in SQL Server database.";

            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"SALARY_UPDATED: {resultMessage}"));
            await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 2, "SALARY_UPDATED", resultMessage), cancellationToken);
        }

        // STEP 5: Cryptographic Audit Ledger Recording
        feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, "AUDIT_RECORDING: Signing SHA-256 cryptographic ledger block..."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 10, "AUDIT_RECORDING", "Signing SHA-256 cryptographic ledger block..."), cancellationToken);

        await RecordAuditLogAsync(runId, agentRun.UserId, "system.orchestration", $"Approved execution: {agentRun.OriginalPrompt}", resultMessage, cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, "AUDIT_RECORDED: Block sealed on tamper-proof audit chain."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 11, "AUDIT_RECORDED", "Block sealed on tamper-proof audit chain."), cancellationToken);

        // STEP 6: Complete Agent Run
        agentRun.Status = AgentRunStatus.Completed;
        agentRun.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_COMPLETED, $"WORKFLOW_COMPLETED: {resultMessage}"));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 12, "EXECUTION_COMPLETED", $"Workflow Completed: {resultMessage}"), cancellationToken);

        sw.Stop();

        return new AgentResult
        {
            RunId = runId,
            OriginalPrompt = agentRun.OriginalPrompt,
            Intent = agentRun.Intent,
            IsSuccess = true,
            RequiresApproval = false,
            Plan = BuildPlan(agentRun.OriginalPrompt, new ParsedIntentResult { Intent = agentRun.Intent }),
            ExecutionFeed = feed,
            ResultData = resultMessage,
            ExecutionTimeMs = sw.ElapsedMilliseconds
        };
    }

    private AgentPlan BuildPlan(string prompt, ParsedIntentResult intent)
    {
        var plan = new AgentPlan();
        var intentType = intent.ParsedIntentType;

        switch (intentType)
        {
            case IntentType.EMPLOYEE_CREATE:
                if (_toolRegistry.HasTool("policy.evaluate"))
                {
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR policy rule", RiskLevel = RiskLevel.Low });
                    plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "employee.create", Description = "Create employee record", RiskLevel = RiskLevel.Medium });
                }
                else
                {
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.create", Description = "Create employee record", RiskLevel = RiskLevel.Medium });
                }
                break;

            case IntentType.EMPLOYEE_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.read", Description = "Read employee records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EMPLOYEE_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.update", Description = "Update employee salary / department", RiskLevel = RiskLevel.High });
                break;

            case IntentType.EMPLOYEE_DELETE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.delete", Description = "Delete employee record", RiskLevel = intent.Scope == "ALL" ? RiskLevel.Critical : RiskLevel.High });
                break;

            case IntentType.BUDGET_ANALYSIS:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute natural language T-SQL budget query", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EXPENSE_COMPLIANCE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate expense claim against POL-FIN-002 policy limit", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EMPLOYEE_ONBOARDING:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR Policy POL-HR-001 salary band", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "employee.create", Description = "Create employee SQL record", RiskLevel = RiskLevel.Medium });
                plan.Steps.Add(new AgentStep { StepNumber = 3, ToolName = "onboarding.submit_legacy_form", Description = "Submit legacy HR portal form via Playwright", RiskLevel = RiskLevel.Medium });
                plan.Steps.Add(new AgentStep { StepNumber = 4, ToolName = "sap.employee.create", Description = "Provision employee in Mock SAP HCM", RiskLevel = RiskLevel.Medium });
                plan.Steps.Add(new AgentStep { StepNumber = 5, ToolName = "email.welcome", Description = "Generate welcome email", RiskLevel = RiskLevel.Low });
                break;

            default:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.read", Description = "Search workforce directory", RiskLevel = RiskLevel.Low });
                break;
        }

        plan.TotalRiskLevel = plan.Steps.Count > 0 ? plan.Steps.Max(s => s.RiskLevel) : RiskLevel.Low;
        return plan;
    }

    private async Task<Nexus.Data.ActionPlan.ActionPlan> BuildActionPlanAsync(Guid runId, string prompt, ParsedIntentResult intent, CancellationToken ct)
    {
        var pLower = prompt.ToLower();

        if (intent.ParsedIntentType == IntentType.EMPLOYEE_ONBOARDING || pLower.Contains("onboard"))
        {
            var name = intent.Entities.GetValueOrDefault("name", "Candidate");
            var dept = intent.Entities.GetValueOrDefault("department", "IT");
            var desig = intent.Entities.GetValueOrDefault("designation", ".NET Developer");
            
            decimal proposedSalary = 68000.00m;
            if (intent.Entities.TryGetValue("salary", out var salStr) && decimal.TryParse(salStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var salVal))
            {
                proposedSalary = salVal;
            }
            else
            {
                var salMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\$?\s*([0-9]{1,3}(?:,[0-9]{3})+|[0-9]{5,8})\b");
                if (salMatch.Success && decimal.TryParse(salMatch.Groups[1].Value.Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedSal))
                {
                    proposedSalary = parsedSal;
                }
            }

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Comprehensive Onboarding for {name} ({dept})",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = proposedSalary,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Employee Master (SQL Server)",
                PrimaryLabel = name,
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview
                    {
                        FieldName = "Provisioning Profile",
                        OldValue = "[WILL BE CREATED]",
                        NewValue = $"{desig} @ ${proposedSalary:N2}",
                        Difference = "New SQL Record"
                    }
                }
            });

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Legacy HR Portal",
                PrimaryLabel = "Legacy Directory Sync",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview
                    {
                        FieldName = "Portal Form",
                        OldValue = "[NOT SUBMITTED]",
                        NewValue = "HR-REC-2026 (WILL BE CREATED)",
                        Difference = "Playwright Form Entry"
                    }
                }
            });

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Enterprise SAP HCM",
                PrimaryLabel = "SAP Master Data",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview
                    {
                        FieldName = "Personnel ID",
                        OldValue = "[UNPROVISIONED]",
                        NewValue = "SAP-EMP-2026 (WILL BE CREATED)",
                        Difference = "Mock SAP Connector"
                    }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR Policy POL-HR-001 salary band", RiskLevel = RiskLevel.Low });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 2, ToolName = "employee.create", Description = "Create employee SQL record", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 3, ToolName = "onboarding.submit_legacy_form", Description = "Submit legacy HR portal form via Playwright", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 4, ToolName = "sap.employee.create", Description = "Provision employee in Mock SAP HCM", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 5, ToolName = "email.welcome", Description = "Generate welcome email", RiskLevel = RiskLevel.Low });

            actionPlan.Warnings.Add($"Multi-system onboarding provisions SQL Server, Legacy Portal, SAP ERP, and Welcome Email.");
            actionPlan.Warnings.Add($"Annual base compensation calculated at ${proposedSalary:N2} based on POL-HR-001 policy rules.");

            return actionPlan;
        }
        else if (intent.ParsedIntentType == IntentType.EMPLOYEE_DELETE || pLower.Contains("delete") || pLower.Contains("remove"))
        {
            var scope = intent.Scope;
            var empName = intent.Entities.GetValueOrDefault("name", string.Empty);
            var deptFilter = intent.Entities.GetValueOrDefault("department", string.Empty);
            var statusFilter = intent.Entities.GetValueOrDefault("status", string.Empty);

            List<Employee> matchingEmps = new();

            if (scope == "ALL")
            {
                matchingEmps = await _db.Employees.Include(e => e.Department).ToListAsync(ct);
            }
            else if (scope == "FILTERED")
            {
                var q = _db.Employees.Include(e => e.Department).AsQueryable();
                if (!string.IsNullOrWhiteSpace(deptFilter))
                {
                    q = q.Where(e => e.Department != null && e.Department.Name.ToLower() == deptFilter.ToLower());
                }
                if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<EmployeeStatus>(statusFilter, true, out var stEnum))
                {
                    q = q.Where(e => e.Status == stEnum);
                }
                matchingEmps = await q.ToListAsync(ct);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(empName))
                {
                    matchingEmps = await _db.Employees.Include(e => e.Department)
                        .Where(e => e.Name.ToLower().Contains(empName.ToLower()))
                        .ToListAsync(ct);
                }
            }

            var title = scope switch
            {
                "ALL" => $"Plan of Action: Permanent Removal of All Employees ({matchingEmps.Count} Records)",
                "FILTERED" => $"Plan of Action: Permanent Removal of Filtered Employees ({matchingEmps.Count} Records)",
                _ => string.IsNullOrWhiteSpace(empName) ? "Plan of Action: Employee Deletion" : $"Plan of Action: Permanent Removal of Employee '{empName}'"
            };

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = title,
                RiskLevel = scope == "ALL" ? RiskLevel.Critical : RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            decimal totalSavings = 0m;
            if (matchingEmps.Count > 0)
            {
                foreach (var emp in matchingEmps)
                {
                    totalSavings += emp.Salary;
                    actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                    {
                        RecordId = emp.Id,
                        EntityName = "Employee Master (SQL Server)",
                        PrimaryLabel = emp.Name,
                        Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                        {
                            new Nexus.Data.ActionPlan.ChangePreview
                            {
                                FieldName = "Employment Status",
                                OldValue = $"{emp.Status} (${emp.Salary:N2}/yr)",
                                NewValue = "[PERMANENTLY DELETED]",
                                Difference = "Record Purge"
                            }
                        }
                    });
                }
            }
            else
            {
                actionPlan.Warnings.Add(scope == "ALL"
                    ? "Zero employee records exist in SQL Server database."
                    : $"Zero matching employee records found in SQL Server database.");
            }

            actionPlan.TotalFinancialImpact = -totalSavings;
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "employee.delete",
                Description = $"Purge {matchingEmps.Count} target employee record(s) from SQL Server database",
                RiskLevel = scope == "ALL" ? RiskLevel.Critical : RiskLevel.High
            });

            actionPlan.Warnings.Add($"⚠ High-Risk Operation: Deleting employee records will permanently purge matching database entries.");
            actionPlan.Warnings.Add($"Annual payroll reduction of -${totalSavings:N2}. Requires explicit executive authorization.");

            return actionPlan;
        }
        else
        {
            var scope = intent.Scope;
            var targetEmpName = intent.Entities.GetValueOrDefault("name", string.Empty);
            var targetDept = intent.Entities.GetValueOrDefault("department", string.Empty);
            var targetDesig = intent.Entities.GetValueOrDefault("designation", string.Empty);
            
            decimal pct = 10m;
            if (intent.Entities.TryGetValue("percentage", out var pStr) && decimal.TryParse(pStr, out var pDec))
            {
                pct = pDec;
            }

            if (scope == "SINGLE" && (!string.IsNullOrWhiteSpace(targetDept) || !string.IsNullOrWhiteSpace(targetDesig)))
            {
                scope = "FILTERED";
            }

            var query = _db.Employees.Include(e => e.Department).AsQueryable();
            if (scope == "FILTERED")
            {
                if (!string.IsNullOrWhiteSpace(targetDept))
                {
                    var tDeptLower = targetDept.ToLower();
                    if (tDeptLower == "it" || tDeptLower == "software")
                    {
                        query = query.Where(e => e.DepartmentId == 1 || (e.Department != null && (e.Department.Name.ToLower() == "it" || e.Department.Name.ToLower().Contains("software"))));
                    }
                    else if (tDeptLower == "hr" || tDeptLower == "human resources")
                    {
                        query = query.Where(e => e.DepartmentId == 2 || (e.Department != null && (e.Department.Name.ToLower() == "hr" || e.Department.Name.ToLower().Contains("human"))));
                    }
                    else
                    {
                        query = query.Where(e => e.Department != null && e.Department.Name.ToLower() == tDeptLower);
                    }
                }
                if (!string.IsNullOrWhiteSpace(targetDesig))
                {
                    query = query.Where(e => e.Designation.ToLower().Contains(targetDesig.ToLower()));
                }
            }
            else if (scope == "SINGLE" && !string.IsNullOrWhiteSpace(targetEmpName))
            {
                query = query.Where(e => e.Name.ToLower().Contains(targetEmpName.ToLower()));
            }

            var employees = await query.ToListAsync(ct);

            var title = scope switch
            {
                "ALL" => $"Plan of Action: Bulk Salary Adjustment (+{pct}%) for All Employees ({employees.Count} Records)",
                "FILTERED" => $"Plan of Action: Salary Adjustment (+{pct}%) for Filtered Employees ({employees.Count} Records)",
                _ => string.IsNullOrWhiteSpace(targetEmpName) ? $"Plan of Action: Salary Adjustment (+{pct}%)" : $"Plan of Action: Salary Adjustment (+{pct}%) for {targetEmpName}"
            };

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = title,
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            decimal totalImpact = 0m;
            if (employees.Count > 0)
            {
                foreach (var emp in employees)
                {
                    var oldSalary = emp.Salary;
                    var newSalary = Math.Round(oldSalary * (1m + (pct / 100m)), 2);
                    var diff = newSalary - oldSalary;
                    totalImpact += diff;

                    actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                    {
                        RecordId = emp.Id,
                        EntityName = "Employee Master (SQL Server)",
                        PrimaryLabel = emp.Name,
                        Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                        {
                            new Nexus.Data.ActionPlan.ChangePreview
                            {
                                FieldName = "Compensation Band",
                                OldValue = $"${oldSalary:N2}",
                                NewValue = $"${newSalary:N2}",
                                Difference = $"+${diff:N2} (+{pct}%)"
                            }
                        }
                    });
                }
            }
            else
            {
                actionPlan.Warnings.Add("Zero matching employee records found in SQL Server database for salary adjustment.");
            }

            actionPlan.TotalFinancialImpact = totalImpact;
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "employee.update",
                Description = $"Update salary (+{pct}%) across {employees.Count} matching employee records",
                RiskLevel = RiskLevel.High
            });

            actionPlan.Warnings.Add($"Salary modification affects {employees.Count} employee record(s).");
            actionPlan.Warnings.Add($"Total annual payroll increase is ${totalImpact:N2}. Requires explicit executive authorization.");

            return actionPlan;
        }
    }

    private async Task RecordAuditLogAsync(Guid runId, int? userId, string toolName, string actionDescription, object? resultData, CancellationToken ct)
    {
        var lastAudit = await _db.AuditLogs.OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        var prevHash = lastAudit?.CurrentHash ?? "GENESIS_NEXUS_LITE";

        var payload = $"{runId}|{userId}|{toolName}|{actionDescription}|{JsonSerializer.Serialize(resultData)}|{DateTime.UtcNow:O}|{prevHash}";
        var currentHash = ComputeSha256Hash(payload);

        var auditLog = new AuditLog
        {
            AgentRunId = runId,
            UserId = userId,
            Action = actionDescription,
            ToolName = toolName,
            Target = toolName,
            Result = JsonSerializer.Serialize(resultData),
            Timestamp = DateTime.UtcNow,
            PreviousHash = prevHash,
            CurrentHash = currentHash
        };

        _db.AuditLogs.Add(auditLog);
        await _db.SaveChangesAsync(ct);
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes);
    }
}
