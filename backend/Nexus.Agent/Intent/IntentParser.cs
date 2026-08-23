using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexus.Data.LLM;

namespace Nexus.Agent.Intent;

public class IntentParser : IIntentParser
{
    private readonly ILLMService _llmService;
    private readonly ILogger<IntentParser> _logger;

    public IntentParser(ILLMService llmService, ILogger<IntentParser> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ParsedIntentResult> ParseIntentAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return new ParsedIntentResult
            {
                Intent = IntentType.UNKNOWN.ToString(),
                TargetEntity = "UNKNOWN",
                Operation = "NONE",
                Scope = "NONE",
                Confidence = 0.0,
                RequiresClarification = true,
                ClarificationPrompt = "Please enter a valid request prompt.",
                RawPrompt = userPrompt ?? string.Empty
            };
        }

        var systemPrompt = BuildSystemPrompt();

        ParsedIntentResult? result = null;
        string? llmError = null;

        try
        {
            result = await _llmService.GenerateJsonAsync<ParsedIntentResult>(userPrompt, systemPrompt, cancellationToken);
        }
        catch (Exception ex)
        {
            llmError = $"Gemini API error: {ex.Message}";
            _logger.LogWarning(ex, "LLM call failed for prompt: '{Prompt}'", userPrompt);
        }

        // Use rule-based fallback when LLM is unavailable or returned null
        if (result == null)
        {
            _logger.LogWarning("LLM returned null result; using rule-based fallback. Error: {Error}", llmError ?? "unknown");
            result = RuleBasedFallbackParse(userPrompt);
            result.LlmError = llmError;
        }
        else if (string.IsNullOrWhiteSpace(result.Intent) || result.Intent.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            var rulesResult = RuleBasedFallbackParse(userPrompt);
            if (rulesResult.Intent != "UNKNOWN" && rulesResult.Confidence >= 0.8)
            {
                result.Intent = rulesResult.Intent;
                result.TargetEntity = rulesResult.TargetEntity;
                result.Operation = rulesResult.Operation;
                result.Confidence = rulesResult.Confidence;
            }
        }

        // Disambiguate and cross-validate against entity-attribute confusion
        DisambiguateAndSanitizeIntent(userPrompt, result);

        // Always enrich with deterministic entity/scope extraction
        EnrichIntentScopeAndEntities(userPrompt, result);
        result.RawPrompt = userPrompt;
        ValidateIntentResult(result);

        return result;
    }

    private static string BuildSystemPrompt()
    {
        return @"You are NEXUS ENTERPRISE SEMANTIC INTENT PARSER — an advanced AI architect for an enterprise workforce management system.

YOUR PRINCIPLE RESPONSIBILITY:
Determine the PRIMARY TARGET ENTITY being acted upon, the OPERATION, and extract the ATTRIBUTES and VALUES.

CRITICAL DISAMBIGUATION RULES:
1. TARGET ENTITY vs ATTRIBUTE:
   - A field/attribute reference must NEVER automatically become an operation on that entity.
   - Example: 'add employee Ali whose salary is 80k and his department is IT'
     * Target Entity = EMPLOYEE
     * Operation = CREATE
     * Parameters: name='Ali', salary=80000, department='IT', designation='.NET Junior Developer'
     * Intent = CREATE_EMPLOYEE (NOT CREATE_DEPARTMENT!)
     * 'department' here is an attribute value of the employee, NOT a command to create a department!

   - Example: 'Add Sara as an employee in the HR department'
     * Target Entity = EMPLOYEE, Operation = CREATE, department = 'HR' (NOT CREATE_DEPARTMENT)

   - Example: 'Update Ali's salary to 100,000 and move him to Finance'
     * Target Entity = EMPLOYEE, Operation = UPDATE, parameters: employee='Ali', salary=100000, department='Finance' (NOT UPDATE_DEPARTMENT)

   - Example: 'Create a new employee in the IT department'
     * Target Entity = EMPLOYEE, Operation = CREATE, department = 'IT' (NOT CREATE_DEPARTMENT)

   - Example: 'Which employees are working in the IT department?'
     * Target Entity = EMPLOYEE, Operation = LIST, filters: department = 'IT' (NOT GET_DEPARTMENT)

   - Example: 'Show me the budget of the IT department'
     * Target Entity = DEPARTMENT_BUDGET, Operation = READ, department = 'IT'

   - Example: 'Increase the IT department budget by 50,000'
     * Target Entity = DEPARTMENT_BUDGET, Operation = ALLOCATE, department = 'IT', amount = 50000

   - Example: 'Create a new department called QA'
     * Target Entity = DEPARTMENT, Operation = CREATE, name = 'QA'

2. SUPPORTED TARGET ENTITIES:
   - EMPLOYEE
   - DEPARTMENT
   - DEPARTMENT_BUDGET
   - POLICY
   - EXPENSE
   - ONBOARDING
   - APPROVAL
   - AUDIT_LOG

3. SUPPORTED OPERATIONS:
   - CREATE
   - READ
   - LIST
   - SEARCH
   - UPDATE
   - DELETE
   - ANALYZE
   - ALLOCATE
   - APPROVE
   - REJECT

4. SUPPORTED INTENTS (Output exact intent string):
   - CREATE_EMPLOYEE (or EMPLOYEE_CREATE)
   - LIST_EMPLOYEES / GET_EMPLOYEES (or EMPLOYEE_READ)
   - UPDATE_EMPLOYEE (or EMPLOYEE_UPDATE)
   - DELETE_EMPLOYEE (or EMPLOYEE_DELETE)
   - ONBOARD_EMPLOYEE (or EMPLOYEE_ONBOARDING)
   - CREATE_DEPARTMENT (or DEPARTMENT_CREATE)
   - GET_DEPARTMENTS (or DEPARTMENT_READ)
   - UPDATE_DEPARTMENT (or DEPARTMENT_UPDATE)
   - DELETE_DEPARTMENT (or DEPARTMENT_DELETE)
   - GET_DEPARTMENT_BUDGET (or BUDGET_READ)
   - ALLOCATE_DEPARTMENT_BUDGET (or BUDGET_UPDATE)
   - ANALYZE_BUDGET (or BUDGET_ANALYSIS)
   - CREATE_POLICY (or POLICY_CREATE)
   - GET_POLICY (or POLICY_READ)
   - UPDATE_POLICY (or POLICY_UPDATE)
   - DELETE_POLICY (or POLICY_DELETE)
   - SUBMIT_EXPENSE (or EXPENSE_CREATE)
   - CHECK_EXPENSE (or EXPENSE_COMPLIANCE)
   - GET_EXPENSES (or EXPENSE_READ)

5. MIXED LANGUAGE & INFORMAL PHRASING:
   - 'Ali ko IT department mein employee add karo salary 80k aur designation junior .NET developer'
     * Intent: CREATE_EMPLOYEE, TargetEntity: EMPLOYEE, Operation: CREATE
     * Parameters: name='Ali', salary=80000, department='IT', designation='Junior .NET Developer'

OUTPUT CONTRACT:
Return ONLY a valid raw JSON object matching this exact structure (no markdown fences, no extra text):

{
  ""intent"": ""CREATE_EMPLOYEE"",
  ""targetEntity"": ""EMPLOYEE"",
  ""operation"": ""CREATE"",
  ""scope"": ""SINGLE"",
  ""parameters"": {
    ""name"": ""Ali"",
    ""salary"": 80000,
    ""department"": ""IT"",
    ""designation"": "".NET Junior Developer""
  },
  ""filters"": {},
  ""missingFields"": [],
  ""requiresPolicyLookup"": false,
  ""requiresApproval"": false,
  ""confidence"": 0.95
}";
    }

    /// <summary>
    /// Cross-validates and sanity-guards parsed intent to prevent entity-attribute confusion
    /// (e.g. prompt adding an employee with department="IT" being misclassified as CREATE_DEPARTMENT).
    /// </summary>
    private void DisambiguateAndSanitizeIntent(string prompt, ParsedIntentResult result)
    {
        var p = prompt.ToLowerInvariant();

        // Check if prompt clearly indicates an employee action
        bool containsEmployeeVerbs = p.Contains("employee") || p.Contains("emp") || p.Contains("onboard") ||
                                     p.Contains("hire") || p.Contains("salary is") || p.Contains("designation is") ||
                                     p.Contains("junior dev") || p.Contains("senior dev") || p.Contains("developer") ||
                                     Regex.IsMatch(p, @"\badd\s+employee\b|\bcreate\s+employee\b|\bnew\s+employee\b|\bemployee\s+ali\b|\bemployee\s+ahmed\b");

        // GUARD 1: Misclassified DEPARTMENT when prompt is actually creating/updating an EMPLOYEE
        if ((result.TargetEntity.Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase) ||
             result.Intent.Equals("CREATE_DEPARTMENT", StringComparison.OrdinalIgnoreCase) ||
             result.Intent.Equals("DEPARTMENT_CREATE", StringComparison.OrdinalIgnoreCase) ||
             result.Intent.Equals("UPDATE_DEPARTMENT", StringComparison.OrdinalIgnoreCase) ||
             result.Intent.Equals("DEPARTMENT_UPDATE", StringComparison.OrdinalIgnoreCase)) &&
            containsEmployeeVerbs)
        {
            _logger.LogWarning("SANITY GUARD TRIGGERED: Misclassified DEPARTMENT action corrected to EMPLOYEE for prompt: '{Prompt}'", prompt);
            
            bool isUpdate = p.Contains("update") || p.Contains("change") || p.Contains("move") || p.Contains("promote");
            result.TargetEntity = "EMPLOYEE";
            result.Operation = isUpdate ? "UPDATE" : "CREATE";
            result.Intent = isUpdate ? "UPDATE_EMPLOYEE" : "CREATE_EMPLOYEE";

            string? dept = null;
            if (result.Entities.TryGetValue("department", out var d1) && !string.IsNullOrWhiteSpace(d1))
            {
                dept = d1;
            }
            else if (result.Parameters.TryGetValue("department", out var d2Obj) && d2Obj != null)
            {
                dept = d2Obj.ToString();
            }

            if (!string.IsNullOrWhiteSpace(dept))
            {
                result.Parameters["department"] = dept;
                result.Entities["department"] = dept;
            }
        }

        // GUARD 2: Ensure TargetEntity & Operation are normalized if missing
        if (string.IsNullOrWhiteSpace(result.TargetEntity) || result.TargetEntity.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            switch (result.ParsedIntentType)
            {
                case IntentType.EMPLOYEE_CREATE:
                case IntentType.EMPLOYEE_ONBOARDING:
                    result.TargetEntity = "EMPLOYEE";
                    result.Operation = "CREATE";
                    break;
                case IntentType.EMPLOYEE_READ:
                    result.TargetEntity = "EMPLOYEE";
                    result.Operation = "READ";
                    break;
                case IntentType.EMPLOYEE_UPDATE:
                    result.TargetEntity = "EMPLOYEE";
                    result.Operation = "UPDATE";
                    break;
                case IntentType.EMPLOYEE_DELETE:
                    result.TargetEntity = "EMPLOYEE";
                    result.Operation = "DELETE";
                    break;
                case IntentType.DEPARTMENT_CREATE:
                    result.TargetEntity = "DEPARTMENT";
                    result.Operation = "CREATE";
                    break;
                case IntentType.DEPARTMENT_READ:
                    result.TargetEntity = "DEPARTMENT";
                    result.Operation = "READ";
                    break;
                case IntentType.DEPARTMENT_UPDATE:
                    result.TargetEntity = "DEPARTMENT";
                    result.Operation = "UPDATE";
                    break;
                case IntentType.DEPARTMENT_DELETE:
                    result.TargetEntity = "DEPARTMENT";
                    result.Operation = "DELETE";
                    break;
                case IntentType.BUDGET_READ:
                case IntentType.BUDGET_ANALYSIS:
                    result.TargetEntity = "DEPARTMENT_BUDGET";
                    result.Operation = "READ";
                    break;
                case IntentType.BUDGET_UPDATE:
                    result.TargetEntity = "DEPARTMENT_BUDGET";
                    result.Operation = "ALLOCATE";
                    break;
                case IntentType.POLICY_CREATE:
                    result.TargetEntity = "POLICY";
                    result.Operation = "CREATE";
                    break;
                case IntentType.POLICY_READ:
                    result.TargetEntity = "POLICY";
                    result.Operation = "READ";
                    break;
                case IntentType.POLICY_UPDATE:
                    result.TargetEntity = "POLICY";
                    result.Operation = "UPDATE";
                    break;
                case IntentType.POLICY_DELETE:
                    result.TargetEntity = "POLICY";
                    result.Operation = "DELETE";
                    break;
            }
        }
    }

    private static void ValidateIntentResult(ParsedIntentResult result)
    {
        if (result.ParsedIntentType == IntentType.UNKNOWN)
        {
            result.Intent = IntentType.UNKNOWN.ToString();
            result.Confidence = Math.Min(result.Confidence, 0.5);
            result.RequiresClarification = true;
            result.ClarificationPrompt = "I could not understand your request. Could you please specify what action you would like to take? For example: 'add employee Ali', 'show budget', 'show leave policy'.";
            return;
        }

        if (result.Confidence < 0.5)
        {
            result.RequiresClarification = true;
            result.ClarificationPrompt = $"I'm not fully confident about your request (classified as: {result.Intent}). Could you please clarify your goal?";
            return;
        }

        var entities = result.Entities;
        var scope = result.Scope;

        switch (result.ParsedIntentType)
        {
            case IntentType.EMPLOYEE_CREATE:
                if (!entities.ContainsKey("name") || string.IsNullOrWhiteSpace(entities["name"]))
                {
                    result.RequiresClarification = true;
                    result.ClarificationPrompt = "Please specify the full name of the employee you want to create.";
                }
                break;

            case IntentType.EMPLOYEE_ONBOARDING:
                if (!entities.ContainsKey("name") || string.IsNullOrWhiteSpace(entities["name"]))
                {
                    result.RequiresClarification = true;
                    result.ClarificationPrompt = "Please specify the candidate's full name to begin onboarding.";
                }
                break;

            case IntentType.EMPLOYEE_UPDATE:
                if (scope == "SINGLE" && (!entities.ContainsKey("name") && !entities.ContainsKey("employeeId") && !entities.ContainsKey("id")))
                {
                    result.RequiresClarification = true;
                    result.ClarificationPrompt = "Please specify which employee you would like to update.";
                }
                break;

            case IntentType.EMPLOYEE_DELETE:
                if (scope == "SINGLE" && (!entities.ContainsKey("name") && !entities.ContainsKey("employeeId") && !entities.ContainsKey("id")))
                {
                    result.RequiresClarification = true;
                    result.ClarificationPrompt = "Please specify the employee name or ID you wish to delete, or say 'delete all employees'.";
                }
                break;

            case IntentType.BUDGET_UPDATE:
                if (!entities.ContainsKey("department") && !entities.ContainsKey("budgetAmount"))
                {
                    result.RequiresClarification = true;
                    result.ClarificationPrompt = "Please specify which department's budget to update and the new amount or increase.";
                }
                break;

            case IntentType.POLICY_CREATE:
            case IntentType.POLICY_UPDATE:
            case IntentType.POLICY_DELETE:
                if (!entities.ContainsKey("policyTitle") && !entities.ContainsKey("policyCode"))
                {
                    result.RequiresClarification = true;
                    result.ClarificationPrompt = "Please specify the policy name or code (e.g. 'leave policy', 'POL-HR-001').";
                }
                break;
        }
    }

    private static ParsedIntentResult RuleBasedFallbackParse(string prompt)
    {
        var p = prompt.ToLowerInvariant();
        var result = new ParsedIntentResult();

        // Check Employee creation/onboarding verbs FIRST
        bool isEmployeeCreation = p.Contains("add employee") || p.Contains("create employee") || p.Contains("new employee") ||
                                  p.Contains("onboard") || p.Contains("onboarding") || p.Contains("hire") ||
                                  (p.Contains("employee") && (p.Contains("salary") || p.Contains("designation") || p.Contains("salary is") || p.Contains("department is"))) ||
                                  Regex.IsMatch(p, @"\badd\s+[a-z0-9\s]+as\s+(?:an?\s+)?employee\b|\bemployee\s+add\b");

        bool isEmployeeUpdate = (p.Contains("employee") || p.Contains("salary") || p.Contains("developer")) &&
                                (p.Contains("update") || p.Contains("change salary") || p.Contains("increase salary") || p.Contains("raise") || p.Contains("move him") || p.Contains("move her"));

        bool isEmployeeDelete = (p.Contains("employee") || p.Contains("staff")) &&
                                (p.Contains("delete") || p.Contains("remove") || p.Contains("fire") || p.Contains("terminate"));

        if (isEmployeeCreation)
        {
            result.Intent = p.Contains("onboard") ? IntentType.EMPLOYEE_ONBOARDING.ToString() : IntentType.EMPLOYEE_CREATE.ToString();
            result.TargetEntity = "EMPLOYEE";
            result.Operation = "CREATE";
        }
        else if (isEmployeeUpdate)
        {
            result.Intent = IntentType.EMPLOYEE_UPDATE.ToString();
            result.TargetEntity = "EMPLOYEE";
            result.Operation = "UPDATE";
        }
        else if (isEmployeeDelete)
        {
            result.Intent = IntentType.EMPLOYEE_DELETE.ToString();
            result.TargetEntity = "EMPLOYEE";
            result.Operation = "DELETE";
        }
        // Policy intents
        else if (p.Contains("policy") || p.Contains("policies") || p.Contains("pol-"))
        {
            result.TargetEntity = "POLICY";
            if (p.Contains("upload") || p.Contains("create") || p.Contains("add") || p.Contains("write") || p.Contains("new policy"))
            {
                result.Intent = IntentType.POLICY_CREATE.ToString();
                result.Operation = "CREATE";
            }
            else if (p.Contains("delete") || p.Contains("remove") || p.Contains("archive") || p.Contains("revoke"))
            {
                result.Intent = IntentType.POLICY_DELETE.ToString();
                result.Operation = "DELETE";
            }
            else if (p.Contains("update") || p.Contains("edit") || p.Contains("change") || p.Contains("modify") || p.Contains("revise"))
            {
                result.Intent = IntentType.POLICY_UPDATE.ToString();
                result.Operation = "UPDATE";
            }
            else
            {
                result.Intent = IntentType.POLICY_READ.ToString();
                result.Operation = "READ";
            }
        }
        // Budget intents
        else if (p.Contains("budget"))
        {
            result.TargetEntity = "DEPARTMENT_BUDGET";
            if (p.Contains("increase") || p.Contains("add") || p.Contains("give") || p.Contains("allocate") || p.Contains("set") || p.Contains("update"))
            {
                result.Intent = IntentType.BUDGET_UPDATE.ToString();
                result.Operation = "ALLOCATE";
            }
            else if (p.Contains("over") || p.Contains("exceeding") || p.Contains("exceed") || p.Contains("crossed") || p.Contains("analysis") || p.Contains("compare"))
            {
                result.Intent = IntentType.BUDGET_ANALYSIS.ToString();
                result.Operation = "ANALYZE";
            }
            else
            {
                result.Intent = IntentType.BUDGET_READ.ToString();
                result.Operation = "READ";
            }
        }
        // Department intents (ONLY if explicit department creation/management and NOT employee)
        else if ((p.Contains("department") || p.Contains("dept")) && !p.Contains("employee") && !p.Contains("salary"))
        {
            result.TargetEntity = "DEPARTMENT";
            if (p.Contains("add department") || p.Contains("create department") || p.Contains("new department"))
            {
                result.Intent = IntentType.DEPARTMENT_CREATE.ToString();
                result.Operation = "CREATE";
            }
            else if (p.Contains("delete department") || p.Contains("remove department"))
            {
                result.Intent = IntentType.DEPARTMENT_DELETE.ToString();
                result.Operation = "DELETE";
            }
            else if (p.Contains("update department") || p.Contains("rename department"))
            {
                result.Intent = IntentType.DEPARTMENT_UPDATE.ToString();
                result.Operation = "UPDATE";
            }
            else
            {
                result.Intent = IntentType.DEPARTMENT_READ.ToString();
                result.Operation = "READ";
            }
        }
        // Expense
        else if (p.Contains("expense") || p.Contains("claim") || p.Contains("reimburse"))
        {
            result.TargetEntity = "EXPENSE";
            if (p.Contains("comply") || p.Contains("complies") || p.Contains("policy") || p.Contains("allowed") || p.Contains("valid"))
            {
                result.Intent = IntentType.EXPENSE_COMPLIANCE.ToString();
                result.Operation = "READ";
            }
            else if (p.Contains("submit") || p.Contains("create") || p.Contains("add") || p.Contains("record"))
            {
                result.Intent = IntentType.EXPENSE_CREATE.ToString();
                result.Operation = "CREATE";
            }
            else
            {
                result.Intent = IntentType.EXPENSE_READ.ToString();
                result.Operation = "READ";
            }
        }
        // Audit
        else if (p.Contains("audit") || p.Contains("log") || p.Contains("activity") || p.Contains("history"))
        {
            result.Intent = IntentType.AUDIT_READ.ToString();
            result.TargetEntity = "AUDIT_LOG";
            result.Operation = "READ";
        }
        // Approval
        else if (p.Contains("approval") || p.Contains("pending") || p.Contains("approve") || p.Contains("reject"))
        {
            result.Intent = IntentType.APPROVAL_READ.ToString();
            result.TargetEntity = "APPROVAL";
            result.Operation = "READ";
        }
        // Dashboard
        else if (p.Contains("dashboard") || p.Contains("overview") || p.Contains("summary") || p.Contains("how many"))
        {
            result.Intent = IntentType.DASHBOARD_ANALYTICS.ToString();
            result.TargetEntity = "EMPLOYEE";
            result.Operation = "READ";
        }
        // Fallback read
        else if (p.Contains("show") || p.Contains("list") || p.Contains("get") || p.Contains("search") || p.Contains("find") || p.Contains("view"))
        {
            result.Intent = IntentType.EMPLOYEE_READ.ToString();
            result.TargetEntity = "EMPLOYEE";
            result.Operation = "READ";
        }

        result.Confidence = result.Intent == "UNKNOWN" ? 0.0 : 0.90;
        return result;
    }

    private static void EnrichIntentScopeAndEntities(string prompt, ParsedIntentResult result)
    {
        var p = prompt.ToLowerInvariant();
        var entities = result.Entities ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        result.Entities = entities;

        var parameters = result.Parameters ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        result.Parameters = parameters;

        // 1. Scope
        bool isAll = p.Contains("all") || p.Contains("every") || p.Contains("each") || p.Contains("entire") ||
                     p.Contains("everyone") || p.Contains("from the list") || p.Contains("all employees");
        bool isFiltered = p.Contains("in it") || p.Contains("in hr") || p.Contains("inactive") || p.Contains("active") ||
                          p.Contains("developers") || p.Contains("managers") || p.Contains("earning") ||
                          p.Contains("above") || p.Contains("greater than") || p.Contains("less than") ||
                          p.Contains("in finance") || p.Contains("in marketing") || p.Contains("in operations");

        if (isAll && !isFiltered)
        {
            result.Scope = "ALL";
            entities["scope"] = "ALL";
            entities.Remove("name");
            parameters.Remove("name");
        }
        else if (isFiltered)
        {
            result.Scope = "FILTERED";
            entities["scope"] = "FILTERED";
        }
        else
        {
            result.Scope = "SINGLE";
            entities["scope"] = "SINGLE";
        }

        // 2. Extract Percentage (10%, 15 percent)
        var pctMatch = Regex.Match(prompt, @"([0-9]+(?:\.[0-9]+)?)\s*%|\b([0-9]+)\s*percent\b", RegexOptions.IgnoreCase);
        if (pctMatch.Success)
        {
            var rawPct = pctMatch.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(rawPct)) rawPct = pctMatch.Groups[2].Value;
            if (decimal.TryParse(rawPct, NumberStyles.Any, CultureInfo.InvariantCulture, out var pctVal))
            {
                entities["percentage"] = pctVal.ToString(CultureInfo.InvariantCulture);
                parameters["percentage"] = pctVal;
            }
        }

        // 3. Extract Salary / Budget Amount — handles: 80k, 50k, 50,000, $80000, 80000, 1.5m
        if (!entities.ContainsKey("salary") && !entities.ContainsKey("budgetAmount") && !parameters.ContainsKey("salary"))
        {
            var shortMatch = Regex.Match(prompt, @"\b([0-9]+(?:\.[0-9]+)?)\s*(k|m)\b", RegexOptions.IgnoreCase);
            if (shortMatch.Success && decimal.TryParse(shortMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var shortVal))
            {
                var mult = shortMatch.Groups[2].Value.ToLower() == "k" ? 1000m : 1000000m;
                var amount = shortVal * mult;
                var isBudgetIntent = result.TargetEntity.Equals("DEPARTMENT_BUDGET", StringComparison.OrdinalIgnoreCase) || result.Intent.Contains("BUDGET");
                string key = isBudgetIntent ? "budgetAmount" : "salary";
                entities[key] = amount.ToString(CultureInfo.InvariantCulture);
                parameters[key] = amount;
            }
            else
            {
                var salMatch = Regex.Match(prompt, @"(?:\bsalary\s+(?:is\s+)?|\bsalary\s*=\s*|\bof\s+|\bis\s+|\bto\s+)?\$?\s*([0-9]{1,3}(?:,[0-9]{3})+|[0-9]{4,8})\b", RegexOptions.IgnoreCase);
                if (salMatch.Success)
                {
                    var rawSal = salMatch.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(rawSal, NumberStyles.Any, CultureInfo.InvariantCulture, out var salVal) && salVal >= 100)
                    {
                        var isBudgetIntent = result.TargetEntity.Equals("DEPARTMENT_BUDGET", StringComparison.OrdinalIgnoreCase) || result.Intent.Contains("BUDGET");
                        string key = isBudgetIntent ? "budgetAmount" : "salary";
                        entities[key] = salVal.ToString(CultureInfo.InvariantCulture);
                        parameters[key] = salVal;
                    }
                }
            }
        }

        // 4. Extract Department
        if (!entities.ContainsKey("department") && !parameters.ContainsKey("department"))
        {
            var deptMatch = Regex.Match(prompt, @"\bdepartment\s+(?:is\s+)?([a-zA-Z]+)\b|\b([a-zA-Z]+)\s+department\b", RegexOptions.IgnoreCase);
            if (deptMatch.Success)
            {
                var dRaw = !string.IsNullOrWhiteSpace(deptMatch.Groups[1].Value) ? deptMatch.Groups[1].Value : deptMatch.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(dRaw) && !dRaw.Equals("the", StringComparison.OrdinalIgnoreCase) && !dRaw.Equals("his", StringComparison.OrdinalIgnoreCase))
                {
                    var cleanDept = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dRaw.ToLower());
                    if (cleanDept.Equals("It", StringComparison.OrdinalIgnoreCase)) cleanDept = "IT";
                    if (cleanDept.Equals("Hr", StringComparison.OrdinalIgnoreCase)) cleanDept = "HR";
                    entities["department"] = cleanDept;
                    parameters["department"] = cleanDept;
                }
            }

            if (!entities.ContainsKey("department"))
            {
                if (Regex.IsMatch(p, @"\bit\b|\bsoftware\b|\bfrontend\b")) { entities["department"] = "IT"; parameters["department"] = "IT"; }
                else if (Regex.IsMatch(p, @"\bhr\b|\bhuman resources\b")) { entities["department"] = "HR"; parameters["department"] = "HR"; }
                else if (p.Contains("finance")) { entities["department"] = "Finance"; parameters["department"] = "Finance"; }
                else if (p.Contains("marketing")) { entities["department"] = "Marketing"; parameters["department"] = "Marketing"; }
                else if (p.Contains("operations")) { entities["department"] = "Operations"; parameters["department"] = "Operations"; }
                else if (p.Contains("legal")) { entities["department"] = "Legal"; parameters["department"] = "Legal"; }
            }
        }

        // 5. Extract Designation
        if (!entities.ContainsKey("designation") && !parameters.ContainsKey("designation"))
        {
            var desigMatch = Regex.Match(prompt, @"\bdesignation\s+(?:is\s+)?([a-zA-Z0-9\.\s\-\#\+]+?)(?:\s+and\s+|\s*$|[,.])|\bas\s+(?:an?\s+)?([a-zA-Z0-9\.\s\-\#\+]+?)(?:\s+in\s+|\s+with\s+|\s+at\s+|$)", RegexOptions.IgnoreCase);
            if (desigMatch.Success)
            {
                var rawDesig = !string.IsNullOrWhiteSpace(desigMatch.Groups[1].Value) ? desigMatch.Groups[1].Value : desigMatch.Groups[2].Value;
                rawDesig = rawDesig.Trim();
                if (!string.IsNullOrWhiteSpace(rawDesig) && rawDesig.Length < 40)
                {
                    entities["designation"] = rawDesig;
                    parameters["designation"] = rawDesig;
                }
            }
        }

        // 6. Extract Status
        if (!entities.ContainsKey("status") && !parameters.ContainsKey("status"))
        {
            if (p.Contains("inactive")) { entities["status"] = "Inactive"; parameters["status"] = "Inactive"; }
            else if (p.Contains("active") && !p.Contains("inactive")) { entities["status"] = "Active"; parameters["status"] = "Active"; }
        }

        // 7. Extract Quarter
        var quarterMatch = Regex.Match(prompt, @"\b(Q[1-4])\b", RegexOptions.IgnoreCase);
        if (quarterMatch.Success && !entities.ContainsKey("quarter"))
        {
            entities["quarter"] = quarterMatch.Groups[1].Value.ToUpper();
            parameters["quarter"] = quarterMatch.Groups[1].Value.ToUpper();
        }

        // 8. Extract Name
        if (!entities.ContainsKey("name") || string.IsNullOrWhiteSpace(entities.GetValueOrDefault("name")))
        {
            var knownNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ahmed khan", "Ahmed Khan" },
                { "sufyan khan", "Sufyan Khan" },
                { "tariq mahmood", "Tariq Mahmood" },
                { "sarah jenkins", "Sarah Jenkins" },
                { "maria garcia", "Maria Garcia" },
                { "ali", "Ali" },
                { "sara", "Sara" },
                { "ahmed", "Ahmed Khan" }
            };

            foreach (var kv in knownNames)
            {
                if (Regex.IsMatch(p, $@"\b{kv.Key}\b"))
                {
                    entities["name"] = kv.Value;
                    parameters["name"] = kv.Value;
                    break;
                }
            }

            if (!entities.ContainsKey("name") || string.IsNullOrWhiteSpace(entities.GetValueOrDefault("name")))
            {
                var namePatterns = new[]
                {
                    @"\b(?:add\s+employee|create\s+employee|onboard\s+employee|onboard|hire)\s+(?:named\s+|name\s+)?([A-Za-z]+(?:\s+[A-Za-z]+)?)\b",
                    @"\bemployee\s+([A-Za-z]+(?:\s+[A-Za-z]+)?)\b",
                    @"\bname\s+(?:is\s+)?([A-Za-z]+(?:\s+[A-Za-z]+)?)\b",
                    @"\bnamed\s+([A-Za-z]+(?:\s+[A-Za-z]+)?)\b"
                };

                var fillerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "employee", "candidate", "person", "user", "staff", "list", "item", "record",
                    "salaries", "salary", "an", "a", "the", "all", "new", "department", "it", "hr",
                    "whose", "his", "her", "their", "is", "in", "with", "and", "as"
                };

                foreach (var pattern in namePatterns)
                {
                    var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var candidate = match.Groups[1].Value.Trim();
                        var firstWord = candidate.Split(' ')[0];
                        if (!fillerWords.Contains(candidate) && !fillerWords.Contains(firstWord) && candidate.Length >= 2)
                        {
                            var cleanName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(candidate.ToLower());
                            entities["name"] = cleanName;
                            parameters["name"] = cleanName;
                            break;
                        }
                    }
                }
            }
        }

        // 9. Auto-generate email from name
        if (entities.TryGetValue("name", out var empName) && !string.IsNullOrWhiteSpace(empName)
            && (!entities.ContainsKey("email") || string.IsNullOrWhiteSpace(entities.GetValueOrDefault("email"))))
        {
            var email = $"{empName.ToLower().Replace(" ", ".")}@nexus.local";
            entities["email"] = email;
            parameters["email"] = email;
        }

        // 10. Copy all Entities to Parameters for downstream tools
        foreach (var kv in entities)
        {
            if (!parameters.ContainsKey(kv.Key))
            {
                parameters[kv.Key] = kv.Value;
            }
        }

        // 11. Populate StructuredEntities
        result.StructuredEntities = new StructuredEntities
        {
            EmployeeName = entities.GetValueOrDefault("name"),
            Department = entities.GetValueOrDefault("department"),
            Designation = entities.GetValueOrDefault("designation"),
            Status = entities.GetValueOrDefault("status"),
            Salary = entities.TryGetValue("salary", out var sStr) && decimal.TryParse(sStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sDec) ? sDec : null,
            Percentage = entities.TryGetValue("percentage", out var pStr) && decimal.TryParse(pStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var pDec) ? pDec : null,
            PolicyTitle = entities.GetValueOrDefault("policyTitle"),
            PolicyCode = entities.GetValueOrDefault("policyCode"),
            Quarter = entities.GetValueOrDefault("quarter"),
            BudgetAmount = entities.TryGetValue("budgetAmount", out var bStr) && decimal.TryParse(bStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var bDec) ? bDec : null
        };
    }
}
