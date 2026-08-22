using System;
using System.ComponentModel.DataAnnotations;
using Nexus.Data.Enums;

namespace Nexus.Data.DTOs;

public class ExpenseDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public ExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName => ExpenseType.ToString();
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public ExpenseStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string Description { get; set; } = string.Empty;
}

public class CreateExpenseDto
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public ExpenseType ExpenseType { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;
}

public class UpdateExpenseDto
{
    [Required]
    public ExpenseType ExpenseType { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    public ExpenseStatus Status { get; set; }

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;
}
