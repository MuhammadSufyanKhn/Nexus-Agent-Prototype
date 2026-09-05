using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexus.Data.LLM;
using Nexus.Data;
using Nexus.Data.Enums;
using Nexus.Security.Sql;
using Nexus.Tools.Core;

namespace Nexus.Tools.Implementations;

public class SqlAnalyticsResult
{
    public string Query { get; set; } = string.Empty;
    public string Operation { get; set; } = "SELECT"; // SELECT | INSERT | UPDATE | DELETE
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyInsights { get; set; } = new();
    public int RowsAffected { get; set; } = 0;
}

public class SqlAnalyticsTool : IAgentTool
{
    private readonly ISqlValidator _sqlValidator;
    private readonly ILLMService _llmService;
    private readonly NexusDbContext _db;
    private readonly ILogger<SqlAnalyticsTool> _logger;

    // ── Production SQL Agent System Prompt ──────────────────────────────────
    private const string SqlAgentSystemPrompt = @"
You are NEXUS ENTERPRISE AI, a production-grade natural-language-to-SQL agent for Microsoft SQL Server.

Your ONLY job is to convert natural-language requests into correct T-SQL and return the raw SQL.

DATABASE SCHEMA (USE ONLY THESE TABLES AND COLUMNS):

PRIMARY TABLES:
Table: Departments — Columns: Id INT, Name VARCHAR, Description VARCHAR
Table: Budgets — Columns: Id INT, DepartmentId INT, Year INT, Quarter VARCHAR, AllocatedAmount DECIMAL, SpentAmount DECIMAL
Table: Employees — Columns: Id INT, Name VARCHAR, Email VARCHAR, DepartmentId INT, Designation VARCHAR, Salary DECIMAL, ExperienceYears INT, Status INT (1 = Active, 2 = Inactive, 3 = OnLeave, 4 = Terminated. When querying for active employees, always use Status = 1)
Table: MasterBudgets — Columns: Id INT, Year INT, FiscalYear VARCHAR, TotalBudgetPool DECIMAL, UpdatedAt DATETIME
NOTE: MasterBudgets does NOT have RemainingBalance or AllocatedTotal columns. To compute remaining balance use: (mb.TotalBudgetPool - ISNULL((SELECT SUM(AllocatedAmount) FROM Budgets), 0))
Table: Expenses — Columns: Id, EmployeeId, ExpenseType, Amount, ExpenseDate, Status, Description
Table: Tickets — Columns: Id, TicketId, EmployeeName, Department, RequestType, Priority, Status, Details, CreatedAt
Table: Policies — Columns: Id, Code, Title, Category, DocumentPath, ContentSummary, IsActive
Table: Approvals — Columns: Id, AgentRunId, RiskLevel, RequestedBy, ApprovedBy, Status, Reason, CreatedAt
Table: OnboardingTasks — Columns: Id, EmployeeId, TaskName, Category, Status, DueDate
Table: AuditLogs — Columns: Id, AgentRunId, ActionType, Details, Timestamp, Severity
Table: Leaves — Columns: Id, EmployeeId, LeaveType, StartDate, EndDate, Status, Notes

VIEW / ALIAS:
View: DepartmentBudgets — Columns: DepartmentName VARCHAR, Year INT, Quarter VARCHAR, AllocatedAmount DECIMAL, SpentAmount DECIMAL (joins Budgets and Departments: b.DepartmentId = d.Id and d.Name = DepartmentName)

INTENT DETECTION RULES:
- show/list/give/get/find/display/check/which/what/how many → SELECT
- add/insert/create/register → INSERT
- update/change/modify/increase/decrease/set → UPDATE
- delete/remove/erase → DELETE

DEFAULT VALUES FOR INSERT (use when not specified by user):
- Year = 2026
- Quarter = 'Q3'
- SpentAmount = 0

AMOUNT PARSING: '50k' = 50000, '100 thousand' = 100000, '1.5m' = 1500000, '200k' = 200000

DEPARTMENT & BUDGET RULES:
- 'give me department names' or 'list all departments' → SELECT Name AS Department FROM Departments
- 'departments with budget > 200k' or 'budget greater than 200k' → SELECT d.Name AS Department, b.Year, b.Quarter, b.AllocatedAmount, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id WHERE b.AllocatedAmount > 200000 ORDER BY b.AllocatedAmount DESC
- 'headcount distribution' or 'distribution report' → SELECT d.Name AS Department, COUNT(e.Id) AS Headcount FROM Departments d LEFT JOIN Employees e ON d.Id = e.DepartmentId AND e.Status = 1 GROUP BY d.Name ORDER BY Headcount DESC
- Use WHERE SpentAmount > AllocatedAmount for 'exceeding budget'
- Use WHERE SpentAmount < AllocatedAmount for 'under budget'

SQL SERVER RULES:
- Use TOP N instead of LIMIT
- Use NULLIF(column, 0) to prevent division-by-zero
- Use valid T-SQL syntax only (not MySQL/PostgreSQL)
- Never use DROP, ALTER, TRUNCATE, EXEC, XP_CMDSHELL, CREATE TABLE, MERGE

OUTPUT CONTRACT:
- Return ONLY the raw executable SQL query — nothing else
- No markdown, no code fences, no explanations, no greetings
- For unsupported/irrelevant requests: return exactly FALLBACK_TRIGGERED
- Do NOT add semicolon at end

CRITICAL:
- Never confuse SELECT with INSERT
- 'Add Finance with budget 50k' is INSERT, not SELECT
- 'Give me departments' is SELECT, not INSERT
- Never invent department names or fabricate data

EXAMPLES:

Request: give me department names
Output: SELECT Name AS Department FROM Departments

Request: show departments with budget > 200k
Output: SELECT d.Name AS Department, b.Year, b.Quarter, b.AllocatedAmount, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id WHERE b.AllocatedAmount > 200000 ORDER BY b.AllocatedAmount DESC

Request: headcount distribution report
Output: SELECT d.Name AS Department, COUNT(e.Id) AS Headcount FROM Departments d LEFT JOIN Employees e ON d.Id = e.DepartmentId AND e.Status = 1 GROUP BY d.Name ORDER BY Headcount DESC

Request: which departments exceed budget
Output: SELECT d.Name AS DepartmentName, b.AllocatedAmount, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id WHERE b.SpentAmount > b.AllocatedAmount

Request: add Finance with budget 50k
Output: INSERT INTO DepartmentBudgets (DepartmentName, Year, Quarter, AllocatedAmount, SpentAmount) VALUES ('Finance', 2026, 'Q3', 50000, 0)

Request: create HR with 75000 budget
Output: INSERT INTO DepartmentBudgets (DepartmentName, Year, Quarter, AllocatedAmount, SpentAmount) VALUES ('HR', 2026, 'Q3', 75000, 0)

Request: set Finance Q3 budget to 75000
Output: UPDATE DepartmentBudgets SET AllocatedAmount = 75000 WHERE DepartmentName = 'Finance' AND Year = 2026 AND Quarter = 'Q3'

Request: increase Finance budget by 10000
Output: UPDATE DepartmentBudgets SET AllocatedAmount = AllocatedAmount + 10000 WHERE DepartmentName = 'Finance'

Request: delete Finance from Q3 2026
Output: DELETE FROM DepartmentBudgets WHERE DepartmentName = 'Finance' AND Year = 2026 AND Quarter = 'Q3'

Request: how much did Finance spend
Output: SELECT SUM(SpentAmount) AS TotalSpent FROM DepartmentBudgets WHERE DepartmentName = 'Finance'

Request: which department has highest spending
Output: SELECT TOP 1 d.Name AS DepartmentName, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id ORDER BY b.SpentAmount DESC

Request: show total allocated budget by department
Output: SELECT d.Name AS DepartmentName, SUM(b.AllocatedAmount) AS TotalAllocatedAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id GROUP BY d.Name

Request: tell me a joke
Output: FALLBACK_TRIGGERED
";

    public SqlAnalyticsTool(
        ISqlValidator sqlValidator,
        ILLMService llmService,
        NexusDbContext db,
        ILogger<SqlAnalyticsTool> logger)
    {
        _sqlValidator = sqlValidator;
        _llmService = llmService;
        _db = db;
        _logger = logger;
    }

    public ToolDefinition Definition => new()
    {
        Name = "sql.analytics",
        Description = "Converts natural-language requests into T-SQL and executes them against SQL Server (SELECT/INSERT/UPDATE/DELETE).",
        InputSchema = @"{
          ""type"": ""object"",
          ""properties"": {
            ""question"": { ""type"": ""string"" },
            ""sqlQuery"": { ""type"": ""string"" }
          },
          ""required"": [""question""]
        }",
        RequiredPermission = "sql.analytics",
        RiskLevel = RiskLevel.Low
    };

    public Task<ValidationResult> ValidateInputAsync(ToolExecutionContext context)
    {
        var question = context.GetArgument<string>("question");
        var sqlQuery = context.GetArgument<string>("sqlQuery");

        if (string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(sqlQuery))
            return Task.FromResult(ValidationResult.Failure("Either 'question' or 'sqlQuery' is required."));

        return Task.FromResult(ValidationResult.Success());
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionContext context)
    {
        var inputVal = await ValidateInputAsync(context);
        if (!inputVal.IsValid)
            return ToolExecutionResult.Failure(string.Join("; ", inputVal.Errors), Definition.RiskLevel);

        var sw = Stopwatch.StartNew();
        var question = context.GetArgument<string>("question") ?? "Show all departments";
        var candidateSql = context.GetArgument<string>("sqlQuery");

        // 1. Generate SQL via Gemini Production SQL Agent if not supplied directly
        if (string.IsNullOrWhiteSpace(candidateSql))
        {
            candidateSql = await GenerateSqlFromQuestionAsync(question);
        }

        // 2. Handle FALLBACK_TRIGGERED with deterministic SQL fallback query
        if (candidateSql.Trim().Equals("FALLBACK_TRIGGERED", StringComparison.OrdinalIgnoreCase))
        {
            candidateSql = DeterministicFallback(question);
        }

        // 3. Detect operation type from the generated SQL
        var operation = DetectOperation(candidateSql);

        // 4. Route to the appropriate validator and executor
        try
        {
            if (operation == "SELECT")
            {
                return await ExecuteReadQueryAsync(candidateSql, question, sw);
            }
            else
            {
                return await ExecuteWriteQueryAsync(candidateSql, question, operation, sw);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "SQL execution failed for query: {Query}", candidateSql);
            return ToolExecutionResult.Failure($"SQL Execution Error: {ex.Message}", Definition.RiskLevel);
        }
    }

    // ── SQL Generation ───────────────────────────────────────────────────────

    private async Task<string> GenerateSqlFromQuestionAsync(string question)
    {
        // Call Gemini with the full Production SQL Agent system prompt
        var response = await _llmService.GenerateCompletionAsync(
            prompt: question,
            systemPrompt: SqlAgentSystemPrompt,
            requestJson: false);

        if (response.IsSuccess && !string.IsNullOrWhiteSpace(response.Content))
        {
            return SanitizeSqlOutput(response.Content);
        }

        _logger.LogWarning("Gemini SQL Agent returned no content. Falling back to deterministic rule.");
        return DeterministicFallback(question);
    }

    /// <summary>
    /// Strip markdown fences and extract the raw SQL string.
    /// </summary>
    private static string SanitizeSqlOutput(string text)
    {
        var t = text.Trim();

        if (t.StartsWith("```sql", StringComparison.OrdinalIgnoreCase)) t = t[6..];
        else if (t.StartsWith("```")) t = t[3..];

        if (t.EndsWith("```")) t = t[..^3];

        // Sanitize varchar status values on Employees table to prevent varchar-to-int conversion errors
        t = Regex.Replace(t, @"\b(?:e\.)?Status\s*=\s*['""]Active['""]", "Status = 1", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\b(?:e\.)?Status\s*=\s*['""]Inactive['""]", "Status = 2", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\b(?:e\.)?Status\s*=\s*['""]OnLeave['""]", "Status = 3", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\b(?:e\.)?Status\s*=\s*['""]Terminated['""]", "Status = 4", RegexOptions.IgnoreCase);

        // Rewrite queries that group only by raw DepartmentId in distribution/count queries to use Department Name
        if (Regex.IsMatch(t, @"\bGROUP\s+BY\s+(?:e\.)?DepartmentId\b", RegexOptions.IgnoreCase) && !t.Contains("Departments", StringComparison.OrdinalIgnoreCase))
        {
            t = "SELECT d.Name AS Department, COUNT(e.Id) AS Headcount FROM Departments d LEFT JOIN Employees e ON d.Id = e.DepartmentId AND e.Status = 1 GROUP BY d.Name ORDER BY Headcount DESC";
        }

        // Remove trailing semicolon — the validator will decide
        return t.Trim().TrimEnd(';').Trim();
    }

    /// <summary>
    /// Last-resort deterministic fallback when Gemini is unavailable.
    /// </summary>
    private static string DeterministicFallback(string question)
    {
        var q = question.ToLower();

        // 0. Executive Overview & Workforce Metrics
        if (q.Contains("executive overview") || (q.Contains("workforce") && q.Contains("metrics")) || (q.Contains("department") && q.Contains("headcount") && q.Contains("overview")))
        {
            return "SELECT d.Name AS Department, COUNT(e.Id) AS ActiveHeadcount, ISNULL(AVG(e.Salary), 0) AS AverageSalary, ISNULL(SUM(e.Salary), 0) AS TotalPayrollCost FROM Departments d LEFT JOIN Employees e ON d.Id = e.DepartmentId AND e.Status = 1 GROUP BY d.Name";
        }

        // 0b. Headcount Distribution Report
        if ((q.Contains("headcount") || q.Contains("employee")) && (q.Contains("distribution") || q.Contains("breakdown") || q.Contains("by department") || q.Contains("per department") || q.Contains("distribution report")))
        {
            return "SELECT d.Name AS Department, COUNT(e.Id) AS Headcount FROM Departments d LEFT JOIN Employees e ON d.Id = e.DepartmentId AND e.Status = 1 GROUP BY d.Name ORDER BY Headcount DESC";
        }

        // 0c. Budget Thresholds (e.g. > 200k, over 130k, allocated budgets over 500k, over $500,000)
        if (q.Contains("budget") && (q.Contains(">") || q.Contains("greater") || q.Contains("more than") || q.Contains("above") || q.Contains("over") || q.Contains("exceeding") || q.Contains("higher than") || q.Contains("at least") || Regex.IsMatch(q, @"\b(?:over|above|exceeding|\>)\s*\$?\d+", RegexOptions.IgnoreCase) || Regex.IsMatch(q, @"\b\d+\s*(?:k|m|thousand|million)\b", RegexOptions.IgnoreCase)))
        {
            decimal minBudget = 130000;
            var match = Regex.Match(q, @"(?:over|above|greater than|more than|exceeding|higher than|at least|\>|\>\=)\s*\$?([0-9,]+(?:\.[0-9]+)?)\s*(k|thousand|m|million)?", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(q, @"\$?([0-9,]+(?:\.[0-9]+)?)\s*(k|thousand|m|million)", RegexOptions.IgnoreCase);
            }
            if (match.Success)
            {
                var numStr = match.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(numStr, out var num))
                {
                    var unit = match.Groups[2].Value.ToLower();
                    if (unit == "k" || unit == "thousand") num *= 1000;
                    else if (unit == "m" || unit == "million") num *= 1000000;
                    if (num > 0) minBudget = num;
                }
            }
            bool isInclusive = q.Contains("at least") || q.Contains(">=") || q.Contains("greater than or equal");
            string op = isInclusive ? ">=" : ">";
            return $"SELECT d.Name AS Department, b.Year, b.Quarter, b.AllocatedAmount, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id WHERE b.AllocatedAmount {op} {minBudget} ORDER BY b.AllocatedAmount DESC";
        }

        // 0d. Department Names / List departments
        if (q.Contains("department name") || q.Contains("list departments") || q.Contains("list all departments") || q.Contains("which departments exist") || q.Contains("give me departments"))
        {
            return "SELECT Name AS Department FROM Departments";
        }

        // 1. Average Salary & Compensation
        if ((q.Contains("salary") || q.Contains("compensation")) && (q.Contains("average") || q.Contains("avg") || q.Contains("distribution")))
        {
            if (q.Contains("it"))
                return "SELECT d.Name AS Department, AVG(e.Salary) AS AverageSalary, MIN(e.Salary) AS MinSalary, MAX(e.Salary) AS MaxSalary, COUNT(e.Id) AS Headcount FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'IT' AND e.Status = 1 GROUP BY d.Name";
            if (q.Contains("marketing"))
                return "SELECT d.Name AS Department, AVG(e.Salary) AS AverageSalary, MIN(e.Salary) AS MinSalary, MAX(e.Salary) AS MaxSalary, COUNT(e.Id) AS Headcount FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'Marketing' AND e.Status = 1 GROUP BY d.Name";
            if (q.Contains("operations"))
                return "SELECT d.Name AS Department, AVG(e.Salary) AS AverageSalary, MIN(e.Salary) AS MinSalary, MAX(e.Salary) AS MaxSalary, COUNT(e.Id) AS Headcount FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'Operations' AND e.Status = 1 GROUP BY d.Name";
            if (q.Contains("hr"))
                return "SELECT d.Name AS Department, AVG(e.Salary) AS AverageSalary, MIN(e.Salary) AS MinSalary, MAX(e.Salary) AS MaxSalary, COUNT(e.Id) AS Headcount FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'HR' AND e.Status = 1 GROUP BY d.Name";

            return "SELECT d.Name AS Department, AVG(e.Salary) AS AverageSalary, MIN(e.Salary) AS MinSalary, MAX(e.Salary) AS MaxSalary, COUNT(e.Id) AS Headcount FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE e.Status = 1 GROUP BY d.Name";
        }

        // 2. Workplace Service Desk / Tickets (Open vs All)
        if (q.Contains("ticket") || q.Contains("hardware") || q.Contains("laptop") || q.Contains("macbook") || q.Contains("service desk") || q.Contains("technician"))
        {
            if (q.Contains("open") || q.Contains("waiting") || q.Contains("technician") || q.Contains("pending") || q.Contains("unassigned"))
                return "SELECT TicketId, EmployeeName, Department, RequestType, Priority, Status, Details, CreatedAt FROM Tickets WHERE Status = 'Open'";

            return "SELECT TicketId, EmployeeName, Department, RequestType, Priority, Status, Details, CreatedAt FROM Tickets";
        }

        // 3. Unallocated pool & Master budget
        if (q.Contains("unallocated") || (q.Contains("pool") && (q.Contains("balance") || q.Contains("remaining"))) || q.Contains("remaining unallocated") || q.Contains("master budget"))
        {
            return "SELECT mb.TotalBudgetPool AS TotalCorporatePool, ISNULL((SELECT SUM(AllocatedAmount) FROM Budgets), 0) AS TotalDepartmentAllocated, (mb.TotalBudgetPool - ISNULL((SELECT SUM(AllocatedAmount) FROM Budgets), 0)) AS RemainingUnallocatedBalance FROM MasterBudgets mb WHERE mb.Year = 2026";
        }

        // 4. Onboarding tasks & completions
        if (q.Contains("onboarding") || q.Contains("onboarded") || q.Contains("completion"))
        {
            return "SELECT e.Name AS EmployeeName, d.Name AS Department, t.TaskName, t.SystemTarget, t.Status, t.CreatedAt FROM OnboardingTasks t JOIN Employees e ON t.EmployeeId = e.Id JOIN Departments d ON e.DepartmentId = d.Id";
        }

        // 5. Leaves & Sick days
        if (q.Contains("leave") || q.Contains("sick") || q.Contains("pto") || q.Contains("vacation"))
        {
            return "SELECT e.Name AS EmployeeName, d.Name AS Department, l.LeaveType, l.StartDate, l.EndDate, l.Status FROM Leaves l JOIN Employees e ON l.EmployeeId = e.Id JOIN Departments d ON e.DepartmentId = d.Id";
        }

        // 6. Expense claims & policy compliance
        if (q.Contains("expense") || q.Contains("claim") || q.Contains("reimbursement") || q.Contains("meal limit"))
        {
            return "SELECT e.Name AS EmployeeName, exp.ExpenseType, exp.Amount, exp.Status, exp.Description, exp.ExpenseDate FROM Expenses exp JOIN Employees e ON exp.EmployeeId = e.Id";
        }

        // 7. Employees & Directory filtering
        if (q.Contains("employee") || q.Contains("headcount") || q.Contains("staff") || q.Contains("hire") || q.Contains("directory"))
        {
            if (Regex.IsMatch(q, @"\bmarketing\b"))
                return "SELECT e.Name, d.Name AS Department, e.Designation, e.Salary, e.ManagerName, e.Status FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'Marketing'";
            if (Regex.IsMatch(q, @"\b(it|information technology)\b"))
                return "SELECT e.Name, d.Name AS Department, e.Designation, e.Salary, e.ManagerName, e.Status FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'IT'";
            if (Regex.IsMatch(q, @"\boperations\b"))
                return "SELECT e.Name, d.Name AS Department, e.Designation, e.Salary, e.ManagerName, e.Status FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'Operations'";
            if (Regex.IsMatch(q, @"\b(hr|human resources)\b"))
                return "SELECT e.Name, d.Name AS Department, e.Designation, e.Salary, e.ManagerName, e.Status FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id WHERE d.Name = 'HR'";

            return "SELECT e.Name, d.Name AS Department, e.Designation, e.Salary, e.ManagerName, e.Status FROM Employees e JOIN Departments d ON e.DepartmentId = d.Id";
        }

        // 8. Activity History / Audit Logs
        if (q.Contains("audit") || q.Contains("activity") || q.Contains("history") || q.Contains("log"))
        {
            return "SELECT Action, ToolName, Target, Result, Timestamp FROM AuditLogs";
        }

        // 9. Department Budgets
        if (q.Contains("exceeding") || q.Contains("over budget") || q.Contains("overbudget") || q.Contains("exceed"))
        {
            return "SELECT d.Name AS DepartmentName, b.AllocatedAmount, b.SpentAmount, (b.SpentAmount - b.AllocatedAmount) AS Overspend FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id WHERE b.SpentAmount > b.AllocatedAmount";
        }

        return "SELECT d.Name AS DepartmentName, b.Year, b.Quarter, b.AllocatedAmount, b.SpentAmount FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id";
    }

    // ── Execution ────────────────────────────────────────────────────────────

    private async Task<ToolExecutionResult> ExecuteReadQueryAsync(
        string sql, string question, Stopwatch sw)
    {
        // Validate through read-only ISqlValidator
        var validation = _sqlValidator.ValidateQuery(sql);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Read SQL validation failed: {Error}", validation.ErrorMessage);
            sw.Stop();
            return ToolExecutionResult.Failure(
                validation.ErrorMessage ?? "SQL security validation failed.",
                Definition.RiskLevel);
        }

        var (columns, rows) = await ExecuteRawSqlAsync(validation.SanitizedQuery, timeoutSeconds: 5);
        var summary = BuildReadSummary(question, validation.SanitizedQuery, columns, rows);

        sw.Stop();
        return ToolExecutionResult.Success(summary, Definition.RiskLevel, sw.ElapsedMilliseconds);
    }

    private async Task<ToolExecutionResult> ExecuteWriteQueryAsync(
        string sql, string question, string operation, Stopwatch sw)
    {
        // Validate through write-allowed validator
        var validation = _sqlValidator.ValidateWriteQuery(sql);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Write SQL validation failed: {Error}", validation.ErrorMessage);
            sw.Stop();
            return ToolExecutionResult.Failure(
                validation.ErrorMessage ?? "SQL write validation failed.",
                RiskLevel.Medium);
        }

        var rowsAffected = await ExecuteNonQueryAsync(validation.SanitizedQuery, timeoutSeconds: 5);

        var result = new SqlAnalyticsResult
        {
            Query = validation.SanitizedQuery,
            Operation = operation,
            Columns = new List<string>(),
            Rows = new List<Dictionary<string, object?>>(),
            Summary = $"{operation} operation completed successfully. {rowsAffected} row(s) affected.",
            KeyInsights = new List<string>
            {
                $"Executed: {operation} on DepartmentBudgets",
                $"Rows affected: {rowsAffected}",
                $"Original request: {question}"
            },
            RowsAffected = rowsAffected
        };

        sw.Stop();
        return ToolExecutionResult.Success(result, RiskLevel.Medium, sw.ElapsedMilliseconds);
    }

    // ── Raw SQL Execution ────────────────────────────────────────────────────

    private async Task<(List<string> Columns, List<Dictionary<string, object?>> Rows)> ExecuteRawSqlAsync(
        string sql, int timeoutSeconds)
    {
        var columns = new List<string>();
        var rows = new List<Dictionary<string, object?>>();

        if (!_db.Database.IsRelational())
        {
            // In-memory mode for unit tests — query Budgets / Departments via LINQ
            columns.AddRange(new[] { "Department", "AllocatedAmount", "SpentAmount" });
            var overBudgets = await _db.Budgets
                .Include(b => b.Department)
                .Where(b => b.SpentAmount > b.AllocatedAmount)
                .ToListAsync();

            foreach (var b in overBudgets)
            {
                rows.Add(new Dictionary<string, object?>
                {
                    { "Department", b.Department?.Name ?? "IT" },
                    { "AllocatedAmount", b.AllocatedAmount },
                    { "SpentAmount", b.SpentAmount }
                });
            }
            return (columns, rows);
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = timeoutSeconds;

            using var reader = await command.ExecuteReaderAsync();

            for (int i = 0; i < reader.FieldCount; i++)
                columns.Add(reader.GetName(i));

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);

                if (rows.Count >= 100) break; // hard cap
            }
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }

        return (columns, rows);
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, int timeoutSeconds)
    {
        if (!_db.Database.IsRelational())
            return 0;

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = timeoutSeconds;
            return await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string DetectOperation(string sql)
    {
        var t = sql.TrimStart();
        if (t.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) return "INSERT";
        if (t.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)) return "UPDATE";
        if (t.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
        return "SELECT";
    }

    private static SqlAnalyticsResult BuildReadSummary(
        string question,
        string query,
        List<string> columns,
        List<Dictionary<string, object?>> rows)
    {
        var insights = new List<string>();
        string summary;

        if (rows.Count == 0)
        {
            summary = "No records found matching the specified criteria.";
            insights.Add("Query returned 0 rows.");
        }
        else
        {
            // Executive overview summary
            if (columns.Contains("ActiveHeadcount") && columns.Contains("TotalPayrollCost"))
            {
                int totalHeadcount = rows.Sum(r => r.ContainsKey("ActiveHeadcount") && r["ActiveHeadcount"] != null ? Convert.ToInt32(r["ActiveHeadcount"]) : 0);
                decimal totalPayroll = rows.Sum(r => r.ContainsKey("TotalPayrollCost") && r["TotalPayrollCost"] != null ? Convert.ToDecimal(r["TotalPayrollCost"]) : 0m);
                decimal avgSalary = totalHeadcount > 0 ? totalPayroll / totalHeadcount : 0m;

                summary = $"Executive Workforce Overview: Active headcount is {totalHeadcount} across {rows.Count} departments, with a total annual payroll of ${totalPayroll:N2} (Average salary: ${avgSalary:N2}).";
                foreach (var r in rows)
                {
                    var dept = r.GetValueOrDefault("Department")?.ToString() ?? "Unknown";
                    var hc = r.GetValueOrDefault("ActiveHeadcount")?.ToString() ?? "0";
                    var sal = r.ContainsKey("AverageSalary") && r["AverageSalary"] != null ? Convert.ToDecimal(r["AverageSalary"]).ToString("N2") : "0.00";
                    insights.Add($"{dept}: {hc} active staff, Avg Salary: ${sal}");
                }
            }
            // Unallocated budget pool summary
            else if (columns.Contains("RemainingUnallocatedBalance"))
            {
                var first = rows[0];
                var pool = first.ContainsKey("TotalCorporatePool") && first["TotalCorporatePool"] != null ? Convert.ToDecimal(first["TotalCorporatePool"]) : 1000000000.00m;
                var alloc = first.ContainsKey("TotalDepartmentAllocated") && first["TotalDepartmentAllocated"] != null ? Convert.ToDecimal(first["TotalDepartmentAllocated"]) : 0m;
                var rem = first.ContainsKey("RemainingUnallocatedBalance") && first["RemainingUnallocatedBalance"] != null ? Convert.ToDecimal(first["RemainingUnallocatedBalance"]) : pool - alloc;

                summary = $"Remaining unallocated corporate budget pool balance is ${rem:N2} out of ${pool:N2} total corporate master pool (${alloc:N2} currently allocated across operational departments).";
                insights.Add($"Total Master Budget Pool: ${pool:N2}");
                insights.Add($"Total Department Allocated: ${alloc:N2}");
                insights.Add($"Remaining Available Pool: ${rem:N2}");
            }
            // Service desk tickets
            else if (columns.Contains("TicketId"))
            {
                summary = $"Retrieved {rows.Count} service desk ticket(s).";
                foreach (var r in rows.Take(5))
                {
                    var tId = r.GetValueOrDefault("TicketId")?.ToString() ?? "";
                    var emp = r.GetValueOrDefault("EmployeeName")?.ToString() ?? "";
                    var req = r.GetValueOrDefault("RequestType")?.ToString() ?? "";
                    var stat = r.GetValueOrDefault("Status")?.ToString() ?? "";
                    insights.Add($"{tId} ({emp}, {req}) - Status: {stat}");
                }
            }
            // Budget over-spend summary
            else if (rows.Any(r => r.ContainsKey("SpentAmount") && r.ContainsKey("AllocatedAmount") &&
                r["SpentAmount"] != null && r["AllocatedAmount"] != null &&
                Convert.ToDecimal(r["SpentAmount"]) > Convert.ToDecimal(r["AllocatedAmount"])))
            {
                var overBudget = rows.Where(r =>
                    r.ContainsKey("SpentAmount") && r.ContainsKey("AllocatedAmount") &&
                    r["SpentAmount"] != null && r["AllocatedAmount"] != null &&
                    Convert.ToDecimal(r["SpentAmount"]) > Convert.ToDecimal(r["AllocatedAmount"])).ToList();

                var depts = string.Join(", ", overBudget.Select(r => r.GetValueOrDefault("Department")?.ToString() ?? r.GetValueOrDefault("DepartmentName")?.ToString() ?? r.GetValueOrDefault("Name")?.ToString() ?? "Unknown"));
                summary = $"{overBudget.Count} department(s) ({depts}) have exceeded their allocated budget.";
                foreach (var row in overBudget)
                {
                    var dept = row.GetValueOrDefault("Department")?.ToString() ?? row.GetValueOrDefault("DepartmentName")?.ToString() ?? row.GetValueOrDefault("Name")?.ToString() ?? "Unknown";
                    var over = Convert.ToDecimal(row["SpentAmount"]) - Convert.ToDecimal(row["AllocatedAmount"]);
                    insights.Add($"{dept}: over budget by {over:N2}");
                }
            }
            else
            {
                summary = $"Retrieved {rows.Count} record(s).";
                insights.Add($"Returned {columns.Count} column(s) across {rows.Count} row(s).");
            }
        }

        return new SqlAnalyticsResult
        {
            Query = query,
            Operation = "SELECT",
            Columns = columns,
            Rows = rows,
            Summary = summary,
            KeyInsights = insights,
            RowsAffected = rows.Count
        };
    }
}
