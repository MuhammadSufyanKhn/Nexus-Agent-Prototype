# Database Directory & Schema Guide

This directory contains the SQL Server schema scripts, migration definitions, and initial seed data for **NEXUS-AGENT LITE**.

---

## 📁 Directory File Structure

```
database/
├── README.md                      # Database documentation guide
├── schema.sql                     # Complete SQL Server T-SQL table creation script
├── seed.sql                       # Initial seed data script with valid employee email addresses
└── migrations/
    └── sql_schema_updates.sql     # Database schema update and migration scripts
```

---

## 🗄️ Database Tables Overview

| Table Name | Description | Key Fields |
| :--- | :--- | :--- |
| `Employees` | Workforce records and designations | `Id`, `Name`, `Email`, `DepartmentId`, `Designation`, `Salary`, `Status` |
| `Departments` | Corporate departments | `Id`, `Name`, `Description` |
| `Budgets` | Quarterly budget allocations and spent totals | `Id`, `DepartmentId`, `Year`, `Quarter`, `AllocatedAmount`, `SpentAmount`, `IsFrozen` |
| `Leaves` | Employee sick leave and PTO logs | `Id`, `EmployeeId`, `LeaveType`, `StartDate`, `EndDate`, `Status`, `Notes` |
| `Expenses` | Expense reimbursement claims | `Id`, `EmployeeId`, `ExpenseType`, `Amount`, `Status`, `Description` |
| `MasterBudgets` | Corporate master pool budget | `Id`, `Year`, `FiscalYear`, `TotalBudgetPool`, `UpdatedAt` |
| `Tickets` | Automated IT ticketing records | `Id`, `TicketId`, `EmployeeName`, `Department`, `RequestType`, `Priority`, `Status` |
| `AuditLogs` | Tamper-proof SHA-256 audit ledger | `Id`, `RunId`, `ToolName`, `Action`, `Result`, `CurrentHash`, `PreviousHash` |

---

## 🚀 Execution Instructions

### Running Schema & Seed Scripts Manually in SQL Server Management Studio (SSMS) or SQLCMD:

```sql
-- 1. Create tables
:r database/schema.sql

-- 2. Populate initial seed data
:r database/seed.sql
```

> **Automatic DB Initialization**: Note that when running the backend API (`dotnet run`), ASP.NET Core automatically applies EF Core migrations and initial SQL schema updates on startup!
