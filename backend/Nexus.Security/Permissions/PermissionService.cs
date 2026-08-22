using System;
using System.Collections.Generic;

namespace Nexus.Security.Permissions;

public interface IPermissionService
{
    bool HasPermission(int? userId, string userRole, string requiredPermission);
}

public class PermissionService : IPermissionService
{
    private static readonly Dictionary<string, HashSet<string>> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "Admin", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "*", "employee.read", "employee.create", "employee.update", "employee.delete",
                "department.read", "budget.read", "expense.read", "sql.analytics",
                "policy.evaluate", "compliance.check"
            }
        },
        {
            "HRManager", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "employee.read", "employee.create", "employee.update",
                "department.read", "budget.read", "expense.read",
                "policy.evaluate", "compliance.check"
            }
        },
        {
            "Employee", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "employee.read", "department.read", "budget.read", "expense.read",
                "sql.analytics", "policy.evaluate", "compliance.check"
            }
        },
        {
            "UnauthorizedUser", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        },
        {
            "Guest", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        }
    };

    public bool HasPermission(int? userId, string userRole, string requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(userRole)) userRole = "Admin";
        if (string.IsNullOrWhiteSpace(requiredPermission)) return true;

        if (!RolePermissions.TryGetValue(userRole, out var permissions))
        {
            return false;
        }

        return permissions.Contains("*") || permissions.Contains(requiredPermission);
    }
}
