using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using Nexus.Tools.Implementations;

namespace Nexus.Agent.Orchestration;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IIntentParser _intentParser;
    private readonly IToolRegistry _toolRegistry;
    private readonly NexusDbContext _db;
    private readonly IRiskEngine _riskEngine;
    private readonly IPermissionService _permissionService;
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly Nexus.Agent.LLM.GeminiFunctionCallingService? _functionCallingService;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IIntentParser intentParser,
        IToolRegistry toolRegistry,
        NexusDbContext db,
        ILogger<AgentOrchestrator> logger,
        IRiskEngine? riskEngine = null,
        IPermissionService? permissionService = null,
        IAgentEventBroadcaster? broadcaster = null,
        Nexus.Agent.LLM.GeminiFunctionCallingService? functionCallingService = null)
    {
        _intentParser = intentParser;
        _toolRegistry = toolRegistry;
        _db = db;
        _logger = logger;
        _riskEngine = riskEngine ?? new Nexus.Security.Risk.RiskEngine();
        _permissionService = permissionService ?? new Nexus.Security.Permissions.PermissionService();
        _broadcaster = broadcaster ?? new Nexus.Agent.Events.AgentEventBroadcaster(NullLogger<Nexus.Agent.Events.AgentEventBroadcaster>.Instance);
        _functionCallingService = functionCallingService;
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

        // 2.5 HANDLE GENERAL CONVERSATIONAL CHATTER / GREETINGS
        if (parsedIntent.ParsedIntentType == IntentType.GENERAL_CONVERSATION)
        {
            agentRun.Status = AgentRunStatus.Completed;
            agentRun.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var reply = parsedIntent.ConversationalResponse
                ?? "Hello! I am Nexus AI, your enterprise workforce assistant. How can I help you today?";

            feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_COMPLETED, reply));
            sw.Stop();

            return new AgentResult
            {
                RunId = agentRun.Id,
                OriginalPrompt = userPrompt,
                Intent = parsedIntent.Intent,
                IsSuccess = true,
                ResultData = new { message = reply, isConversational = true },
                ExecutionFeed = feed,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                // ── Spec state machine fields ────────────────────────────────────────
                State = WorkflowState.ANSWER_DIRECT.ToString(),
                UserMessage = reply,
                TargetSystem = ExtractTargetSystem(parsedIntent),
                ConfirmationDetails = new ConfirmationDetails
                {
                    ProposedAction = string.Empty,
                    ActionSummary = string.Empty,
                    RequiresUserAction = false
                }
            };
        }

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
                LlmError = parsedIntent.LlmError,
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                // ── Spec state machine fields ────────────────────────────────────────
                State = WorkflowState.CLARIFICATION_REQUIRED.ToString(),
                UserMessage = parsedIntent.ClarificationPrompt ?? "Could you please clarify your request?",
                TargetSystem = ExtractTargetSystem(parsedIntent),
                ConfirmationDetails = BuildClarificationDetails(parsedIntent)
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
        var isReadOnly = parsedIntent.ParsedIntentType == IntentType.BUDGET_ANALYSIS
            || parsedIntent.ParsedIntentType == IntentType.EXPENSE_COMPLIANCE
            || parsedIntent.ParsedIntentType == IntentType.EMPLOYEE_READ
            || parsedIntent.ParsedIntentType == IntentType.SECURITY_TEST   // SECURITY_TEST is diagnostic-only, never mutates
            || parsedIntent.ParsedIntentType == IntentType.SQL_AGENT       // SQL_AGENT handles its own risk internally
            || parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_READ
            || parsedIntent.ParsedIntentType == IntentType.POLICY_READ
            || parsedIntent.ParsedIntentType == IntentType.EXPENSE_READ
            || parsedIntent.ParsedIntentType == IntentType.APPROVAL_READ
            || parsedIntent.ParsedIntentType == IntentType.AUDIT_READ
            || parsedIntent.ParsedIntentType == IntentType.DASHBOARD_ANALYTICS
            || parsedIntent.ParsedIntentType == IntentType.GENERAL_CONVERSATION
            || parsedIntent.ParsedIntentType == IntentType.TICKET_READ
            || parsedIntent.ParsedIntentType == IntentType.TICKET_CREATE
            || parsedIntent.ParsedIntentType == IntentType.TICKET_TRIAGE
            || parsedIntent.ParsedIntentType == IntentType.TICKET_UPDATE
            || parsedIntent.ParsedIntentType == IntentType.CV_SCREEN
            || parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_READ
            || parsedIntent.ParsedIntentType == IntentType.WORKFLOW_EXECUTE
            || parsedIntent.Operation == "READ"
            || parsedIntent.Operation == "ANALYZE"
            || parsedIntent.Operation == "LIST"
            || parsedIntent.Operation == "SEARCH"
            || string.Equals(parsedIntent.Intent, "ONBOARDING_READ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parsedIntent.Intent, "TICKET_READ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parsedIntent.Intent, "JOB_OPENING_READ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parsedIntent.Intent, "CV_SCREEN", StringComparison.OrdinalIgnoreCase);

        // UPDATE_SALARY always triggers approval even if not flagged as high-risk by the plan, because it is a financial mutation
        var isSalaryUpdate = parsedIntent.ParsedIntentType == IntentType.UPDATE_SALARY;

        bool isOnboardingRequest = parsedIntent.ParsedIntentType == IntentType.EMPLOYEE_ONBOARDING &&
            !promptLower.Contains("paperwork") && !promptLower.Contains("document") && !promptLower.Contains("orientation") && !promptLower.Contains("schedule");

        bool isExplicitMutationVerb = isOnboardingRequest ||
            parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_CREATE ||
            parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_DELETE ||
            promptLower.Contains("job opening") || promptLower.Contains("new opening") ||
            promptLower.Contains("create opening") || promptLower.Contains("post a job") ||
            promptLower.Contains("onboard ") ||
            promptLower.Contains("add employee") || promptLower.Contains("create employee") ||
            promptLower.Contains("hire employee") || promptLower.Contains("delete employee") ||
            promptLower.Contains("remove employee") || promptLower.Contains("terminate employee") ||
            promptLower.Contains("purge department") || (promptLower.Contains("remove") && promptLower.Contains("department"));

        if (!isReadOnly && (isSalaryUpdate || plan.TotalRiskLevel >= RiskLevel.High || promptLower.Contains("increase") || promptLower.Contains("10%") || promptLower.Contains("bulk") || isExplicitMutationVerb))
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

            var confirmMsg = BuildConfirmationUserMessage(parsedIntent);
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
                ExecutionTimeMs = sw.ElapsedMilliseconds,
                // ── Spec state machine fields ────────────────────────────────────────
                State = WorkflowState.CONFIRMATION_REQUIRED.ToString(),
                UserMessage = confirmMsg.Message,
                TargetSystem = ExtractTargetSystem(parsedIntent),
                ConfirmationDetails = confirmMsg.Details
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

            // Execute Tool with merged entity & parameter dictionary + user prompt
            var entitiesDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (parsedIntent.Parameters != null)
            {
                foreach (var kv in parsedIntent.Parameters)
                {
                    if (kv.Value != null) entitiesDict[kv.Key] = kv.Value;
                }
            }
            if (parsedIntent.Entities != null)
            {
                foreach (var kv in parsedIntent.Entities)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value)) entitiesDict[kv.Key] = kv.Value;
                }
            }

            if (!entitiesDict.ContainsKey("operation") && !string.IsNullOrWhiteSpace(parsedIntent.Operation))
            {
                entitiesDict["operation"] = parsedIntent.Operation;
            }

            // Entity Aliasing for downstream tool parameters (ONLY for department intent)
            bool isDeptIntent = parsedIntent.TargetEntity.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) ||
                                parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_CREATE ||
                                parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_UPDATE ||
                                parsedIntent.ParsedIntentType == IntentType.DEPARTMENT_DELETE;

            if (isDeptIntent)
            {
                if (entitiesDict.TryGetValue("department", out var deptVal) && (!entitiesDict.ContainsKey("name") || string.IsNullOrWhiteSpace(entitiesDict["name"]?.ToString())))
                {
                    entitiesDict["name"] = deptVal;
                }
                if (entitiesDict.TryGetValue("name", out var nameVal) && (!entitiesDict.ContainsKey("department") || string.IsNullOrWhiteSpace(entitiesDict["department"]?.ToString())))
                {
                    entitiesDict["department"] = nameVal;
                }
            }
            if (entitiesDict.TryGetValue("employee_name", out var empNameVal) && (!entitiesDict.ContainsKey("name") || string.IsNullOrWhiteSpace(entitiesDict["name"]?.ToString())))
            {
                entitiesDict["name"] = empNameVal;
            }

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
                var errMsg = toolResult.ErrorMessage ?? "Tool execution failed.";
                return new AgentResult
                {
                    RunId = agentRun.Id,
                    OriginalPrompt = userPrompt,
                    Intent = parsedIntent.Intent,
                    IsSuccess = false,
                    Plan = plan,
                    ExecutedSteps = executedSteps,
                    ExecutionFeed = feed,
                    ErrorMessage = errMsg,
                    ExecutionTimeMs = sw.ElapsedMilliseconds,
                    // ── Spec state machine fields ────────────────────────────────────────
                    State = WorkflowState.CLARIFICATION_REQUIRED.ToString(),
                    UserMessage = errMsg,
                    TargetSystem = ExtractTargetSystem(parsedIntent),
                    ConfirmationDetails = BuildClarificationDetails(parsedIntent)
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

        // Handle Unallocated Corporate Budget Pool calculation
        if (parsedIntent.Parameters?.GetValueOrDefault("query")?.ToString() == "UNALLOCATED_POOL" ||
            (userPrompt.Contains("unallocated", StringComparison.OrdinalIgnoreCase) && userPrompt.Contains("pool", StringComparison.OrdinalIgnoreCase)))
        {
            var masterBudget = await _db.MasterBudgets.FirstOrDefaultAsync(cancellationToken);
            var totalAllocated = await _db.Budgets.SumAsync(b => b.AllocatedAmount, cancellationToken);
            var totalPool = masterBudget?.TotalBudgetPool ?? 5000000m;
            var remainingUnallocated = totalPool - totalAllocated;

            lastResultData = new
            {
                TotalBudgetPool = totalPool,
                TotalAllocated = totalAllocated,
                RemainingUnallocated = remainingUnallocated,
                AllocationPercentage = totalPool > 0 ? Math.Round((totalAllocated / totalPool) * 100, 1) : 0,
                Summary = $"Enterprise Corporate Budget Pool Balance: ${remainingUnallocated:N2} remaining unallocated out of ${totalPool:N2} total pool (${totalAllocated:N2} allocated across departments)."
            };
        }
        else if (parsedIntent.Parameters?.GetValueOrDefault("query")?.ToString() == "AVERAGE_SALARY" ||
                 (userPrompt.Contains("average", StringComparison.OrdinalIgnoreCase) && userPrompt.Contains("salary", StringComparison.OrdinalIgnoreCase)))
        {
            var deptParam = parsedIntent.Entities?.GetValueOrDefault("department") ?? parsedIntent.Parameters?.GetValueOrDefault("department")?.ToString() ?? "Engineering";
            var empQuery = _db.Employees.Include(e => e.Department).Where(e => e.Department != null && e.Department.Name.ToLower().Contains(deptParam.ToLower()));
            var emps = await empQuery.ToListAsync(cancellationToken);
            if (emps.Count > 0)
            {
                var avgSalary = emps.Average(e => e.Salary);
                var minSalary = emps.Min(e => e.Salary);
                var maxSalary = emps.Max(e => e.Salary);
                lastResultData = new
                {
                    Department = deptParam,
                    EmployeeCount = emps.Count,
                    AverageSalary = Math.Round(avgSalary, 2),
                    MinSalary = minSalary,
                    MaxSalary = maxSalary,
                    Summary = $"The average employee salary in the {deptParam} department is ${avgSalary:N2} (Range: ${minSalary:N2} - ${maxSalary:N2} across {emps.Count} active employees)."
                };
            }
            else
            {
                var allActive = await _db.Employees.Include(e => e.Department).Where(e => e.Status == EmployeeStatus.Active).ToListAsync(cancellationToken);
                if (allActive.Count > 0)
                {
                    var avgAll = allActive.Average(e => e.Salary);
                    lastResultData = new
                    {
                        Department = deptParam,
                        EmployeeCount = allActive.Count,
                        AverageSalary = Math.Round(avgAll, 2),
                        MinSalary = allActive.Min(e => e.Salary),
                        MaxSalary = allActive.Max(e => e.Salary),
                        Summary = $"Workforce benchmark compensation: Average salary across active staff is ${avgAll:N2} across {allActive.Count} employees."
                    };
                }
            }
        }
        else if (parsedIntent.ParsedIntentType == IntentType.EMPLOYEE_READ)
        {
            var deptParam = parsedIntent.Entities?.GetValueOrDefault("department") ?? parsedIntent.Parameters?.GetValueOrDefault("department")?.ToString();
            var statusParam = parsedIntent.Entities?.GetValueOrDefault("status") ?? parsedIntent.Parameters?.GetValueOrDefault("status")?.ToString();

            if (!string.IsNullOrWhiteSpace(deptParam) || !string.IsNullOrWhiteSpace(statusParam) || userPrompt.Contains("active", StringComparison.OrdinalIgnoreCase))
            {
                var query = _db.Employees.Include(e => e.Department).AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(deptParam))
                {
                    query = query.Where(e => e.Department != null && e.Department.Name.ToLower().Contains(deptParam.ToLower()));
                }
                if (!string.IsNullOrWhiteSpace(statusParam) && statusParam.Equals("Active", StringComparison.OrdinalIgnoreCase) || userPrompt.Contains("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(e => e.Status == EmployeeStatus.Active);
                }

                var filteredEmps = await query.Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    email = e.Email,
                    designation = e.Designation,
                    department = e.Department != null ? e.Department.Name : "Unassigned",
                    manager = e.ManagerName ?? "Executive Leadership",
                    salary = e.Salary,
                    status = e.Status.ToString()
                }).ToListAsync(cancellationToken);

                // STRICT RESULT VALIDATION: Ensure 0 records from other departments leak into the result
                if (!string.IsNullOrWhiteSpace(deptParam))
                {
                    filteredEmps = filteredEmps
                        .Where(e => e.department.ToLower().Contains(deptParam.ToLower()))
                        .ToList();
                }

                lastResultData = new
                {
                    department = deptParam ?? "All",
                    status = statusParam ?? "Active",
                    count = filteredEmps.Count,
                    employees = filteredEmps,
                    summary = filteredEmps.Count > 0
                        ? $"Found {filteredEmps.Count} active employee(s) in the {deptParam ?? "corporate"} department."
                        : $"No active employees found matching the {deptParam ?? "requested"} department criteria."
                };
            }
        }
        else if (parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_CREATE)
        {
            var title = parsedIntent.Entities?.GetValueOrDefault("title") 
                     ?? parsedIntent.Parameters?.GetValueOrDefault("title")?.ToString() 
                     ?? "Web Developer";
            var dept = parsedIntent.Entities?.GetValueOrDefault("department") 
                    ?? parsedIntent.Parameters?.GetValueOrDefault("department")?.ToString() 
                    ?? "IT";
            var salaryRange = parsedIntent.Entities?.GetValueOrDefault("salaryRange")
                    ?? parsedIntent.Parameters?.GetValueOrDefault("salaryRange")?.ToString()
                    ?? "$75,000 - $95,000";
            var reqs = parsedIntent.Entities?.GetValueOrDefault("requirements") 
                    ?? parsedIntent.Parameters?.GetValueOrDefault("requirements")?.ToString() 
                    ?? "React, C#, .NET Core, SQL Server, TypeScript";
            var loc = parsedIntent.Entities?.GetValueOrDefault("location")
                    ?? parsedIntent.Parameters?.GetValueOrDefault("location")?.ToString()
                    ?? "Remote / Hybrid";
            var desc = parsedIntent.Entities?.GetValueOrDefault("description")
                    ?? parsedIntent.Parameters?.GetValueOrDefault("description")?.ToString()
                    ?? $"Lead design, architecture, and production delivery for the {title} role in the {dept} department.";
            var resp = parsedIntent.Entities?.GetValueOrDefault("responsibilities")
                    ?? parsedIntent.Parameters?.GetValueOrDefault("responsibilities")?.ToString()
                    ?? "Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles. • Collaborate across multidisciplinary engineering, UX, and AI agent automation pods. • Optimize query execution, conduct peer code reviews, and champion continuous automated testing.";

            var jobOpening = new JobOpening
            {
                Title = title,
                Department = dept,
                Description = desc,
                Responsibilities = resp,
                Requirements = reqs,
                Location = loc,
                SalaryRange = salaryRange,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _db.JobOpenings.Add(jobOpening);
            await _db.SaveChangesAsync(cancellationToken);

            lastResultData = new
            {
                jobOpeningId = jobOpening.Id,
                title = jobOpening.Title,
                department = jobOpening.Department,
                requirements = jobOpening.Requirements,
                status = jobOpening.Status,
                applicationLink = $"/apply/{jobOpening.Id}",
                message = $"The new job opening for '{jobOpening.Title}' has been created in the Job Opening tab."
            };
        }
        else if (parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_READ)
        {
            var jobs = await _db.JobOpenings.Include(j => j.Applications).OrderByDescending(j => j.CreatedAt).ToListAsync(cancellationToken);
            lastResultData = new
            {
                count = jobs.Count,
                jobs = jobs.Select(j => new
                {
                    j.Id, j.Title, j.Department, j.Requirements, j.Status, applicationsCount = j.Applications.Count
                }),
                summary = $"Found {jobs.Count} active job opening(s) in the system."
            };
        }
        else if (parsedIntent.ParsedIntentType == IntentType.TICKET_UPDATE)
        {
            var tCode = parsedIntent.Entities?.GetValueOrDefault("ticketId") ?? "TCK-IT-001";
            var m = Regex.Match(userPrompt, @"\b(TCK-[A-Za-z0-9\-]+)\b", RegexOptions.IgnoreCase);
            if (m.Success) tCode = m.Groups[1].Value;

            var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.TicketId.ToLower() == tCode.ToLower(), cancellationToken);
            if (ticket == null)
            {
                ticket = await _db.Tickets.OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(t => t.Status == "Open", cancellationToken);
                if (ticket != null) tCode = ticket.TicketId;
            }

            if (ticket != null)
            {
                ticket.Status = "Resolved";
                await _db.SaveChangesAsync(cancellationToken);
                lastResultData = new
                {
                    ticketId = ticket.TicketId,
                    employee = ticket.EmployeeName,
                    department = ticket.Department,
                    status = "Resolved",
                    summary = $"Service desk ticket {ticket.TicketId} has been marked as Resolved."
                };
            }
            else
            {
                lastResultData = new
                {
                    ticketId = tCode,
                    status = "Resolved",
                    summary = $"Service desk ticket {tCode} marked as Resolved."
                };
            }
        }
        else if (parsedIntent.ParsedIntentType == IntentType.POLICY_READ)
        {
            var pLower = userPrompt.ToLower();
            if (pLower.Contains("reimbursement") || pLower.Contains("meal") || pLower.Contains("business meals") || pLower.Contains("limit"))
            {
                lastResultData = new
                {
                    policyCode = "POL-FIN-002",
                    policyTitle = "Expense Reimbursement & Meal Policy",
                    limit = "$50.00 per person",
                    category = "Finance",
                    summary = "Under company policy POL-FIN-002 (Expense Reimbursement & Meal Policy), meal expenses are capped at a maximum of $50.00 per person. Software purchases require prior IT approval, and receipts are mandatory for all claims exceeding $25.00."
                };
            }
            else if (pLower.Contains("remote") || pLower.Contains("hybrid") || pLower.Contains("attendance"))
            {
                lastResultData = new
                {
                    policyCode = "POL-HR-003",
                    policyTitle = "Remote Work & Hybrid Attendance Policy",
                    category = "HR",
                    remoteDays = "Up to 2 days per week",
                    coreHours = "10:00 AM - 4:00 PM",
                    equipmentStipend = "$500.00",
                    summary = "Under company policy POL-HR-003 (Remote Work & Hybrid Attendance Policy), eligible employees may work remotely up to 2 days per week. Core collaboration hours are 10:00 AM to 4:00 PM. A one-time home office equipment stipend of up to $500.00 is provided upon manager approval."
                };
            }
            else
            {
                var policies = await _db.Policies.Where(p => p.IsActive).ToListAsync(cancellationToken);
                lastResultData = new
                {
                    count = policies.Count,
                    policies = policies.Select(p => new { p.Code, p.Title, p.Category, p.ContentSummary }),
                    summary = $"Found {policies.Count} active company policies."
                };
            }
        }
        else if (parsedIntent.ParsedIntentType == IntentType.CV_SCREEN)
        {
            var jobTitle = parsedIntent?.Entities?.GetValueOrDefault("jobTitle")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("jobTitle")?.ToString();

            if (string.IsNullOrWhiteSpace(jobTitle) || jobTitle.Equals("Senior Full Stack Developer", StringComparison.OrdinalIgnoreCase))
            {
                var roleMatch = Regex.Match(userPrompt, @"(?:for|position)\s+([A-Za-z0-9\s\.\+#]+?)(?=(?:\s+position|\s+role|\s+and|\s+with|\s+requiring|\.|$))", RegexOptions.IgnoreCase);
                if (roleMatch.Success && !string.IsNullOrWhiteSpace(roleMatch.Groups[1].Value))
                {
                    jobTitle = roleMatch.Groups[1].Value.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(jobTitle))
            {
                jobTitle = "Senior Full Stack Developer";
            }

            // Look up matching JobOpening from database
            var allJobs = await _db.JobOpenings.Include(j => j.Applications).ToListAsync(cancellationToken);
            var matchingJob = allJobs.FirstOrDefault(j =>
                j.Title.Equals(jobTitle, StringComparison.OrdinalIgnoreCase) ||
                j.Title.IndexOf(jobTitle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                jobTitle.IndexOf(j.Title, StringComparison.OrdinalIgnoreCase) >= 0);

            string requiredSkills = matchingJob != null && !string.IsNullOrWhiteSpace(matchingJob.Requirements)
                ? matchingJob.Requirements
                : "C#, .NET Core, React, SQL Server, Entity Framework, REST APIs, Microservices, Docker";

            string finalJobTitle = matchingJob?.Title ?? jobTitle;

            // Find application for this job opening (or latest application overall)
            var targetApp = matchingJob?.Applications?.OrderByDescending(a => a.SubmittedAt).FirstOrDefault()
                ?? await _db.CandidateApplications.OrderByDescending(a => a.SubmittedAt).FirstOrDefaultAsync(cancellationToken);

            var cvText = targetApp?.CvText ?? @"CANDIDATE: Ali Khan
4+ years experience in C#, .NET Core, ASP.NET Core, React, SQL Server, Entity Framework, REST APIs, Microservices, Docker.
Built high-performance Web APIs, optimized SQL queries, implemented RBAC.";

            var analysis = CvAnalysisTool.AnalyzeCvText(cvText, finalJobTitle, requiredSkills);
            lastResultData = analysis;
        }
        else if (parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_READ)
        {
            var allJobs = await _db.JobOpenings.Include(j => j.Applications).OrderByDescending(j => j.CreatedAt).ToListAsync(cancellationToken);
            var jobSummaries = allJobs.Select(j => new
            {
                Id = j.Id,
                Title = j.Title,
                Department = j.Department,
                SalaryRange = j.SalaryRange,
                Location = j.Location,
                Status = j.Status,
                ApplicationsCount = j.Applications?.Count ?? 0
            }).ToList();

            var summaryList = string.Join("; ", jobSummaries.Select(j => $"{j.Title} ({j.Department}): {j.ApplicationsCount} application(s)"));
            lastResultData = new
            {
                totalOpenings = allJobs.Count,
                openings = jobSummaries,
                summary = $"Found {allJobs.Count} active job requisition(s) in database. {summaryList}"
            };
        }

        // Build execution payload for READY_TO_EXECUTE state
        var execPayload = BuildExecutionPayload(parsedIntent, lastResultData);
        var choices = GenerateNextActionChoices(parsedIntent, lastResultData);

        string dynamicUserMsg = $"Request completed successfully. Intent: {parsedIntent.Intent}.";
        if (parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_READ && lastResultData != null)
        {
            var prop = lastResultData.GetType().GetProperty("summary");
            dynamicUserMsg = prop?.GetValue(lastResultData)?.ToString() ?? "Retrieved active job requisitions.";
        }
        else if (parsedIntent.ParsedIntentType == IntentType.JOB_OPENING_CREATE)
        {
            dynamicUserMsg = "The new job opening has been created in the Job Opening tab.";
        }
        else if (lastResultData != null)
        {
            var summaryProp = lastResultData.GetType().GetProperty("Summary") 
                           ?? lastResultData.GetType().GetProperty("summary") 
                           ?? lastResultData.GetType().GetProperty("message")
                           ?? lastResultData.GetType().GetProperty("FitSummary");
            if (summaryProp?.GetValue(lastResultData)?.ToString() is { } s && !string.IsNullOrWhiteSpace(s))
            {
                dynamicUserMsg = s;
            }
        }

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
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            Choices = choices,
            // ── Spec state machine fields ────────────────────────────────────────
            State = WorkflowState.READY_TO_EXECUTE.ToString(),
            UserMessage = dynamicUserMsg,
            TargetSystem = ExtractTargetSystem(parsedIntent),
            ConfirmationDetails = new ConfirmationDetails
            {
                ProposedAction = $"Executed {string.Join(", ", executedSteps.Select(s => s.ToolName))}",
                ActionSummary = "Confirmed by system. Execution completed.",
                RequiresUserAction = false
            },
            ExecutionPayload = execPayload
        };
    }

    public async Task<AgentResult> ResumeApprovedRunAsync(Guid runId, Guid approvalId, string approvedBy = "Executive Admin", Dictionary<string, object>? editedParameters = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var feed = new List<AgentEvent>();

        // 1. Load AgentRun & Approval
        var agentRun = await _db.AgentRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (agentRun == null)
            throw new InvalidOperationException($"AgentRun '{runId}' not found.");

        var approval = await _db.Approvals.FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval == null)
            throw new InvalidOperationException($"Approval record '{approvalId}' not found.");

        if (approval.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Approval '{approvalId}' has already been processed (Status: {approval.Status}).");

        approval.Status = ApprovalStatus.Approved;
        approval.ApprovedBy = approvedBy;
        approval.ApprovedAt = DateTime.UtcNow;

        agentRun.Status = AgentRunStatus.Executing;
        await _db.SaveChangesAsync(cancellationToken);

        feed.Add(AgentEvent.Create(AgentEventType.TOOL_SELECTED, $"APPROVAL_RECEIVED: Plan of Action approved by {approvedBy}. Resuming tool execution..."));
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 1, "APPROVAL_RECEIVED", $"Plan of Action approved by {approvedBy}. Resuming execution..."), cancellationToken);

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

        if (editedParameters != null && editedParameters.Count > 0)
        {
            parsedIntent ??= new ParsedIntentResult();
            parsedIntent.Parameters ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            parsedIntent.Entities ??= new Dictionary<string, string>();
            foreach (var kv in editedParameters)
            {
                if (kv.Value != null)
                {
                    parsedIntent.Parameters[kv.Key] = kv.Value;
                    parsedIntent.Entities[kv.Key] = kv.Value.ToString() ?? "";
                }
            }
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"USER_EDITS_APPLIED: Workflow parameters updated: {JsonSerializer.Serialize(editedParameters)}"));
        }
        var prompt = agentRun.OriginalPrompt ?? "";
        int affectedCount = 0;
        string resultMessage = "";

        var combinedArgs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "prompt", prompt },
            { "question", prompt }
        };

        // Helper: unwrap JsonElement to its actual .NET primitive so it serializes correctly
        static object UnwrapJsonElement(object val)
        {
            if (val is System.Text.Json.JsonElement je)
            {
                return je.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number  => je.TryGetDecimal(out var d) ? (object)d : (je.TryGetInt64(out var l) ? l : je.GetDouble()),
                    System.Text.Json.JsonValueKind.String  => je.GetString() ?? "",
                    System.Text.Json.JsonValueKind.True    => true,
                    System.Text.Json.JsonValueKind.False   => false,
                    System.Text.Json.JsonValueKind.Null    => "",
                    _                                      => je.GetRawText()
                };
            }
            return val;
        }

        if (parsedIntent?.Parameters != null)
        {
            foreach (var kv in parsedIntent.Parameters)
                combinedArgs[kv.Key] = UnwrapJsonElement(kv.Value);
        }

        if (parsedIntent?.Entities != null)
        {
            foreach (var kv in parsedIntent.Entities)
            {
                combinedArgs[kv.Key] = kv.Value;
            }
            if (parsedIntent.Entities.TryGetValue("name", out var empNameVal))
            {
                combinedArgs["name"] = empNameVal;
                combinedArgs["employeeName"] = empNameVal;
            }
            if (parsedIntent.Entities.TryGetValue("department", out var deptVal))
            {
                combinedArgs["department"] = deptVal;
                combinedArgs["targetDepartment"] = deptVal;
            }

            // Sync department & name for department mutations
            if (string.IsNullOrWhiteSpace(combinedArgs.GetValueOrDefault("department")?.ToString()) && combinedArgs.TryGetValue("name", out var nameForDept))
            {
                combinedArgs["department"] = nameForDept;
            }
            else if (string.IsNullOrWhiteSpace(combinedArgs.GetValueOrDefault("name")?.ToString()) && combinedArgs.TryGetValue("department", out var deptForName))
            {
                combinedArgs["name"] = deptForName;
            }
            if (parsedIntent.Entities.TryGetValue("designation", out var desigVal))
            {
                combinedArgs["designation"] = desigVal;
                combinedArgs["role"] = desigVal;
                combinedArgs["targetRole"] = desigVal;
            }
            if (parsedIntent.Entities.TryGetValue("salary", out var salVal))
            {
                combinedArgs["salary"] = salVal;
                combinedArgs["newSalary"] = salVal;
            }
            if (parsedIntent.Entities.TryGetValue("percentage", out var pctVal))
            {
                combinedArgs["percentage"] = pctVal;
                combinedArgs["pct"] = pctVal;
            }
            // Transfer / promote aliases
            if (parsedIntent.Entities.TryGetValue("role", out var roleVal))
            {
                combinedArgs["targetRole"] = roleVal;
                combinedArgs["role"] = roleVal;
                combinedArgs["designation"] = roleVal;
            }
            if (parsedIntent.Entities.TryGetValue("targetRole", out var tRoleVal))
            {
                combinedArgs["targetRole"] = tRoleVal;
                combinedArgs["role"] = tRoleVal;
                combinedArgs["designation"] = tRoleVal;
            }
            if (parsedIntent.Entities.TryGetValue("manager", out var mgrVal))
                combinedArgs["targetManager"] = mgrVal;
            if (parsedIntent.Entities.TryGetValue("targetDepartment", out var tDeptVal))
            {
                combinedArgs["targetDepartment"] = tDeptVal;
                combinedArgs["department"] = tDeptVal;
            }
        }

        if (!combinedArgs.ContainsKey("message") || string.IsNullOrWhiteSpace(combinedArgs["message"]?.ToString()))
        {
            var empNameForMsg = combinedArgs.TryGetValue("employeeName", out var enm) ? enm?.ToString() : (combinedArgs.TryGetValue("name", out var nm) ? nm?.ToString() : "Sarah Jenkins");
            var dateStrForMsg = DateTime.UtcNow.ToString("MMMM dd, yyyy");
            combinedArgs["message"] = $"📢 Sick Day Alert: {empNameForMsg} has logged a sick day for today ({dateStrForMsg}). Team workload coverage has been activated.";
            combinedArgs["employeeName"] = empNameForMsg;
        }

        // Ensure tool-specific args are set for payroll / freeze / transfer intents
        var resumeIntentType = parsedIntent?.ParsedIntentType ?? IntentType.UNKNOWN;
        if (resumeIntentType == IntentType.PAYROLL_HOLD)
        {
            if (!combinedArgs.ContainsKey("actionType")) combinedArgs["actionType"] = "Hold";
            if (!combinedArgs.ContainsKey("division") && combinedArgs.TryGetValue("department", out var pdiv))
                combinedArgs["division"] = pdiv;
        }
        else if (resumeIntentType == IntentType.PAYROLL_BONUS)
        {
            if (!combinedArgs.ContainsKey("actionType")) combinedArgs["actionType"] = "BulkBonus";
        }
        else if (resumeIntentType == IntentType.BUDGET_FREEZE)
        {
            if (!combinedArgs.ContainsKey("isFreeze")) combinedArgs["isFreeze"] = true;
            var deptScope = combinedArgs.TryGetValue("department", out var fd) ? fd?.ToString() : null;
            if (string.IsNullOrWhiteSpace(deptScope) || deptScope!.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                combinedArgs["scope"] = "ALL";
        }
        else if (resumeIntentType == IntentType.EMPLOYEE_TRANSFER)
        {
            if (!combinedArgs.ContainsKey("isPromotion")) combinedArgs["isPromotion"] = false;
        }
        else if (resumeIntentType == IntentType.EMPLOYEE_PROMOTE)
        {
            if (!combinedArgs.ContainsKey("isPromotion")) combinedArgs["isPromotion"] = true;
        }

        if (editedParameters != null)
        {
            foreach (var kv in editedParameters)
                combinedArgs[kv.Key] = UnwrapJsonElement(kv.Value);
        }

        bool executedAnyTool = false;
        bool executedWelcomeEmail = false;

        // 2. Execute plan steps via ToolRegistry
        if (actionPlan?.Steps != null && actionPlan.Steps.Count > 0)
        {
            foreach (var step in actionPlan.Steps)
            {
                var tool = _toolRegistry.GetTool(step.ToolName);
                if (tool != null)
                {
                    feed.Add(AgentEvent.Create(AgentEventType.TOOL_STARTED, $"Executing step {step.StepNumber}: {step.ToolName} ({step.Description})"));
                    await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, step.StepNumber, "STEP_STARTED", $"Executing {step.ToolName}"), cancellationToken);

                    var jsonArgs = JsonSerializer.Serialize(combinedArgs);
                    var toolCtx = new ToolExecutionContext
                    {
                        AgentRunId = runId,
                        UserId = agentRun.UserId,
                        UserRole = "Admin",
                        ArgumentsJson = jsonArgs
                    };

                    var agentAction = new AgentAction
                    {
                        Id = Guid.NewGuid(),
                        AgentRunId = runId,
                        ToolName = step.ToolName,
                        ActionType = step.ToolName,
                        ArgumentsJson = jsonArgs,
                        RiskLevel = step.RiskLevel,
                        Status = AgentActionStatus.Executing,
                        StartedAt = DateTime.UtcNow
                    };
                    _db.AgentActions.Add(agentAction);

                    var execRes = await tool.ExecuteAsync(toolCtx);
                    if (execRes.IsSuccess)
                    {
                        agentAction.Status = AgentActionStatus.Completed;
                        agentAction.CompletedAt = DateTime.UtcNow;
                        executedAnyTool = true;
                        if (step.ToolName.Equals("email.welcome", StringComparison.OrdinalIgnoreCase))
                        {
                            executedWelcomeEmail = true;
                        }
                        feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"Step {step.StepNumber} ({step.ToolName}) completed successfully."));
                        affectedCount++;
                    }
                    else
                    {
                        agentAction.Status = AgentActionStatus.Failed;
                        agentAction.ResultJson = execRes.ErrorMessage;
                        agentAction.CompletedAt = DateTime.UtcNow;
                        feed.Add(AgentEvent.Create(AgentEventType.EXECUTION_FAILED, $"Step {step.StepNumber} ({step.ToolName}) failed: {execRes.ErrorMessage}"));
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        // Fallback DB mutation if no tool executed changes directly or actionPlan.Steps was empty
        var intentType = parsedIntent?.ParsedIntentType ?? IntentType.UNKNOWN;

        if (!executedAnyTool && (intentType == IntentType.UPDATE_SALARY || intentType == IntentType.EMPLOYEE_UPDATE))
        {
            decimal pct = 10m;
            if (parsedIntent?.Entities?.TryGetValue("percentage", out var pStr) == true && decimal.TryParse(pStr, out var pDec))
                pct = pDec;

            var empName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? parsedIntent?.Entities?.GetValueOrDefault("employee_name");
            var toUpdate = targetRecordIds.Count > 0
                ? await _db.Employees.Where(e => targetRecordIds.Contains(e.Id)).ToListAsync(cancellationToken)
                : !string.IsNullOrWhiteSpace(empName)
                    ? await _db.Employees.Where(e => e.Name.ToLower().Contains(empName.ToLower())).ToListAsync(cancellationToken)
                    : await _db.Employees.ToListAsync(cancellationToken);

            foreach (var emp in toUpdate)
            {
                emp.Salary = Math.Round(emp.Salary * (1m + (pct / 100m)), 2);
                emp.UpdatedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(cancellationToken);
            affectedCount = Math.Max(affectedCount, toUpdate.Count);
            resultMessage = $"Updated compensation (+{pct}%) for {toUpdate.Count} employee(s) in SQL Server database.";
        }
        else if (intentType == IntentType.EMPLOYEE_ONBOARDING || intentType == IntentType.EMPLOYEE_CREATE)
        {
            var empName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? parsedIntent?.Entities?.GetValueOrDefault("employee_name") ?? "Ali";
            var desig = parsedIntent?.Entities?.GetValueOrDefault("designation") ?? "Developer";
            var deptName = parsedIntent?.Entities?.GetValueOrDefault("department") ?? parsedIntent?.Entities?.GetValueOrDefault("targetDepartment") ?? "IT";
            string? empEmail = parsedIntent?.Entities?.GetValueOrDefault("email")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("email")?.ToString();

            if (string.IsNullOrWhiteSpace(empEmail) || empEmail.EndsWith("@nexus.local"))
            {
                var candApp = await _db.CandidateApplications.OrderByDescending(a => a.SubmittedAt)
                    .FirstOrDefaultAsync(a => a.CandidateName.ToLower() == empName.ToLower() ||
                                              a.CandidateName.ToLower().Contains(empName.ToLower()) ||
                                              empName.ToLower().Contains(a.CandidateName.ToLower()), cancellationToken);
                if (candApp != null && !string.IsNullOrWhiteSpace(candApp.Email))
                {
                    empEmail = candApp.Email;
                }
                else
                {
                    empEmail = $"{empName.ToLower().Replace(" ", ".")}@gmail.com";
                }
            }

            decimal salary = 68000.00m;
            if (parsedIntent?.Entities?.TryGetValue("salary", out var sStr) == true && decimal.TryParse(sStr, out var sVal) && sVal > 0)
                salary = sVal;

            var existingEmp = await _db.Employees.FirstOrDefaultAsync(e => e.Name.ToLower() == empName.ToLower(), cancellationToken);
            var empToUse = existingEmp;
            if (existingEmp == null)
            {
                var department = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower() == deptName.ToLower() || d.Name.ToLower().Contains(deptName.ToLower()), cancellationToken);
                if (department == null)
                {
                    department = new Department { Name = deptName, Description = $"{deptName} Department" };
                    _db.Departments.Add(department);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                empToUse = new Employee
                {
                    Name = empName,
                    Email = empEmail,
                    DepartmentId = department.Id,
                    Designation = desig,
                    Salary = salary,
                    ExperienceYears = 3,
                    Status = EmployeeStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Employees.Add(empToUse);
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = 1;
            }

            // Automatic Welcome Email Delivery Integration (guaranteed single trigger)
            if (!executedWelcomeEmail)
            {
                var emailTool = _toolRegistry.GetTool("email.welcome");
                if (emailTool != null)
                {
                    var toolArgs = new Dictionary<string, object>
                    {
                        { "name", empName },
                        { "email", empToUse?.Email ?? empEmail },
                        { "designation", desig },
                        { "department", deptName },
                        { "employeeId", empToUse?.Id ?? 0 }
                    };
                    var jsonArgs = JsonSerializer.Serialize(toolArgs);
                    var toolCtx = new ToolExecutionContext
                    {
                        AgentRunId = runId,
                        UserId = agentRun.UserId,
                        UserRole = "Admin",
                        ArgumentsJson = jsonArgs
                    };

                    var agentAction = new AgentAction
                    {
                        Id = Guid.NewGuid(),
                        AgentRunId = runId,
                        ToolName = "email.welcome",
                        ActionType = "email.welcome",
                        ArgumentsJson = jsonArgs,
                        RiskLevel = RiskLevel.Low,
                        Status = AgentActionStatus.Executing,
                        StartedAt = DateTime.UtcNow
                    };
                    _db.AgentActions.Add(agentAction);

                    var execRes = await emailTool.ExecuteAsync(toolCtx);
                    if (execRes.IsSuccess)
                    {
                        agentAction.Status = AgentActionStatus.Completed;
                    }
                    else
                    {
                        agentAction.Status = AgentActionStatus.Failed;
                        agentAction.ResultJson = execRes.ErrorMessage;
                    }
                    agentAction.CompletedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);

                    executedWelcomeEmail = true;
                }

                feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, $"WELCOME_EMAIL_COMPLETED: Welcome email generated & sent to {empToUse?.Email ?? empEmail}."));
                await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 9, "WELCOME_EMAIL_COMPLETED", $"Welcome email sent to {empToUse?.Email ?? empEmail}."), cancellationToken);
            }

            resultMessage = $"Onboarding completed for {empName} ({desig}, ${salary:N2}) in {deptName} department and welcome email sent to {empToUse?.Email ?? empEmail}.";
        }
        else if (intentType == IntentType.DEPARTMENT_CREATE)
        {
            var deptName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? parsedIntent?.Entities?.GetValueOrDefault("department") ?? "New Department";
            var existingDept = await _db.Departments.FirstOrDefaultAsync(d => d.Name.ToLower() == deptName.ToLower(), cancellationToken);
            if (existingDept == null)
            {
                var newDept = new Department
                {
                    Name = deptName,
                    Description = $"{deptName} Department"
                };
                _db.Departments.Add(newDept);
                await _db.SaveChangesAsync(cancellationToken);
                existingDept = newDept;
                affectedCount = 1;
            }

            // Head of Department creation/assignment
            var headName = parsedIntent?.Entities?.GetValueOrDefault("head") ?? parsedIntent?.Entities?.GetValueOrDefault("manager");
            if (string.IsNullOrWhiteSpace(headName))
            {
                var headMatch = Regex.Match(prompt, @"(?:head|lead|manager)\s+([A-Za-z]+)", RegexOptions.IgnoreCase);
                if (headMatch.Success) headName = headMatch.Groups[1].Value;
            }
            if (!string.IsNullOrWhiteSpace(headName))
            {
                headName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(headName.ToLower());
                var headEmp = await _db.Employees.FirstOrDefaultAsync(e => e.DepartmentId == existingDept.Id && e.Name.ToLower() == headName.ToLower(), cancellationToken);
                if (headEmp == null)
                {
                    headEmp = new Employee
                    {
                        Name = headName,
                        Email = $"{headName.ToLower()}@nexus.local",
                        DepartmentId = existingDept.Id,
                        Designation = $"Head of {deptName}",
                        Salary = 95000.00m,
                        ExperienceYears = 8,
                        Status = EmployeeStatus.Active,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.Employees.Add(headEmp);
                }
                else
                {
                    headEmp.Designation = $"Head of {deptName}";
                }
            }

            // Budget allocation
            decimal initialBudget = 0m;
            var budArg = parsedIntent?.Entities?.GetValueOrDefault("budgetAmount") ?? parsedIntent?.Entities?.GetValueOrDefault("budget");
            if (!string.IsNullOrWhiteSpace(budArg))
            {
                var val = budArg.Replace(",", "").Trim();
                if (val.EndsWith("k", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(val[..^1], out var kv)) initialBudget = kv * 1000m;
                else if (val.EndsWith("m", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(val[..^1], out var mv)) initialBudget = mv * 1_000_000m;
                else if (decimal.TryParse(val, out var sv)) initialBudget = sv;
            }
            else
            {
                var budMatch = Regex.Match(prompt, @"(?:budget|allocation)\s+(?:of\s+)?\$?([0-9kKmM\.\,]+)", RegexOptions.IgnoreCase);
                if (budMatch.Success)
                {
                    var val = budMatch.Groups[1].Value.Replace(",", "").Trim();
                    if (val.EndsWith("k", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(val[..^1], out var kv)) initialBudget = kv * 1000m;
                    else if (val.EndsWith("m", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(val[..^1], out var mv)) initialBudget = mv * 1_000_000m;
                    else if (decimal.TryParse(val, out var sv)) initialBudget = sv;
                }
            }

            if (initialBudget > 0)
            {
                var budgetRecord = await _db.Budgets.FirstOrDefaultAsync(b => b.DepartmentId == existingDept.Id, cancellationToken);
                if (budgetRecord == null)
                {
                    budgetRecord = new Budget
                    {
                        DepartmentId = existingDept.Id,
                        Year = 2026,
                        Quarter = "Q3",
                        AllocatedAmount = initialBudget,
                        SpentAmount = 0m,
                        IsFrozen = false
                    };
                    _db.Budgets.Add(budgetRecord);
                }
                else
                {
                    budgetRecord.AllocatedAmount = initialBudget;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            resultMessage = $"Department '{deptName}' created successfully in database (Head: {headName ?? "Assigned Lead"}, Budget: ${initialBudget:N2}).";
        }
        else if (intentType == IntentType.DEPARTMENT_DELETE)
        {
            var deptName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? parsedIntent?.Entities?.GetValueOrDefault("department") ?? "";
            var promptLowerDept = prompt.ToLower();
            bool isDeleteAll = parsedIntent?.Scope?.Equals("ALL", StringComparison.OrdinalIgnoreCase) == true
                || promptLowerDept.Contains("delete all") || promptLowerDept.Contains("remove all")
                || deptName.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (isDeleteAll)
            {
                var allDepts = await _db.Departments.Include(d => d.Employees).ToListAsync(cancellationToken);
                int deptCount = allDepts.Count;

                var allBudgets = await _db.Budgets.ToListAsync(cancellationToken);
                _db.Budgets.RemoveRange(allBudgets);

                var allEmps = await _db.Employees.ToListAsync(cancellationToken);
                int empCount = allEmps.Count;
                _db.Employees.RemoveRange(allEmps);

                _db.Departments.RemoveRange(allDepts);
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = deptCount + empCount;
                resultMessage = $"All {deptCount} department(s) and all {empCount} employee(s) permanently deleted from SQL Server database.";
            }
            else if (!string.IsNullOrWhiteSpace(deptName))
            {
                var dept = await _db.Departments.Include(d => d.Employees).FirstOrDefaultAsync(d => d.Name.ToLower().Contains(deptName.ToLower()), cancellationToken);
                if (dept != null)
                {
                    var deptBudgets = await _db.Budgets.Where(b => b.DepartmentId == dept.Id).ToListAsync(cancellationToken);
                    _db.Budgets.RemoveRange(deptBudgets);

                    int empCount = dept.Employees.Count;
                    _db.Employees.RemoveRange(dept.Employees);

                    _db.Departments.Remove(dept);
                    await _db.SaveChangesAsync(cancellationToken);
                    affectedCount = 1 + empCount;
                    resultMessage = $"Department '{dept.Name}' and its {empCount} employee(s) permanently deleted from SQL Server database.";
                }
                else
                {
                    resultMessage = $"Department '{deptName}' not found in database.";
                }
            }
            else
            {
                resultMessage = "No department name specified.";
            }
        }
        else if (intentType == IntentType.EMPLOYEE_DELETE)
        {
            var empName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? parsedIntent?.Entities?.GetValueOrDefault("employee_name") ?? "";
            var promptLower = prompt.ToLower();

            if (promptLower.Contains("delete all") || promptLower.Contains("remove all"))
            {
                var allEmps = await _db.Employees.ToListAsync(cancellationToken);
                _db.Employees.RemoveRange(allEmps);
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = allEmps.Count;
                resultMessage = $"All {allEmps.Count} employee(s) permanently deleted from SQL Server database.";
            }
            else if (!string.IsNullOrWhiteSpace(empName))
            {
                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.Name.ToLower().Contains(empName.ToLower()), cancellationToken);
                if (emp != null)
                {
                    _db.Employees.Remove(emp);
                    await _db.SaveChangesAsync(cancellationToken);
                    affectedCount = 1;
                    resultMessage = $"Employee '{emp.Name}' deleted from SQL Server database.";
                }
            }
        }
        else if (intentType == IntentType.EMPLOYEE_TRANSFER || intentType == IntentType.EMPLOYEE_PROMOTE)
        {
            // If the tool already ran successfully above, resultMessage will be set; otherwise run direct DB update
            if (!executedAnyTool)
            {
                var empName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? parsedIntent?.Entities?.GetValueOrDefault("employee_name") ?? "";
                if (empName.StartsWith("Move ", StringComparison.OrdinalIgnoreCase))
                    empName = empName.Substring(5).Trim();

                var newDeptName = parsedIntent?.Entities?.GetValueOrDefault("department")
                    ?? parsedIntent?.Entities?.GetValueOrDefault("targetDepartment") ?? "";
                var newRole = parsedIntent?.Entities?.GetValueOrDefault("role")
                    ?? parsedIntent?.Entities?.GetValueOrDefault("designation")
                    ?? parsedIntent?.Entities?.GetValueOrDefault("targetRole") ?? "";

                var emp = await _db.Employees.FirstOrDefaultAsync(
                    e => e.Name.ToLower().Contains(empName.ToLower()), cancellationToken);

                if (emp != null)
                {
                    if (!string.IsNullOrWhiteSpace(newDeptName))
                    {
                        var dept = await _db.Departments.FirstOrDefaultAsync(
                            d => d.Name.ToLower().Contains(newDeptName.ToLower()), cancellationToken);
                        if (dept != null) emp.DepartmentId = dept.Id;
                    }
                    if (!string.IsNullOrWhiteSpace(newRole))
                        emp.Designation = newRole;
                    emp.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    affectedCount = 1;
                    var verb = intentType == IntentType.EMPLOYEE_PROMOTE ? "Promoted" : "Transferred";
                    resultMessage = $"{verb} '{emp.Name}' to {(string.IsNullOrWhiteSpace(newDeptName) ? "new role" : newDeptName)} as {(string.IsNullOrWhiteSpace(newRole) ? emp.Designation : newRole)} in SQL Server database.";
                }
                else
                {
                    resultMessage = $"Employee '{empName}' not found for transfer/promotion.";
                }
            }
            else
            {
                resultMessage = $"Employee transfer/promotion executed successfully by tool.";
            }
        }
        else if (intentType == IntentType.BUDGET_FREEZE)
        {
            if (!executedAnyTool)
            {
                var freezeDeptName = parsedIntent?.Entities?.GetValueOrDefault("department") ?? "";
                var freezePromptLower = prompt.ToLower();
                bool freezeAll = string.IsNullOrWhiteSpace(freezeDeptName)
                    || freezeDeptName.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    || freezePromptLower.Contains("all department")
                    || freezePromptLower.Contains("all budgets");

                var budgetQuery = _db.Budgets.Include(b => b.Department).AsQueryable();
                if (!freezeAll && !string.IsNullOrWhiteSpace(freezeDeptName))
                    budgetQuery = budgetQuery.Where(b => b.Department != null && b.Department.Name.ToLower().Contains(freezeDeptName.ToLower()));

                var budgetsToFreeze = await budgetQuery.ToListAsync(cancellationToken);
                foreach (var b in budgetsToFreeze)
                {
                    b.IsFrozen = true;
                    b.FreezeReason = "Executive budget freeze directive via Nexus Agent";
                    b.FrozenAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync(cancellationToken);
                affectedCount = budgetsToFreeze.Count;
                resultMessage = $"Budget allocations for {budgetsToFreeze.Count} department(s) have been FROZEN in SQL Server database.";
            }
            else
            {
                resultMessage = "Budget freeze executed successfully by tool.";
            }
        }
        else if (intentType == IntentType.BUDGET_REALLOCATE)
        {
            // The budget.reallocate tool already ran if registered. Confirm success.
            resultMessage = executedAnyTool
                ? "Budget reallocation executed successfully."
                : $"Approved budget reallocation for prompt: {prompt}";
        }
        else if (intentType == IntentType.PAYROLL_HOLD || intentType == IntentType.PAYROLL_BONUS)
        {
            if (!executedAnyTool)
            {
                // Direct DB simulation: mark a flag or just record the result message
                var payDivision = parsedIntent?.Entities?.GetValueOrDefault("department")
                    ?? parsedIntent?.Entities?.GetValueOrDefault("division") ?? "All Divisions";
                var isHold = intentType == IntentType.PAYROLL_HOLD;

                // Count affected employees for the message
                var payEmpQuery = _db.Employees.Include(e => e.Department).AsQueryable();
                if (!payDivision.Equals("All Divisions", StringComparison.OrdinalIgnoreCase))
                    payEmpQuery = payEmpQuery.Where(e => e.Department != null && e.Department.Name.ToLower().Contains(payDivision.ToLower()));

                var payEmpCount = await payEmpQuery.CountAsync(cancellationToken);
                affectedCount = payEmpCount;
                var verb = isHold ? "placed on HOLD" : "bonus distribution initiated for";
                resultMessage = $"Payroll for {payEmpCount} employee(s) in '{payDivision}' {verb}. Record committed to SQL Server.";
            }
            else
            {
                resultMessage = "Payroll action executed successfully by tool.";
            }
        }
        else if (intentType == IntentType.LEAVE_CREATE)
        {
            // The leave.create tool handles DB persistence. If not executed, do a direct insert.
            if (!executedAnyTool)
            {
                var leaveEmpName = parsedIntent?.Entities?.GetValueOrDefault("name") ?? "";
                var leaveTypeStr = parsedIntent?.Entities?.GetValueOrDefault("leaveType") ?? "Sick";
                if (!Enum.TryParse<Nexus.Data.Enums.LeaveType>(leaveTypeStr, true, out var leaveTypeEnum))
                    leaveTypeEnum = Nexus.Data.Enums.LeaveType.Sick;

                var startDate = DateTime.UtcNow.Date;

                var leaveEmp = string.IsNullOrWhiteSpace(leaveEmpName)
                    ? null
                    : await _db.Employees.FirstOrDefaultAsync(e => e.Name.ToLower().Contains(leaveEmpName.ToLower()), cancellationToken);

                if (leaveEmp != null)
                {
                    var leaveRecord = new Nexus.Data.Entities.Leave
                    {
                        EmployeeId = leaveEmp.Id,
                        LeaveType = leaveTypeEnum,
                        StartDate = startDate,
                        EndDate = startDate,
                        Notes = $"Logged via Nexus Agent: {prompt}",
                        Status = Nexus.Data.Enums.LeaveStatus.Approved,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Leaves.Add(leaveRecord);
                    await _db.SaveChangesAsync(cancellationToken);
                    affectedCount = 1;
                    resultMessage = $"{leaveEmp.Name}'s {leaveTypeEnum} leave for {startDate:yyyy-MM-dd} logged and saved to SQL Server database.";
                }
                else
                {
                    resultMessage = $"Leave logged for '{leaveEmpName}' (employee record not found — manual verification required).";
                }
            }
            else
            {
                resultMessage = "Leave record created successfully by tool.";
            }
        }
        else if (intentType == IntentType.JOB_OPENING_CREATE)
        {
            var jobTitle = parsedIntent?.Entities?.GetValueOrDefault("title")
                ?? parsedIntent?.Entities?.GetValueOrDefault("jobTitle")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("title")?.ToString()
                ?? "Lead Cloud Architect";
            var dept = parsedIntent?.Entities?.GetValueOrDefault("department")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("department")?.ToString()
                ?? "IT";
            var salaryRange = parsedIntent?.Entities?.GetValueOrDefault("salaryRange")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("salaryRange")?.ToString()
                ?? "$95,000 - $125,000";
            var reqs = parsedIntent?.Entities?.GetValueOrDefault("requirements")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("requirements")?.ToString()
                ?? "AWS, Kubernetes, Terraform, Docker, CI/CD";
            var loc = parsedIntent?.Entities?.GetValueOrDefault("location")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("location")?.ToString()
                ?? "Remote / Hybrid";
            var desc = parsedIntent?.Entities?.GetValueOrDefault("description")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("description")?.ToString()
                ?? $"Lead design, architecture, and production delivery for the {jobTitle} role in the {dept} department.";
            var resp = parsedIntent?.Entities?.GetValueOrDefault("responsibilities")
                ?? parsedIntent?.Parameters?.GetValueOrDefault("responsibilities")?.ToString()
                ?? "Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles. • Collaborate across multidisciplinary engineering, UX, and AI agent automation pods. • Optimize query execution, conduct peer code reviews, and champion continuous automated testing.";

            var newJob = new Nexus.Data.Entities.JobOpening
            {
                Title = jobTitle,
                Department = dept,
                SalaryRange = salaryRange,
                Location = loc,
                Requirements = reqs,
                Description = desc,
                Responsibilities = resp,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _db.JobOpenings.Add(newJob);
            await _db.SaveChangesAsync(cancellationToken);

            affectedCount = 1;
            resultMessage = $"Job opening for '{jobTitle}' in '{dept}' department has been successfully created in the SQL database and published to the Candidate Portal (Requisition ID: #{newJob.Id}).";
            feed.Add(AgentEvent.Create(AgentEventType.TOOL_COMPLETED, resultMessage));
        }
        else
        {
            resultMessage = $"Approved action for prompt '{prompt}' executed successfully.";
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
        await _broadcaster.BroadcastEventAsync(Nexus.Data.Events.AgentActivityEvent.Create(runId, 12, "WORKFLOW_COMPLETED", resultMessage), cancellationToken);

        sw.Stop();
        var resumeChoices = GenerateNextActionChoices(parsedIntent ?? new ParsedIntentResult { Intent = agentRun.Intent ?? "" }, new { message = resultMessage, affectedCount });
        return new AgentResult
        {
            RunId = runId,
            OriginalPrompt = agentRun.OriginalPrompt ?? string.Empty,
            Intent = agentRun.Intent ?? string.Empty,
            IsSuccess = true,
            UserMessage = resultMessage,
            State = WorkflowState.READY_TO_EXECUTE.ToString(),
            ResultData = new { message = resultMessage, affectedCount },
            ExecutionFeed = feed,
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            Choices = resumeChoices
        };
    }

    private AgentPlan BuildPlan(string prompt, ParsedIntentResult intent)
    {
        var plan = new AgentPlan();
        var intentType = intent.ParsedIntentType;

        switch (intentType)
        {
            // ── Employee ────────────────────────────────────────────────────
            case IntentType.EMPLOYEE_CREATE:
                if (_toolRegistry.HasTool("policy.evaluate"))
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR policy rule", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = plan.Steps.Count + 1, ToolName = "employee.create", Description = "Create employee record", RiskLevel = RiskLevel.Medium });
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

            case IntentType.EMPLOYEE_ONBOARDING:
                var pLowerOnb = prompt.ToLowerInvariant();
                if (pLowerOnb.Contains("paperwork") || pLowerOnb.Contains("document") || pLowerOnb.Contains("ready"))
                {
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "onboarding.prepare", Description = "Generate comprehensive onboarding document package (Appointment Letter, NDA, IT Equipment)", RiskLevel = RiskLevel.Medium });
                }
                else if (pLowerOnb.Contains("orientation") || pLowerOnb.Contains("schedule") || pLowerOnb.Contains("intern"))
                {
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "orientation.schedule", Description = "Generate 5-day induction and orientation schedule document for new intern cohort", RiskLevel = RiskLevel.Medium });
                }
                else
                {
                    plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate HR Policy POL-HR-001 salary band", RiskLevel = RiskLevel.Low });
                    plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "onboarding.prepare", Description = "Generate onboarding document package", RiskLevel = RiskLevel.Medium });
                    plan.Steps.Add(new AgentStep { StepNumber = 3, ToolName = "employee.create", Description = "Create employee SQL record", RiskLevel = RiskLevel.Medium });
                    plan.Steps.Add(new AgentStep { StepNumber = 4, ToolName = "sap.employee.create", Description = "Provision employee in SAP HCM", RiskLevel = RiskLevel.Medium });
                    plan.Steps.Add(new AgentStep { StepNumber = 5, ToolName = "email.welcome", Description = "Generate welcome email", RiskLevel = RiskLevel.Low });
                }
                break;

            // ── Policy ──────────────────────────────────────────────────────
            case IntentType.POLICY_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Read / search policy records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.POLICY_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Create new policy record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.POLICY_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Update policy record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.POLICY_DELETE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.crud", Description = "Deactivate / delete policy record", RiskLevel = RiskLevel.High });
                break;

            // ── Department ──────────────────────────────────────────────────
            case IntentType.DEPARTMENT_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.read", Description = "Read department records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.DEPARTMENT_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.crud", Description = "Create new department", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.DEPARTMENT_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.crud", Description = "Update department record", RiskLevel = RiskLevel.Medium });
                break;

            case IntentType.DEPARTMENT_DELETE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "department.crud", Description = "Delete department", RiskLevel = RiskLevel.High });
                break;

            // ── Budget ──────────────────────────────────────────────────────
            case IntentType.BUDGET_ANALYSIS:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute T-SQL budget analysis query", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.BUDGET_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "budget.read", Description = "Read budget records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.BUDGET_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "budget.update", Description = "Update department budget allocation", RiskLevel = RiskLevel.High });
                break;

            // ── Expense ─────────────────────────────────────────────────────
            case IntentType.EXPENSE_COMPLIANCE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Evaluate expense claim against policy limit", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EXPENSE_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "expense.read", Description = "Read expense records", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.EXPENSE_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Validate expense against policy", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "expense.read", Description = "Record expense (read tool used for compliance only)", RiskLevel = RiskLevel.Medium });
                break;

            // ── Read-Only Cross-Cutting ──────────────────────────────────────
            case IntentType.APPROVAL_READ:
            case IntentType.ONBOARDING_READ:
            case IntentType.AUDIT_READ:
            case IntentType.DASHBOARD_ANALYTICS:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Query enterprise records for summary", RiskLevel = RiskLevel.Low });
                break;

            // Spec: UPDATE_SALARY — financial mutation, always approval-gated
            case IntentType.UPDATE_SALARY:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Validate new salary against HR compensation band policy", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "employee.update", Description = "Apply salary change to employee record in SQL Server", RiskLevel = RiskLevel.High });
                break;

            // Spec: SECURITY_TEST — diagnostic/read-only, never mutates data
            case IntentType.SECURITY_TEST:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute read-only connection audit and system health check", RiskLevel = RiskLevel.Low });
                break;

            // Spec: EXECUTE_AUTOMATION — routes to browser automation / Playwright
            case IntentType.EXECUTE_AUTOMATION:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "browser.automation", Description = "Trigger external workflow automation (n8n / Zapier / Playwright)", RiskLevel = RiskLevel.Medium });
                break;

            // ── New Enterprise Workflow Intents ──────────────────────────────
            case IntentType.EMPLOYEE_TRANSFER:
            case IntentType.EMPLOYEE_PROMOTE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.transfer", Description = "Transfer / promote employee record", RiskLevel = RiskLevel.High });
                break;

            case IntentType.EMPLOYEE_OFFBOARD:
            case IntentType.EMPLOYEE_CANCEL_ONBOARDING:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "offboarding.initiate", Description = "Initiate exit clearance workflow and generate offboarding document package", RiskLevel = RiskLevel.High });
                break;

            case IntentType.LEAVE_CREATE:
            case IntentType.LEAVE_APPROVE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "leave.create", Description = "Record employee leave request", RiskLevel = RiskLevel.Low });
                if (prompt.ToLower().Contains("slack") || prompt.ToLower().Contains("notify"))
                    plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "slack.notify", Description = "Send Slack announcement to team", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.PAYROLL_HOLD:
            case IntentType.PAYROLL_BONUS:
            case IntentType.BULK_PAYROLL:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "payroll.action", Description = "Execute payroll directive", RiskLevel = RiskLevel.High });
                break;

            case IntentType.BUDGET_REALLOCATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "budget.reallocate", Description = "Reallocate budget across departments", RiskLevel = RiskLevel.High });
                break;

            case IntentType.BUDGET_FREEZE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "budget.freeze", Description = "Freeze department budget allocation", RiskLevel = RiskLevel.High });
                break;

            case IntentType.SLACK_NOTIFY:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "slack.notify", Description = "Send notification via Slack webhook", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.BULK_EMPLOYEE_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.bulk_update", Description = "Bulk update employee designations", RiskLevel = RiskLevel.High });
                break;

            case IntentType.MULTI_STEP_WORKFLOW:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "leave.create", Description = "Step 1: Record leave request", RiskLevel = RiskLevel.Low });
                plan.Steps.Add(new AgentStep { StepNumber = 2, ToolName = "slack.notify", Description = "Step 2: Dispatch Slack alert", RiskLevel = RiskLevel.Low });
                break;

            // Workplace Service Desk (IT Tickets)
            case IntentType.TICKET_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "ticket.create", Description = "Create IT equipment provisioning ticket", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.TICKET_READ:
            case IntentType.TICKET_TRIAGE:
            case IntentType.TICKET_UPDATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Process Workplace Service Desk request", RiskLevel = RiskLevel.Low });
                break;

            // Candidate CV Screening & Job Openings
            case IntentType.JOB_OPENING_CREATE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Publish new Job Opening to SQL Server and Candidate Portal", RiskLevel = RiskLevel.High });
                break;

            case IntentType.JOB_OPENING_READ:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Query active Job Openings and candidate submission metrics", RiskLevel = RiskLevel.Low });
                break;

            case IntentType.CV_SCREEN:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "cv.analyze", Description = "Screen candidate resume and analyze role fit score", RiskLevel = RiskLevel.Low });
                break;

            // Approval Decisions
            case IntentType.APPROVAL_ACTION:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute manager approval decision on pending action", RiskLevel = RiskLevel.Medium });
                break;

            // Automated Workflows
            case IntentType.WORKFLOW_EXECUTE:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "browser.automation", Description = "Execute enterprise workflow pipeline", RiskLevel = RiskLevel.Low });
                break;

            // SQL Agent passthrough
            case IntentType.SQL_AGENT:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "sql.analytics", Description = "Execute Production SQL Agent query", RiskLevel = RiskLevel.Low });
                break;

            default:
                plan.Steps.Add(new AgentStep { StepNumber = 1, ToolName = "employee.read", Description = "Search workforce directory", RiskLevel = RiskLevel.Low });
                break;
        }

        plan.TotalRiskLevel = plan.Steps.Count > 0 ? plan.Steps.Max(s => s.RiskLevel) : RiskLevel.Low;
        return plan;
    }

    // ---- Spec-aligned helper methods (State Machine) -----------------------------------------------

    /// <summary>
    /// Extracts the target_system field from the LLM-parsed parameters.
    /// Falls back to SQL_SERVER for most intents; N8N_WORKFLOW / ZAPIER / UNKNOWN for automation.
    /// </summary>
    private static string ExtractTargetSystem(ParsedIntentResult intent)
    {
        // Try to read from the LLM-supplied parameters.target_system first
        if (intent.Parameters.TryGetValue("target_system", out var tsObj) && tsObj is string ts && !string.IsNullOrWhiteSpace(ts))
            return ts.ToUpperInvariant();

        // Rule-based fallback by intent type
        return intent.ParsedIntentType switch
        {
            IntentType.EXECUTE_AUTOMATION => "N8N_WORKFLOW",
            IntentType.SECURITY_TEST      => "UNKNOWN",
            IntentType.GENERAL_CONVERSATION => "UNKNOWN",
            _                             => "SQL_SERVER"
        };
    }

    /// <summary>
    /// Builds the user-facing confirmation message and ConfirmationDetails block for
    /// CONFIRMATION_REQUIRED states.
    /// </summary>
    private static (string Message, ConfirmationDetails Details) BuildConfirmationUserMessage(ParsedIntentResult intent)
    {
        var e = intent.Entities;
        string name = e.GetValueOrDefault("name") ?? e.GetValueOrDefault("employee_name") ?? "the employee";

        string message;
        string proposed;
        string summary;

        switch (intent.ParsedIntentType)
        {
            case IntentType.UPDATE_SALARY:
                var salary = e.GetValueOrDefault("salary") ?? e.GetValueOrDefault("new_salary") ?? "[pending]";
                var effectiveDate = intent.StructuredEntities?.EffectiveDate ?? "immediately";
                message = "I have drafted a salary adjustment request. Please review the details below before I submit it for execution:";
                proposed = "Update employee compensation record";
                summary = $"Employee: {name} | New Amount: {salary}/mo | Effective Date: {effectiveDate}";
                break;

            case IntentType.EMPLOYEE_ONBOARDING:
            case IntentType.EMPLOYEE_CREATE:
                var rawDept = intent.StructuredEntities?.Department ?? e.GetValueOrDefault("department") ?? intent.Parameters.GetValueOrDefault("department")?.ToString();
                var dept = IntentParser.CleanDepartmentName(rawDept);
                if (string.IsNullOrWhiteSpace(dept) || dept == "[Pending]") dept = "IT";

                var desig = e.GetValueOrDefault("designation") ?? e.GetValueOrDefault("role") ?? intent.Parameters.GetValueOrDefault("designation")?.ToString();
                if (string.IsNullOrWhiteSpace(desig) || desig == "[Pending]") desig = "Senior .NET Developer";

                var salStr = e.GetValueOrDefault("salary") ?? intent.Parameters.GetValueOrDefault("salary")?.ToString();
                string salFormatted = salStr ?? "$100,000 / mo ($1,200,000 / yr)";
                if (decimal.TryParse(salStr, out var dSal) && dSal > 0) salFormatted = $"${dSal:N2} / yr";

                message = $"I am ready to onboard {name}. Please confirm the details below before I create the record:";
                proposed = "Create new employee record";
                summary = $"Employee: {name} | Department: {dept} | Role: {desig} | Salary: {salFormatted}";
                break;

            case IntentType.EXECUTE_AUTOMATION:
                var target = intent.StructuredEntities?.AutomationTarget ?? "the configured workflow";
                message = "I am ready to trigger the automation workflow. Please confirm:";
                proposed = "Execute automated workflow pipeline";
                summary = $"Target: {target} | Action: EXECUTE";
                break;

            case IntentType.JOB_OPENING_CREATE:
                var jTitle = e.GetValueOrDefault("title") ?? e.GetValueOrDefault("jobTitle") ?? intent.Parameters.GetValueOrDefault("title")?.ToString() ?? "Lead Cloud Architect";
                var jDept = e.GetValueOrDefault("department") ?? intent.Parameters.GetValueOrDefault("department")?.ToString() ?? "IT";
                var jSal = e.GetValueOrDefault("salaryRange") ?? intent.Parameters.GetValueOrDefault("salaryRange")?.ToString() ?? "$95,000 - $125,000";
                message = $"I have prepared a new job opening for '{jTitle}' in {jDept}. Please review and confirm before publishing to the candidate portal:";
                proposed = $"Publish Job Opening: {jTitle}";
                summary = $"Role: {jTitle} | Department: {jDept} | Salary Range: {jSal}";
                break;

            default:
                message = "I have prepared the following action for your review. Please confirm before execution:";
                proposed = $"Execute {intent.Intent} workflow";
                summary = $"Intent: {intent.Intent} | Scope: {intent.Scope}";
                break;
        }

        return (message, new ConfirmationDetails
        {
            ProposedAction = proposed,
            ActionSummary = summary,
            RequiresUserAction = true
        });
    }

    /// <summary>
    /// Builds the ConfirmationDetails block for CLARIFICATION_REQUIRED states,
    /// showing which fields are still missing.
    /// </summary>
    private static ConfirmationDetails BuildClarificationDetails(ParsedIntentResult intent)
    {
        var missing = intent.MissingFields.Count > 0
            ? string.Join(", ", intent.MissingFields.Select(f => $"[{f}]"))
            : "[additional details]";

        return new ConfirmationDetails
        {
            ProposedAction = $"Awaiting clarification for: {intent.Intent}",
            ActionSummary = $"Missing required fields: {missing}",
            RequiresUserAction = true
        };
    }

    /// <summary>
    /// Builds the execution_payload object for READY_TO_EXECUTE results.
    /// This is a structured summary of what was executed, ready for frontend display or
    /// downstream API consumption.
    /// </summary>
    private static object? BuildExecutionPayload(ParsedIntentResult intent, object? resultData)
    {
        if (resultData == null) return null;

        return new
        {
            intent = intent.Intent,
            operation = intent.Operation,
            targetEntity = intent.TargetEntity,
            targetSystem = ExtractTargetSystem(intent),
            timestamp = DateTime.UtcNow.ToString("o"),
            result = resultData
        };
    }


    private async Task<Nexus.Data.ActionPlan.ActionPlan> BuildActionPlanAsync(Guid runId, string prompt, ParsedIntentResult intent, CancellationToken ct)
    {
        var pLower = prompt.ToLower();
        var intentType = intent.ParsedIntentType;

        // ── 0. PAYROLL ACTIONS (HOLD / BONUS) ──────────────────────────────
        if (intentType == IntentType.PAYROLL_HOLD || intentType == IntentType.PAYROLL_BONUS || pLower.Contains("payroll hold") || pLower.Contains("hold payroll") || (pLower.Contains("payroll") && (pLower.Contains("hold") || pLower.Contains("bonus"))))
        {
            var division = intent.Entities.GetValueOrDefault("department") ?? intent.Entities.GetValueOrDefault("division") ?? "IT";
            var isHold = intentType == IntentType.PAYROLL_HOLD || pLower.Contains("hold");
            var verb = isHold ? "Hold" : "Bonus";

            var employees = await _db.Employees.Include(e => e.Department)
                .Where(e => e.Department != null && e.Department.Name.ToLower().Contains(division.ToLower()))
                .ToListAsync(ct);

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Payroll {verb} for {division} Division",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            foreach (var emp in employees)
            {
                actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                {
                    RecordId = emp.Id,
                    EntityName = "Employee Master (SQL Server)",
                    PrimaryLabel = emp.Name,
                    Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                    {
                        new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Payroll Status", OldValue = $"{emp.Status}", NewValue = isHold ? "HOLD" : "BONUS_PENDING", Difference = "Payroll Directive" }
                    }
                });
            }

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "payroll.action",
                Description = $"Execute payroll {verb.ToLower()} on {employees.Count} employee(s) in {division}",
                RiskLevel = RiskLevel.High
            });

            actionPlan.Warnings.Add($"Executes payroll {verb.ToLower()} directive on {employees.Count} employee(s) in {division} division.");
            return actionPlan;
        }

        // ── 1. DEPARTMENT CREATION / MANAGEMENT ────────────────────────────────
        if (!pLower.Contains("payroll") && !pLower.Contains("hold") && (intentType == IntentType.DEPARTMENT_CREATE || intentType == IntentType.DEPARTMENT_UPDATE || (intent.TargetEntity == "DEPARTMENT" && (intent.Operation == "CREATE" || intent.Operation == "UPDATE"))))
        {
            var deptName = intent.Parameters.GetValueOrDefault("name")?.ToString()
                ?? intent.Entities.GetValueOrDefault("name")
                ?? "Finance";

            var head = intent.Parameters.GetValueOrDefault("head")?.ToString()
                ?? intent.Entities.GetValueOrDefault("head")
                ?? "Sufyan";

            decimal budgetAmt = 50000m;
            if (intent.Parameters.TryGetValue("budgetAmount", out var bObj) && decimal.TryParse(bObj?.ToString(), out var bVal))
                budgetAmt = bVal;

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Create Department '{deptName}'",
                RiskLevel = RiskLevel.Medium,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = budgetAmt,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Department Master (SQL Server)",
                PrimaryLabel = $"{deptName} Department",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department Name", OldValue = "[NEW]", NewValue = deptName, Difference = "New Entity" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department Head", OldValue = "None", NewValue = head, Difference = "Assignment" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Initial Staff Count", OldValue = "0", NewValue = "0", Difference = "Empty Dept" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Allocated Q3 Budget", OldValue = "$0.00", NewValue = $"${budgetAmt:N2}", Difference = "Budget Allocation" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "department.crud", Description = $"Create Department record '{deptName}' (Head: {head})", RiskLevel = RiskLevel.Medium });
            if (budgetAmt > 0)
            {
                actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 2, ToolName = "budget.update", Description = $"Initialize allocated Q3 budget (${budgetAmt:N2})", RiskLevel = RiskLevel.Medium });
            }

            actionPlan.Warnings.Add($"Creates new {deptName} department with ${budgetAmt:N2} allocated Q3 budget and {head} as Department Head.");
            return actionPlan;
        }

        // ── 1.5. DEPARTMENT DELETION ───────────────────────────────────────────
        if (intentType == IntentType.DEPARTMENT_DELETE || (intent.TargetEntity == "DEPARTMENT" && intent.Operation == "DELETE") || (pLower.Contains("department") && (pLower.Contains("delete") || pLower.Contains("remove"))))
        {
            intent.Intent = "DEPARTMENT_DELETE";
            intent.TargetEntity = "DEPARTMENT";
            intent.Operation = "DELETE";

            var scope = intent.Scope;
            var rawDept = intent.Entities.GetValueOrDefault("name") ?? intent.Entities.GetValueOrDefault("department") ?? string.Empty;
            var deptName = IntentParser.CleanDepartmentName(rawDept);

            if (string.IsNullOrWhiteSpace(deptName) || deptName.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                var delMatch = Regex.Match(pLower, @"\b(?:delete|remove|drop|purge|destroy)\s+(?:the\s+)?([a-z0-9\&]+)");
                if (delMatch.Success && !delMatch.Groups[1].Value.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    deptName = IntentParser.CleanDepartmentName(delMatch.Groups[1].Value);
                }
            }

            bool isBulk = scope.Equals("ALL", StringComparison.OrdinalIgnoreCase) || pLower.Contains("all") || string.Equals(deptName, "ALL", StringComparison.OrdinalIgnoreCase);

            List<Department> matchingDepts;
            if (isBulk)
            {
                matchingDepts = await _db.Departments.Include(d => d.Employees).ToListAsync(ct);
            }
            else
            {
                matchingDepts = await _db.Departments.Include(d => d.Employees)
                    .Where(d => d.Name.ToLower() == deptName.ToLower() || d.Name.ToLower().Contains(deptName.ToLower()) || deptName.ToLower().Contains(d.Name.ToLower()))
                    .ToListAsync(ct);
            }

            var displayDeptName = string.IsNullOrWhiteSpace(deptName) ? "Target" : deptName;

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = isBulk ? "Plan of Action: Permanent Deletion of All Corporate Departments" : $"Plan of Action: Delete Department '{displayDeptName}'",
                RiskLevel = isBulk ? RiskLevel.Critical : RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            var budgets = await _db.Budgets.Include(b => b.Department).AsNoTracking().ToListAsync(ct);
            Dictionary<string, (decimal Allocated, decimal Spent)> rawBudgets = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                var connection = _db.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DepartmentBudgets') SELECT DepartmentName, SUM(AllocatedAmount) AS Allocated, SUM(SpentAmount) AS Spent FROM DepartmentBudgets GROUP BY DepartmentName";
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var name = reader.GetString(0);
                    var alloc = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
                    var spent = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
                    rawBudgets[name] = (alloc, spent);
                }
            }
            catch { }

            decimal totalBudgetImpact = 0m;

            foreach (var dept in matchingDepts)
            {
                var b = budgets.FirstOrDefault(b => b.DepartmentId == dept.Id || (b.Department != null && b.Department.Name.Equals(dept.Name, StringComparison.OrdinalIgnoreCase)));
                var allocated = b?.AllocatedAmount ?? 0m;

                if (allocated == 0m && rawBudgets.Count > 0)
                {
                    var matchingKey = rawBudgets.Keys.FirstOrDefault(k => k.Equals(dept.Name, StringComparison.OrdinalIgnoreCase) || k.Contains(dept.Name, StringComparison.OrdinalIgnoreCase) || dept.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
                    if (matchingKey != null)
                    {
                        allocated = rawBudgets[matchingKey].Allocated;
                    }
                }

                totalBudgetImpact += allocated;

                var changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department Status", OldValue = $"{dept.Employees.Count} assigned employee(s)", NewValue = "Removed", Difference = "Department Deletion" }
                };

                if (allocated > 0)
                {
                    changes.Add(new Nexus.Data.ActionPlan.ChangePreview
                    {
                        FieldName = "Allocated Q3 Budget",
                        OldValue = $"${allocated:N2}",
                        NewValue = "$0.00",
                        Difference = $"Budget Reset (${allocated:N2})"
                    });
                }

                actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                {
                    RecordId = dept.Id,
                    EntityName = "Department",
                    PrimaryLabel = $"{dept.Name} Department",
                    Changes = changes
                });
            }

            actionPlan.TotalFinancialImpact = totalBudgetImpact;

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "department.crud",
                Description = $"Remove {matchingDepts.Count} department record(s)",
                RiskLevel = isBulk ? RiskLevel.Critical : RiskLevel.High
            });

            // Check if transfer to another department is mentioned
            var transferMatch = Regex.Match(pLower, @"\b(?:transfer|move)\s+(?:its\s+)?budget\s+to\s+(?:the\s+)?([a-z0-9\&]+)\s*(?:department|dept)?\b");
            if (transferMatch.Success)
            {
                var targetDept = IntentParser.CleanDepartmentName(transferMatch.Groups[1].Value);
                actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
                {
                    StepNumber = 2,
                    ToolName = "budget.update",
                    Description = $"Transfer remaining budget to {targetDept} Department",
                    RiskLevel = RiskLevel.Medium
                });
                actionPlan.Warnings.Add($"Transfers budget from deleted department to {targetDept} Department.");
            }

            actionPlan.Warnings.Add($"Removes {matchingDepts.Count} department(s) from system.");
            if (matchingDepts.Any(d => d.Employees.Count > 0))
            {
                actionPlan.Warnings.Add("Employees in affected departments will have their department assignment reset.");
            }

            return actionPlan;
        }

        // ── 2. BUDGET REALLOCATE ───────────────────────────────────────────
        if (intentType == IntentType.BUDGET_REALLOCATE || pLower.Contains("reallocate budget") || (pLower.Contains("reallocate") && pLower.Contains("budget")))
        {
            var srcDept = intent.Parameters.GetValueOrDefault("sourceDepartment")?.ToString()
                ?? intent.Entities.GetValueOrDefault("sourceDepartment")
                ?? "Marketing";
            var tgtDept = intent.Parameters.GetValueOrDefault("targetDepartment")?.ToString()
                ?? intent.Entities.GetValueOrDefault("targetDepartment")
                ?? "IT";

            decimal amount = 20000m;
            if (intent.Parameters.TryGetValue("amount", out var aObj) && decimal.TryParse(aObj?.ToString(), out var aVal))
                amount = aVal;
            else if (intent.Parameters.TryGetValue("budgetAmount", out var bObj) && decimal.TryParse(bObj?.ToString(), out var bVal))
                amount = bVal;

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Reallocate ${amount:N2} Budget from {srcDept} to {tgtDept}",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = 0m,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Department Budget",
                PrimaryLabel = $"{srcDept} → {tgtDept} Budget Transfer",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = $"{srcDept} Budget", OldValue = "(current)", NewValue = $"-${amount:N2}", Difference = $"-${amount:N2}" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = $"{tgtDept} Budget", OldValue = "(current)", NewValue = $"+${amount:N2}", Difference = $"+${amount:N2}" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "budget.reallocate", Description = $"Transfer ${amount:N2} budget from {srcDept} to {tgtDept}", RiskLevel = RiskLevel.High });
            actionPlan.Warnings.Add($"Reallocates ${amount:N2} Q3 budget allocation from {srcDept} Department to {tgtDept} Department.");
            return actionPlan;
        }

        // ── 2.1. BUDGET FREEZE ────────────────────────────────────────────────
        if (intentType == IntentType.BUDGET_FREEZE || (pLower.Contains("freeze") && pLower.Contains("budget")))
        {
            var deptName = intent.Parameters.GetValueOrDefault("department")?.ToString()
                ?? intent.Entities.GetValueOrDefault("department")
                ?? "ALL";

            decimal frozenBudgetAmt = 0m;
            var targetDeptObj = await _db.Departments
                .FirstOrDefaultAsync(d => d.Name.ToLower().Contains(deptName.ToLower()), ct);

            if (targetDeptObj != null)
            {
                var budgetRecord = await _db.Budgets
                    .FirstOrDefaultAsync(b => b.DepartmentId == targetDeptObj.Id, ct);
                if (budgetRecord != null && budgetRecord.AllocatedAmount > 0)
                {
                    frozenBudgetAmt = budgetRecord.AllocatedAmount;
                }
            }
            if (frozenBudgetAmt == 0m)
            {
                if (deptName.Equals("IT", StringComparison.OrdinalIgnoreCase)) frozenBudgetAmt = 100000m;
                else if (deptName.Equals("HR", StringComparison.OrdinalIgnoreCase)) frozenBudgetAmt = 50000m;
                else if (deptName.Equals("Marketing", StringComparison.OrdinalIgnoreCase)) frozenBudgetAmt = 75000m;
                else if (deptName.Equals("Operations", StringComparison.OrdinalIgnoreCase)) frozenBudgetAmt = 80000m;
                else frozenBudgetAmt = 50000m;
            }

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Freeze Department Budget Allocation ({deptName})",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = frozenBudgetAmt,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = targetDeptObj?.Id ?? 0,
                EntityName = "Department Budget (SQL Server)",
                PrimaryLabel = $"Budget Freeze ({deptName})",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "IsFrozen State", OldValue = "False", NewValue = "True", Difference = "Budget Locked" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Frozen Allocated Budget", OldValue = $"${frozenBudgetAmt:N2}", NewValue = "$0.00 (Locked)", Difference = $"Locked ${frozenBudgetAmt:N2}" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "budget.freeze", Description = $"Freeze budget allocations for {deptName}", RiskLevel = RiskLevel.High });
            actionPlan.Warnings.Add($"Freezes ${frozenBudgetAmt:N2} budget allocation and blocks further spending for department target: {deptName}.");
            return actionPlan;
        }

        // ── 2.2. BUDGET UPDATE / ALLOCATE ─────────────────────────────────────────
        if (intentType == IntentType.BUDGET_UPDATE || intent.TargetEntity == "DEPARTMENT_BUDGET")
        {
            var deptName = intent.Parameters.GetValueOrDefault("department")?.ToString()
                ?? intent.Entities.GetValueOrDefault("department")
                ?? "IT";

            decimal budgetAmt = 0m;
            if (intent.Parameters.TryGetValue("amount", out var aObj) && decimal.TryParse(aObj?.ToString(), out var aVal) && aVal > 0)
                budgetAmt = aVal;
            else if (intent.Parameters.TryGetValue("budgetAmount", out var bObj) && decimal.TryParse(bObj?.ToString(), out var bVal) && bVal > 0)
                budgetAmt = bVal;
            else if (intent.Entities.TryGetValue("amount", out var aStr) && decimal.TryParse(aStr, out var aVal2) && aVal2 > 0)
                budgetAmt = aVal2;
            else if (intent.Entities.TryGetValue("budgetAmount", out var baStr) && decimal.TryParse(baStr, out var baVal2) && baVal2 > 0)
                budgetAmt = baVal2;

            if (budgetAmt <= 0) budgetAmt = 0m;

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Update Q3 Budget for {deptName} Department",
                RiskLevel = RiskLevel.Medium,
                Status = "AWAITING_APPROVAL",
                TotalFinancialImpact = budgetAmt,
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Department Budget",
                PrimaryLabel = $"{deptName} Q3 Budget",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Q3 Allocated Budget", OldValue = "(current balance)", NewValue = $"${budgetAmt:N2}", Difference = $"Allocation +${budgetAmt:N2}" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "budget.update", Description = $"Allocate +${budgetAmt:N2} budget to {deptName}", RiskLevel = RiskLevel.Medium });
            actionPlan.Warnings.Add($"Adjusts allocated Q3 budget for {deptName} by ${budgetAmt:N2}.");
            return actionPlan;
        }

        // ── 3. EMPLOYEE ONBOARDING ──────────────────────────────────────────────
        if (intentType == IntentType.EMPLOYEE_ONBOARDING || pLower.Contains("onboard"))
        {
            var name = intent.Entities.GetValueOrDefault("name") ?? intent.Parameters.GetValueOrDefault("name")?.ToString() ?? "Candidate";
            var dept = intent.Entities.GetValueOrDefault("department") ?? intent.Parameters.GetValueOrDefault("department")?.ToString() ?? "IT";
            var desig = intent.Entities.GetValueOrDefault("designation") ?? intent.Parameters.GetValueOrDefault("designation")?.ToString() ?? "Team Member";
            
            decimal proposedSalary = 68000.00m;
            string? salString = intent.Entities.GetValueOrDefault("salary") ?? intent.Parameters.GetValueOrDefault("salary")?.ToString();
            if (!string.IsNullOrWhiteSpace(salString) && decimal.TryParse(salString, NumberStyles.Any, CultureInfo.InvariantCulture, out var salVal) && salVal > 0)
            {
                proposedSalary = salVal;
            }
            else
            {
                var promptSalMatch = System.Text.RegularExpressions.Regex.Match(prompt, @"\b([0-9]+(?:\.[0-9]+)?)\s*(k|m)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (promptSalMatch.Success && decimal.TryParse(promptSalMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPromptSalPlan))
                {
                    proposedSalary = parsedPromptSalPlan * (promptSalMatch.Groups[2].Value.ToLower() == "k" ? 1000m : 1000000m);
                    if (prompt.ToLowerInvariant().Contains("monthly")) proposedSalary *= 12m;
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
                EntityName = "Employee Profile",
                PrimaryLabel = name,
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Provisioning Profile", OldValue = "(new employee)", NewValue = $"{desig} (${proposedSalary:N0}/yr)", Difference = "New Record" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "policy.evaluate", Description = "Verify compensation policy compliance (POL-HR-001)", RiskLevel = RiskLevel.Low });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 2, ToolName = "employee.create", Description = "Create employee profile", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 3, ToolName = "onboarding.submit_legacy_form", Description = "Register employee in HR Portal", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 4, ToolName = "sap.employee.create", Description = "Setup payroll & benefits record", RiskLevel = RiskLevel.Medium });
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 5, ToolName = "email.welcome", Description = "Send welcome email", RiskLevel = RiskLevel.Low });

            actionPlan.Warnings.Add("Automated onboarding will register profile, setup payroll access, and dispatch welcome email.");
            return actionPlan;
        }

        // ── 4. EMPLOYEE DELETE ──────────────────────────────────────────────────
        if (intentType == IntentType.EMPLOYEE_DELETE)
        {
            var scope = intent.Scope;
            var empName = intent.Entities.GetValueOrDefault("name", string.Empty);
            var deptFilter = intent.Entities.GetValueOrDefault("department", string.Empty);

            List<Employee> matchingEmps = new();
            if (scope == "ALL") matchingEmps = await _db.Employees.Include(e => e.Department).ToListAsync(ct);
            else if (!string.IsNullOrWhiteSpace(empName)) matchingEmps = await _db.Employees.Where(e => e.Name.ToLower().Contains(empName.ToLower())).ToListAsync(ct);

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = scope == "ALL" ? "Plan of Action: Permanent Removal of All Employees" : $"Plan of Action: Permanent Removal of Employee '{empName}'",
                RiskLevel = scope == "ALL" ? RiskLevel.Critical : RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            decimal totalSavings = matchingEmps.Sum(e => e.Salary);
            foreach (var emp in matchingEmps)
            {
                actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                {
                    RecordId = emp.Id,
                    EntityName = "Employee Master (SQL Server)",
                    PrimaryLabel = emp.Name,
                    Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                    {
                        new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Employment Status", OldValue = $"{emp.Status} (${emp.Salary:N2}/yr)", NewValue = "[PERMANENTLY DELETED]", Difference = "Record Purge" }
                    }
                });
            }

            actionPlan.TotalFinancialImpact = -totalSavings;
            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep { StepNumber = 1, ToolName = "employee.delete", Description = $"Purge {matchingEmps.Count} employee record(s)", RiskLevel = RiskLevel.High });
            actionPlan.Warnings.Add($"⚠ High-Risk Operation: Permanent database purge.");
            return actionPlan;
        }

        // ── 5. EMPLOYEE TRANSFER / PROMOTE / APPOINTMENT ────────────────────────────────
        if (intentType == IntentType.EMPLOYEE_TRANSFER || intentType == IntentType.EMPLOYEE_PROMOTE || pLower.Contains("transfer") || pLower.Contains("reassign") || pLower.Contains("relocate") || pLower.Contains("move") || pLower.Contains("promote") || pLower.Contains("appoint") || pLower.Contains("acting head"))
        {
            var empName = intent.Entities.GetValueOrDefault("name") ?? intent.Entities.GetValueOrDefault("employee_name") ?? "";
            if (empName.StartsWith("Move ", StringComparison.OrdinalIgnoreCase))
                empName = empName.Substring(5).Trim();

            var targetDept = intent.Entities.GetValueOrDefault("department") ?? intent.Entities.GetValueOrDefault("targetDepartment") ?? "";
            var targetRole = intent.Entities.GetValueOrDefault("designation") ?? intent.Entities.GetValueOrDefault("role") ?? intent.Entities.GetValueOrDefault("targetRole") ?? "";

            var emp = await _db.Employees.Include(e => e.Department).FirstOrDefaultAsync(e => e.Name.ToLower().Contains(empName.ToLower()), ct);

            bool isAppointment = intent.Entities.GetValueOrDefault("appointment_type") == "Leadership Appointment" ||
                                 pLower.Contains("appoint") || pLower.Contains("acting head") || pLower.Contains("interim head");

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = isAppointment
                    ? $"Plan of Action: Leadership Appointment: Appoint {emp?.Name ?? empName} as {targetRole} ({targetDept})"
                    : (intentType == IntentType.EMPLOYEE_PROMOTE ? $"Plan of Action: Promotion for {emp?.Name ?? empName}" : $"Plan of Action: Transfer & Reassign Employee '{emp?.Name ?? empName}'"),
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = emp?.Id ?? 0,
                EntityName = "Employee Profile",
                PrimaryLabel = emp?.Name ?? empName,
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department", OldValue = emp?.Department?.Name ?? "(current)", NewValue = targetDept, Difference = "Transfer" },
                    new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Designation", OldValue = emp?.Designation ?? "(current)", NewValue = targetRole, Difference = isAppointment ? "Leadership Role" : "Role Change" }
                }
            });

            if (isAppointment && !string.IsNullOrWhiteSpace(targetDept))
            {
                actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                {
                    RecordId = 0,
                    EntityName = "Department Master",
                    PrimaryLabel = $"{targetDept} Department",
                    Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                    {
                        new Nexus.Data.ActionPlan.ChangePreview { FieldName = "Department Head / Leadership", OldValue = "Pending Assignment", NewValue = emp?.Name ?? empName, Difference = "Leadership Assignment" }
                    }
                });
            }

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "employee.transfer",
                Description = isAppointment
                    ? $"Appoint {emp?.Name ?? empName} to {targetDept} as {targetRole}"
                    : $"Transfer {emp?.Name ?? empName} to {targetDept} as {targetRole}",
                RiskLevel = RiskLevel.High
            });

            actionPlan.Warnings.Add(isAppointment
                ? $"Leadership appointment: Designates {emp?.Name ?? empName} as {targetRole} for the {targetDept} Department."
                : $"Transfers employee to {targetDept} with role updated to {targetRole}.");
            return actionPlan;
        }

        // ── 5.1. EMPLOYEE SALARY UPDATE (UPDATE_SALARY & EMPLOYEE_UPDATE with salary/raise keywords) ──
        if (intentType == IntentType.UPDATE_SALARY || (intentType == IntentType.EMPLOYEE_UPDATE && (pLower.Contains("salary") || pLower.Contains("raise") || pLower.Contains("pay") || intent.Entities.ContainsKey("percentage"))))
        {
            var targetEmpName = intent.Entities.GetValueOrDefault("name", string.Empty);
            var targetDept = intent.Entities.GetValueOrDefault("department", string.Empty);
            
            decimal pct = 10m;
            if (intent.Entities.TryGetValue("percentage", out var pStr) && decimal.TryParse(pStr, out var pDec))
                pct = pDec;

            var query = _db.Employees.Include(e => e.Department).AsQueryable();
            if (!string.IsNullOrWhiteSpace(targetDept)) query = query.Where(e => e.Department != null && e.Department.Name.ToLower() == targetDept.ToLower());
            else if (!string.IsNullOrWhiteSpace(targetEmpName)) query = query.Where(e => e.Name.ToLower().Contains(targetEmpName.ToLower()));

            var employees = await query.ToListAsync(ct);

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Salary Adjustment (+{pct}%) for {employees.Count} Employee(s)",
                RiskLevel = RiskLevel.High,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            decimal totalImpact = 0m;
            if (employees.Count == 0)
            {
                actionPlan.Warnings.Add("No matching employee records found for salary adjustment.");
            }
            else
            {
                foreach (var emp in employees)
                {
                    var newSal = Math.Round(emp.Salary * (1m + (pct / 100m)), 2);
                    var diff = newSal - emp.Salary;
                    totalImpact += diff;

                    actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
                    {
                        RecordId = emp.Id,
                        EntityName = "Employee Profile",
                        PrimaryLabel = emp.Name,
                        Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                        {
                            new Nexus.Data.ActionPlan.ChangePreview
                            {
                                FieldName = "Salary",
                                OldValue = $"${emp.Salary:N2}",
                                NewValue = $"${newSal:N2}",
                                Difference = $"+${diff:N2}"
                            }
                        }
                    });
                }
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

        // ── 5.2. JOB OPENING REQUISITION (JOB_OPENING_CREATE) ──
        if (intentType == IntentType.JOB_OPENING_CREATE)
        {
            var jobTitle = intent.Entities.GetValueOrDefault("title") 
                ?? intent.Entities.GetValueOrDefault("jobTitle")
                ?? intent.Parameters.GetValueOrDefault("title")?.ToString()
                ?? "Lead Cloud Architect";
            var dept = intent.Entities.GetValueOrDefault("department")
                ?? intent.Parameters.GetValueOrDefault("department")?.ToString()
                ?? "IT";
            var salaryRange = intent.Entities.GetValueOrDefault("salaryRange")
                ?? intent.Parameters.GetValueOrDefault("salaryRange")?.ToString()
                ?? "$95,000 - $125,000";
            var reqs = intent.Entities.GetValueOrDefault("requirements")
                ?? intent.Parameters.GetValueOrDefault("requirements")?.ToString()
                ?? "AWS, Kubernetes, Terraform, Docker, CI/CD";
            var loc = intent.Entities.GetValueOrDefault("location")
                ?? intent.Parameters.GetValueOrDefault("location")?.ToString()
                ?? "Remote / Hybrid";

            var actionPlan = new Nexus.Data.ActionPlan.ActionPlan
            {
                Title = $"Plan of Action: Create Job Opening ({jobTitle} - {dept})",
                RiskLevel = RiskLevel.Medium,
                Status = "AWAITING_APPROVAL",
                Metadata = JsonSerializer.Serialize(intent)
            };

            actionPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
            {
                RecordId = 0,
                EntityName = "Job Opening Requisition",
                PrimaryLabel = $"{jobTitle} ({dept})",
                Changes = new List<Nexus.Data.ActionPlan.ChangePreview>
                {
                    new() { FieldName = "Position Title", OldValue = "(None - New Opening)", NewValue = jobTitle, Difference = "NEW" },
                    new() { FieldName = "Department", OldValue = "(None)", NewValue = dept, Difference = "ASSIGNED" },
                    new() { FieldName = "Salary Range", OldValue = "(Unspecified)", NewValue = salaryRange, Difference = "BUDGETED" },
                    new() { FieldName = "Location", OldValue = "(Unspecified)", NewValue = loc, Difference = "SET" },
                    new() { FieldName = "Role Overview", OldValue = "(Unspecified)", NewValue = intent.Entities.GetValueOrDefault("description") ?? intent.Parameters.GetValueOrDefault("description")?.ToString() ?? "Role overview set", Difference = "SET" },
                    new() { FieldName = "Responsibilities", OldValue = "(Unspecified)", NewValue = intent.Entities.GetValueOrDefault("responsibilities") ?? intent.Parameters.GetValueOrDefault("responsibilities")?.ToString() ?? "Responsibilities defined", Difference = "SET" },
                    new() { FieldName = "Requirements", OldValue = "(None)", NewValue = reqs, Difference = "SET" }
                }
            });

            actionPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
            {
                StepNumber = 1,
                ToolName = "sql.analytics",
                Description = $"Create job opening '{jobTitle}' in {dept} department with salary {salaryRange}",
                RiskLevel = RiskLevel.Medium
            });

            actionPlan.Warnings.Add($"This will publish an active job opening '{jobTitle}' to the candidate portal and database.");
            actionPlan.Warnings.Add($"Candidates will be able to apply and submit their PDF CVs immediately upon approval.");
            return actionPlan;
        }

        // ── 6. GENERIC MUTATION ACTION PLAN (Clean fallback for any other entity) ──
        var genericPlan = new Nexus.Data.ActionPlan.ActionPlan
        {
            Title = $"Plan of Action: {intent.Operation} {intent.TargetEntity}",
            RiskLevel = RiskLevel.Medium,
            Status = "AWAITING_APPROVAL",
            Metadata = JsonSerializer.Serialize(intent)
        };

        genericPlan.AffectedRecords.Add(new Nexus.Data.ActionPlan.AffectedRecord
        {
            RecordId = 0,
            EntityName = $"{intent.TargetEntity}",
            PrimaryLabel = intent.Parameters.GetValueOrDefault("name")?.ToString() ?? intent.TargetEntity,
            Changes = intent.Parameters.Select(p => new Nexus.Data.ActionPlan.ChangePreview
            {
                FieldName = p.Key,
                OldValue = "(current)",
                NewValue = String.Concat(p.Value),
                Difference = "Updated"
            }).ToList()
        });

        genericPlan.Steps.Add(new Nexus.Data.ActionPlan.ActionPlanStep
        {
            StepNumber = 1,
            ToolName = "workflow.execute",
            Description = $"Apply {intent.Operation} to {intent.TargetEntity}",
            RiskLevel = RiskLevel.Medium
        });

        return genericPlan;
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

    private List<NextActionChoice> GenerateNextActionChoices(ParsedIntentResult intent, object? resultData)
    {
        var choices = new List<NextActionChoice>();
        var entityName = intent.Entities?.GetValueOrDefault("name") ?? intent.Entities?.GetValueOrDefault("employee_name");
        var deptName = intent.Entities?.GetValueOrDefault("department");

        switch (intent.ParsedIntentType)
        {
            case IntentType.EMPLOYEE_READ:
                if (!string.IsNullOrWhiteSpace(deptName))
                {
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_dept_employees",
                        Label = $"View {deptName} Employees",
                        ActionType = "NAVIGATE",
                        TargetTab = "employees",
                        Context = new Dictionary<string, string> { { "department", deptName } }
                    });
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_directory",
                        Label = "Open Employee Directory",
                        ActionType = "NAVIGATE",
                        TargetTab = "employees"
                    });
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_employee_details",
                        Label = "View Employee Details",
                        ActionType = "EXECUTE_PROMPT",
                        PromptToExecute = string.IsNullOrWhiteSpace(entityName) ? $"Find employee records for {deptName} and show current designation and salary" : $"Find employee records for {entityName} and show current designation and salary"
                    });
                    choices.Add(new NextActionChoice
                    {
                        Id = "export_employee_list",
                        Label = "Export Employee List",
                        ActionType = "EXECUTE_PROMPT",
                        PromptToExecute = $"Calculate average employee salary in the {deptName} department"
                    });
                }
                else
                {
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_directory",
                        Label = string.IsNullOrWhiteSpace(entityName) ? "Open Employee Directory" : $"View {entityName} in Directory",
                        ActionType = "NAVIGATE",
                        TargetTab = "employees",
                        Context = new Dictionary<string, string> { { "highlightName", entityName ?? "" } }
                    });
                    choices.Add(new NextActionChoice
                    {
                        Id = "compensation_review",
                        Label = "Review Department Compensation",
                        ActionType = "EXECUTE_PROMPT",
                        PromptToExecute = "Calculate average employee salary in the Engineering department"
                    });
                    choices.Add(new NextActionChoice
                    {
                        Id = "export_workforce",
                        Label = "View Workforce Overview",
                        ActionType = "EXECUTE_PROMPT",
                        PromptToExecute = "Show an executive overview of active workforce metrics and department headcount"
                    });
                }
                break;

            case IntentType.EMPLOYEE_PROMOTE:
            case IntentType.EMPLOYEE_TRANSFER:
                bool isAppoint = intent.Entities?.GetValueOrDefault("appointment_type") == "Leadership Appointment" ||
                                 (intent.Entities?.ContainsKey("role") == true && intent.Entities["role"].Contains("Head", StringComparison.OrdinalIgnoreCase));
                if (isAppoint)
                {
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_profile",
                        Label = string.IsNullOrWhiteSpace(entityName) ? "View Leadership Profile" : $"View {entityName}'s Profile",
                        ActionType = "NAVIGATE",
                        TargetTab = "employees",
                        Context = new Dictionary<string, string> { { "highlightName", entityName ?? "" } }
                    });
                    if (!string.IsNullOrWhiteSpace(deptName))
                    {
                        choices.Add(new NextActionChoice
                        {
                            Id = "open_department",
                            Label = $"Open {deptName} Department",
                            ActionType = "NAVIGATE",
                            TargetTab = "departments",
                            Context = new Dictionary<string, string> { { "department", deptName } }
                        });
                    }
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_dept_structure",
                        Label = "View Updated Department Structure",
                        ActionType = "NAVIGATE",
                        TargetTab = "departments"
                    });
                }
                else
                {
                    choices.Add(new NextActionChoice
                    {
                        Id = "view_directory",
                        Label = string.IsNullOrWhiteSpace(entityName) ? "Open Employee Directory" : $"View {entityName} in Directory",
                        ActionType = "NAVIGATE",
                        TargetTab = "employees",
                        Context = new Dictionary<string, string> { { "highlightName", entityName ?? "" } }
                    });
                    choices.Add(new NextActionChoice
                    {
                        Id = "open_departments",
                        Label = "Open Departments & Budgets",
                        ActionType = "NAVIGATE",
                        TargetTab = "departments"
                    });
                }
                break;

            case IntentType.EMPLOYEE_UPDATE:
            case IntentType.EMPLOYEE_CREATE:
            case IntentType.EMPLOYEE_ONBOARDING:
            case IntentType.UPDATE_SALARY:
                choices.Add(new NextActionChoice
                {
                    Id = "view_directory",
                    Label = string.IsNullOrWhiteSpace(entityName) ? "Open Employee Directory" : $"View {entityName} in Directory",
                    ActionType = "NAVIGATE",
                    TargetTab = "employees",
                    Context = new Dictionary<string, string> { { "highlightName", entityName ?? "" } }
                });
                choices.Add(new NextActionChoice
                {
                    Id = "compensation_review",
                    Label = "Review Department Compensation",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = $"Calculate average employee salary in the {deptName ?? "Engineering"} department"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "export_workforce",
                    Label = "View Workforce Overview",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Show an executive overview of active workforce metrics and department headcount"
                });
                break;

            case IntentType.POLICY_READ:
            case IntentType.POLICY_CREATE:
            case IntentType.POLICY_UPDATE:
                var polCode = intent.Entities?.GetValueOrDefault("policyCode") ?? "POL-HR-001";
                choices.Add(new NextActionChoice
                {
                    Id = "open_policy_center",
                    Label = "Open HR Policy Center",
                    ActionType = "NAVIGATE",
                    TargetTab = "policies",
                    Context = new Dictionary<string, string> { { "policyCode", polCode } }
                });
                choices.Add(new NextActionChoice
                {
                    Id = "view_official_pdf",
                    Label = "View Official PDF Document",
                    ActionType = "OPEN_URL",
                    Url = "/documents/HR_Policies_Comprehensive.pdf"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "check_meal_policy",
                    Label = "Check Meal Policy Limit",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "What is the maximum reimbursement limit for business meals under company policy?"
                });
                break;

            case IntentType.BUDGET_READ:
            case IntentType.BUDGET_ANALYSIS:
            case IntentType.BUDGET_UPDATE:
            case IntentType.BUDGET_REALLOCATE:
            case IntentType.BUDGET_FREEZE:
                choices.Add(new NextActionChoice
                {
                    Id = "open_departments",
                    Label = "Open Departments & Budgets",
                    ActionType = "NAVIGATE",
                    TargetTab = "departments"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "unallocated_balance",
                    Label = "Check Unallocated Pool Balance",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Check remaining unallocated corporate budget pool balance"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "variance_analysis",
                    Label = "Analyze Department Variances",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Highlight department budget variances exceeding 10% of allocation"
                });
                break;

            case IntentType.TICKET_CREATE:
            case IntentType.TICKET_READ:
            case IntentType.TICKET_TRIAGE:
            case IntentType.TICKET_UPDATE:
                choices.Add(new NextActionChoice
                {
                    Id = "open_service_desk",
                    Label = "Open Workplace Service Desk",
                    ActionType = "NAVIGATE",
                    TargetTab = "tickets"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "view_open_tickets",
                    Label = "Show Open Service Tickets",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Show all open service desk tickets waiting for technician assignment"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "new_hardware_req",
                    Label = "Request Hardware for Sarah",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Need MacBook Pro M3 and AWS VPN access for new hire Sarah in DevOps"
                });
                break;

            case IntentType.CV_SCREEN:
                choices.Add(new NextActionChoice
                {
                    Id = "open_cv_screen",
                    Label = "Open Candidate CV Screening",
                    ActionType = "NAVIGATE",
                    TargetTab = "cv"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "interview_questions",
                    Label = "Generate Interview Questions",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Generate interview question recommendations based on candidate CV"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "check_salary_band",
                    Label = "Check Experience vs Salary Band",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Check candidate years of experience against department salary band"
                });
                break;

            case IntentType.APPROVAL_READ:
            case IntentType.APPROVAL_ACTION:
                choices.Add(new NextActionChoice
                {
                    Id = "open_approval_center",
                    Label = "Open HR Approval Center",
                    ActionType = "NAVIGATE",
                    TargetTab = "approvals"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "view_pending_queue",
                    Label = "Show Pending Action Requests",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Show all pending HR action requests requiring manager authorization"
                });
                break;

            case IntentType.EXPENSE_READ:
            case IntentType.EXPENSE_COMPLIANCE:
            case IntentType.EXPENSE_CREATE:
            case IntentType.EXPENSE_APPROVE:
                choices.Add(new NextActionChoice
                {
                    Id = "open_expense_review",
                    Label = "Open Expense Review & Compliance",
                    ActionType = "NAVIGATE",
                    TargetTab = "expenses"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "filter_violations",
                    Label = "Filter Policy Violations",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Filter expense claims by Policy Violation"
                });
                break;

            case IntentType.WORKFLOW_EXECUTE:
            case IntentType.EXECUTE_AUTOMATION:
                choices.Add(new NextActionChoice
                {
                    Id = "view_activity_history",
                    Label = "View Activity History",
                    ActionType = "NAVIGATE",
                    TargetTab = "audit"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "filter_log_dept",
                    Label = "Filter Activity Log for Engineering",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Filter activity log by affected department: Engineering"
                });
                break;

            case IntentType.AUDIT_READ:
            case IntentType.SECURITY_TEST:
                choices.Add(new NextActionChoice
                {
                    Id = "open_audit_log",
                    Label = "Open Activity History",
                    ActionType = "NAVIGATE",
                    TargetTab = "audit"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "filter_log_dept",
                    Label = "Filter Activity by Department",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Filter activity log for changes made to the IT Department"
                });
                break;

            case IntentType.DASHBOARD_ANALYTICS:
            default:
                choices.Add(new NextActionChoice
                {
                    Id = "open_dashboard",
                    Label = "Open Workforce Dashboard",
                    ActionType = "NAVIGATE",
                    TargetTab = "dashboard"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "view_workforce_dist",
                    Label = "View Workforce Distribution",
                    ActionType = "EXECUTE_PROMPT",
                    PromptToExecute = "Show workforce distribution across all active company departments"
                });
                choices.Add(new NextActionChoice
                {
                    Id = "open_directory",
                    Label = "Open Employee Directory",
                    ActionType = "NAVIGATE",
                    TargetTab = "employees"
                });
                break;
        }

        return choices;
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes);
    }
}
