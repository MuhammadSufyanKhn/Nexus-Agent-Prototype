using System.ComponentModel.DataAnnotations;

namespace Nexus.Data.DTOs;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int EmployeeCount { get; set; }
    public decimal AllocatedBudget { get; set; }
    public decimal ActualSpent { get; set; }
    public decimal RemainingBudget { get; set; }
    public string HeadOfDepartment { get; set; } = "Sarah Jenkins";
}

public class CreateDepartmentDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}

public class UpdateDepartmentDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}
