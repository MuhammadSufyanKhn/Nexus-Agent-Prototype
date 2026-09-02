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
    /// <summary>Validates a read-only SELECT query.</summary>
    SqlValidationResult ValidateQuery(string sqlQuery);

    /// <summary>
    /// Validates a write query (INSERT / UPDATE / DELETE) against the allowed-tables
    /// whitelist and forbidden-keyword blocklist. Does NOT enforce SELECT-only.
    /// </summary>
    SqlValidationResult ValidateWriteQuery(string sqlQuery);
}

public class SqlValidator : ISqlValidator
{
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core application tables
        "Departments",
        "Employees",
        "Budgets",
        "MasterBudgets",
        "Expenses",
        "Policies",
        "Users",
        "AgentRuns",
        "AgentActions",
        "Approvals",
        "AuditLogs",
        "OnboardingTasks",
        "Tickets",
        "JobOpenings",
        "CandidateApplications",
        "Leaves",
        "Documents",
        // Production SQL Agent table
        "DepartmentBudgets"
    };

    /// <summary>Keywords always forbidden in any query (read or write).</summary>
    private static readonly string[] ForbiddenKeywords = new[]
    {
        "DROP", "ALTER", "TRUNCATE", "EXEC", "EXECUTE",
        "XP_CMDSHELL", "SP_", "CREATE", "MERGE",
        "GRANT", "REVOKE", "SHUTDOWN", "DBCC"
    };

    /// <summary>Keywords forbidden specifically in read-only (SELECT) queries.</summary>
    private static readonly string[] ReadOnlyForbiddenKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE"
    };

    public SqlValidationResult ValidateQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return SqlValidationResult.Failure("SQL query cannot be empty.");

        var trimmed = sqlQuery.Trim();

        // 1. Must start with SELECT or WITH
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return SqlValidationResult.Failure("Security Violation: Only SELECT read-only analytics queries are permitted.");
        }

        // 2. Reject multi-statement semicolons and comments
        trimmed = StripTrailingSemicolon(trimmed, out var hasMulti);
        if (hasMulti)
            return SqlValidationResult.Failure("Security Violation: Multiple SQL statements separated by semicolons are forbidden.");

        if (trimmed.Contains("--") || trimmed.Contains("/*") || trimmed.Contains("*/"))
            return SqlValidationResult.Failure("Security Violation: SQL comments are forbidden.");

        // 3. Destructive keyword check (global + read-only specific)
        foreach (var kw in ForbiddenKeywords.Concat(ReadOnlyForbiddenKeywords))
        {
            if (Regex.IsMatch(trimmed, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
                return SqlValidationResult.Failure($"Security Violation: Forbidden keyword '{kw}' detected.");
        }

        // 4. Table whitelist check
        var detectedTables = ExtractTableNames(trimmed);
        foreach (var table in detectedTables)
        {
            if (!AllowedTables.Contains(table))
                return SqlValidationResult.Failure($"Security Violation: Table '{table}' is not in the allowed schema whitelist.");
        }

        // 5. Enforce row limit cap (TOP 100)
        return SqlValidationResult.Success(EnforceRowLimitCap(trimmed), detectedTables);
    }

    public SqlValidationResult ValidateWriteQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return SqlValidationResult.Failure("SQL write query cannot be empty.");

        var trimmed = sqlQuery.Trim();

        // 1. Must start with INSERT, UPDATE, or DELETE
        bool isWrite =
            trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase);

        if (!isWrite)
            return SqlValidationResult.Failure("Write validator expects INSERT, UPDATE, or DELETE.");

        // 2. Strip trailing semicolon / reject multi-statement
        trimmed = StripTrailingSemicolon(trimmed, out var hasMulti);
        if (hasMulti)
            return SqlValidationResult.Failure("Security Violation: Multiple SQL statements are forbidden.");

        if (trimmed.Contains("--") || trimmed.Contains("/*") || trimmed.Contains("*/"))
            return SqlValidationResult.Failure("Security Violation: SQL comments are forbidden.");

        // 3. Global forbidden keyword check (DROP, ALTER, TRUNCATE, EXEC, etc.)
        foreach (var kw in ForbiddenKeywords)
        {
            if (Regex.IsMatch(trimmed, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
                return SqlValidationResult.Failure($"Security Violation: Forbidden keyword '{kw}' detected.");
        }

        // 4. Extract referenced table (INSERT INTO / UPDATE / DELETE FROM)
        var tables = ExtractWriteTableNames(trimmed);
        foreach (var table in tables)
        {
            if (!AllowedTables.Contains(table))
                return SqlValidationResult.Failure($"Security Violation: Table '{table}' is not in the allowed schema whitelist.");
        }

        return SqlValidationResult.Success(trimmed, tables);
    }

    private static string StripTrailingSemicolon(string sql, out bool hasMultiStatement)
    {
        hasMultiStatement = false;
        if (!sql.Contains(';')) return sql;
        var idx = sql.IndexOf(';');
        if (idx < sql.Length - 1) { hasMultiStatement = true; return sql; }
        return sql[..idx].Trim();
    }

    private static List<string> ExtractTableNames(string sql)
    {
        var tables = new List<string>();
        // Match FROM <table_name> and JOIN <table_name>
        var regex = new Regex(@"\b(FROM|JOIN)\s+([a-zA-Z0-9_]+|\[[a-zA-Z0-9_]+\]|dbo\.[a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(sql))
        {
            if (match.Groups.Count >= 3)
            {
                var tableName = match.Groups[2].Value
                    .Replace("[", "").Replace("]", "").Replace("dbo.", "").Trim();
                if (!string.IsNullOrWhiteSpace(tableName) && !tables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                    tables.Add(tableName);
            }
        }
        return tables;
    }

    /// <summary>Extracts table names from INSERT INTO / UPDATE / DELETE FROM statements.</summary>
    private static List<string> ExtractWriteTableNames(string sql)
    {
        var tables = new List<string>();
        var regex = new Regex(
            @"\b(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM)\s+([a-zA-Z0-9_]+|\[[a-zA-Z0-9_]+\])",
            RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(sql))
        {
            if (match.Groups.Count >= 2)
            {
                var tableName = match.Groups[1].Value
                    .Replace("[", "").Replace("]", "").Trim();
                if (!string.IsNullOrWhiteSpace(tableName) && !tables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
                    tables.Add(tableName);
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
