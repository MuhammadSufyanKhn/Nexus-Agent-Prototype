using System.ComponentModel.DataAnnotations;

namespace Nexus.Data.DTOs;

public class BudgetDto
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Quarter { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public bool IsOverBudget => SpentAmount > AllocatedAmount;
    public decimal Variance => SpentAmount - AllocatedAmount;
}

public class CreateBudgetDto
{
    [Required]
    public int DepartmentId { get; set; }

    [Range(2020, 2035)]
    public int Year { get; set; } = 2026;

    [Required, StringLength(10)]
    public string Quarter { get; set; } = "Q3";

    [Range(0, 100000000)]
    public decimal AllocatedAmount { get; set; }

    [Range(0, 100000000)]
    public decimal SpentAmount { get; set; }
}

public class UpdateBudgetDto
{
    [Range(0, 100000000)]
    public decimal AllocatedAmount { get; set; }

    [Range(0, 100000000)]
    public decimal SpentAmount { get; set; }
}
