using System;

namespace Nexus.Data.DTOs;

public class PolicyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ContentSummary { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreatePolicyDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "HR";
    public string ContentSummary { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdatePolicyDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "HR";
    public string ContentSummary { get; set; } = string.Empty;
    public string? DocumentPath { get; set; }
    public bool IsActive { get; set; } = true;
}
