using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nexus.Security.Sql;

public class SqlValidationResult
{
    public bool IsValid { get; set; }
    public string SanitizedQuery { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<string> DetectedTables { get; set; } = new();

    public static SqlValidationResult Success(string sanitizedQuery, List<string> tables)
    {
        return new SqlValidationResult
        {
            IsValid = true,
            SanitizedQuery = sanitizedQuery,
            DetectedTables = tables
        };
    }

    public static SqlValidationResult Failure(string errorMessage)
    {
        return new SqlValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}

public interface ISqlValidator
{
    SqlValidationResult ValidateQuery(string sqlQuery);
}

public class SqlValidator : ISqlValidator
{
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Departments",
        "Employees",
        "Budgets",
        "Expenses",
        "Policies",
        "Users",
        "AgentRuns",
        "AgentActions",
        "Approvals",
        "AuditLogs",
        "OnboardingTasks"
    };

    private static readonly string[] ForbiddenKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "XP_CMDSHELL", "SP_", "CREATE", "MERGE",
        "GRANT", "REVOKE", "SHUTDOWN", "DBCC"
    };

    public SqlValidationResult ValidateQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            return SqlValidationResult.Failure("SQL query cannot be empty.");
        }

        var trimmed = sqlQuery.Trim();

        // 1. Must start with SELECT or WITH
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return SqlValidationResult.Failure("Security Violation: Only SELECT read-only analytics queries are permitted.");
        }

        // 2. Reject multi-statement semicolons and comments
        if (trimmed.Contains(';'))
        {
            // Semicolons allowed only at the very end
            var semicolonIndex = trimmed.IndexOf(';');
            if (semicolonIndex < trimmed.Length - 1)
            {
                return SqlValidationResult.Failure("Security Violation: Multiple SQL statements separated by semicolons are strictly forbidden.");
            }
            trimmed = trimmed.Substring(0, semicolonIndex).Trim();
        }

        if (trimmed.Contains("--") || trimmed.Contains("/*") || trimmed.Contains("*/"))
        {
            return SqlValidationResult.Failure("Security Violation: SQL comments (-- or /* */) are forbidden in dynamically generated queries.");
        }

        // 3. Scan for forbidden destructive keywords
        foreach (var kw in ForbiddenKeywords)
        {
            // Regex match with word boundaries to avoid false positives (e.g. "UPDATED_AT")
            var pattern = $@"\b{Regex.Escape(kw)}\b";
            if (Regex.IsMatch(trimmed, pattern, RegexOptions.IgnoreCase))
            {
                return SqlValidationResult.Failure($"Security Violation: Forbidden keyword '{kw}' detected. Destructive or administrative SQL commands are forbidden.");
            }
        }

        // 4. Extract and check table names against Whitelist
        var detectedTables = ExtractTableNames(trimmed);
        foreach (var table in detectedTables)
        {
            if (!AllowedTables.Contains(table))
            {
                return SqlValidationResult.Failure($"Security Violation: Table '{table}' is not in the allowed schema whitelist. Access denied.");
            }
        }

        // 5. Enforce Row Limit Cap (TOP 100)
        var finalQuery = EnforceRowLimitCap(trimmed);

        return SqlValidationResult.Success(finalQuery, detectedTables);
    }

    private static List<string> ExtractTableNames(string sql)
    {
        var tables = new List<string>();

        // Match FROM <table_name> and JOIN <table_name>
        var regex = new Regex(@"\b(FROM|JOIN)\s+([a-zA-Z0-9_]+|\[[a-zA-Z0-9_]+\]|dbo\.[a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
        var matches = regex.Matches(sql);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var tableName = match.Groups[2].Value
                    .Replace("[", "")
                    .Replace("]", "")
                    .Replace("dbo.", "")
                    .Trim();

                if (!string.IsNullOrWhiteSpace(tableName) && !tables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                {
                    tables.Add(tableName);
                }
            }
        }

        return tables;
    }

    private static string EnforceRowLimitCap(string sql)
    {
        if (sql.Contains("TOP ", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("FETCH NEXT", StringComparison.OrdinalIgnoreCase))
        {
            return sql;
        }

        // Inject TOP (100) right after SELECT
        if (sql.StartsWith("SELECT DISTINCT ", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT DISTINCT TOP (100) " + sql.Substring(16);
        }

        if (sql.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT TOP (100) " + sql.Substring(7);
        }

        return sql;
    }
}
