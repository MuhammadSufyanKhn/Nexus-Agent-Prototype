using System;
using Nexus.Data.Enums;

namespace Nexus.Tools.Core;

public class ToolExecutionResult
{
    public bool IsSuccess { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
    public long ExecutionTimeMs { get; set; }

    public static ToolExecutionResult Success(object? data, RiskLevel riskLevel = RiskLevel.Low, long executionTimeMs = 0)
    {
        return new ToolExecutionResult
        {
            IsSuccess = true,
            Data = data,
            RiskLevel = riskLevel,
            ExecutionTimeMs = executionTimeMs
        };
    }

    public static ToolExecutionResult Failure(string errorMessage, RiskLevel riskLevel = RiskLevel.Low)
    {
        return new ToolExecutionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            RiskLevel = riskLevel
        };
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string error) => new() { IsValid = false, Errors = new List<string> { error } };
    public static ValidationResult Failure(List<string> errors) => new() { IsValid = false, Errors = errors };
}
