using System;
using System.ComponentModel.DataAnnotations;
using Nexus.Data.Enums;

namespace Nexus.Data.DTOs;

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public int ExperienceYears { get; set; }
    public EmployeeStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateEmployeeDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    [Required, StringLength(100)]
    public string Designation { get; set; } = string.Empty;

    [Range(0, 10000000)]
    public decimal Salary { get; set; }

    [Range(0, 50)]
    public int ExperienceYears { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
}

public class UpdateEmployeeDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int DepartmentId { get; set; }

    [Required, StringLength(100)]
    public string Designation { get; set; } = string.Empty;

    [Range(0, 10000000)]
    public decimal Salary { get; set; }

    [Range(0, 50)]
    public int ExperienceYears { get; set; }

    public EmployeeStatus Status { get; set; }
}
