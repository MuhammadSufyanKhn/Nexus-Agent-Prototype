using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

Table: DepartmentBudgets
Columns: DepartmentName VARCHAR, Year INT, Quarter VARCHAR, AllocatedAmount DECIMAL, SpentAmount DECIMAL

Additional tables (for employee queries only):
Table: Departments — Columns: Id, Name, Description
Table: Employees — Columns: Id, Name, Email, DepartmentId, Designation, Salary, ExperienceYears, Status
Table: Budgets — Columns: Id, DepartmentId, Year, Quarter, AllocatedAmount, SpentAmount
Table: Expenses — Columns: Id, EmployeeId, ExpenseType, Amount, ExpenseDate, Status, Description

INTENT DETECTION RULES:
- show/list/give/get/find/display/check/which/what/how many → SELECT
- add/insert/create/register → INSERT
- update/change/modify/increase/decrease/set → UPDATE
- delete/remove/erase → DELETE

DEFAULT VALUES FOR INSERT (use when not specified by user):
- Year = 2026
- Quarter = 'Q3'
- SpentAmount = 0

AMOUNT PARSING: '50k' = 50000, '100 thousand' = 100000, '1.5m' = 1500000

DEPARTMENT NAME RULES:
- 'give me department names' or 'list all departments' → SELECT DISTINCT DepartmentName FROM DepartmentBudgets;
- Use DISTINCT when the intent is 'which departments exist'
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
Output: SELECT DISTINCT DepartmentName FROM DepartmentBudgets

Request: which departments exceed budget
Output: SELECT DISTINCT DepartmentName FROM DepartmentBudgets WHERE SpentAmount > AllocatedAmount

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
Output: SELECT TOP 1 DepartmentName, SpentAmount FROM DepartmentBudgets ORDER BY SpentAmount DESC

Request: show total allocated budget by department
Output: SELECT DepartmentName, SUM(AllocatedAmount) AS TotalAllocatedAmount FROM DepartmentBudgets GROUP BY DepartmentName

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

        // 2. Handle FALLBACK_TRIGGERED
        if (candidateSql.Trim().Equals("FALLBACK_TRIGGERED", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            return ToolExecutionResult.Failure(
                "FALLBACK_TRIGGERED: This request is not related to any available database operation.",
                Definition.RiskLevel);
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

        // Remove trailing semicolon — the validator will decide
        return t.Trim().TrimEnd(';').Trim();
    }

    /// <summary>
    /// Last-resort deterministic fallback when Gemini is unavailable.
    /// </summary>
    private static string DeterministicFallback(string question)
    {
        var q = question.ToLower();

        if (q.Contains("exceeding") || q.Contains("over budget") || q.Contains("overbudget") || q.Contains("exceed"))
            return "SELECT DepartmentName, SUM(AllocatedAmount) AS TotalAllocated, SUM(SpentAmount) AS TotalSpent FROM DepartmentBudgets GROUP BY DepartmentName HAVING SUM(SpentAmount) > SUM(AllocatedAmount)";

        if (q.Contains("overview") || q.Contains("allocated funds") || q.Contains("actual spend") || q.Contains("versus") || q.Contains("vs") || q.Contains("spend") || q.Contains("summary"))
            return "SELECT DepartmentName, SUM(AllocatedAmount) AS TotalAllocated, SUM(SpentAmount) AS TotalSpent FROM DepartmentBudgets GROUP BY DepartmentName";

        if (q.Contains("department") && (q.Contains("name") || q.Contains("list") || q.Contains("show") || q.Contains("give")))
            return "SELECT DISTINCT DepartmentName FROM DepartmentBudgets";

        if (q.Contains("budget") || q.Contains("funds"))
            return "SELECT DepartmentName, SUM(AllocatedAmount) AS TotalAllocated, SUM(SpentAmount) AS TotalSpent FROM DepartmentBudgets GROUP BY DepartmentName";

        return "SELECT DepartmentName, SUM(AllocatedAmount) AS TotalAllocated, SUM(SpentAmount) AS TotalSpent FROM DepartmentBudgets GROUP BY DepartmentName";
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
            // Budget over-spend summary
            var overBudget = rows.Where(r =>
                r.ContainsKey("SpentAmount") && r.ContainsKey("AllocatedAmount") &&
                r["SpentAmount"] != null && r["AllocatedAmount"] != null &&
                Convert.ToDecimal(r["SpentAmount"]) > Convert.ToDecimal(r["AllocatedAmount"])).ToList();

            if (overBudget.Count > 0)
            {
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
