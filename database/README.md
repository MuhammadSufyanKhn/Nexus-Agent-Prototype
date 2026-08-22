# Database Schema & Seed Data

This directory contains SQL Server schema definitions, initial seed data, and EF Core migration scripts for NEXUS-AGENT LITE.

## Database Engine
- SQL Server (LocalDB / Express / Developer Edition)
- ORM: Entity Framework Core (.NET 9)

## Main Entities
- `Employees` (ID, Name, Email, DepartmentId, Role, BaseSalary, HireDate, Status)
- `Departments` (ID, Name, Q3Budget, AllocatedSalaryBudget)
- `Expenses` (ID, EmployeeId, Amount, Category, Description, Status, CreatedAt)
- `AuditLogs` (ID, Timestamp, UserIntent, RiskLevel, ActionPlan, ApprovedBy, Status, ToolExecutionDetails)
