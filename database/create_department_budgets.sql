-- ============================================================
-- NEXUS AGENT LITE — DepartmentBudgets Table
-- Production SQL Agent standalone table
-- Run this once against (localdb)\MSSQLLocalDB — NexusAgentDb
-- ============================================================

USE NexusAgentDb;
GO

-- Create the DepartmentBudgets table if it doesn't exist
IF NOT EXISTS (
    SELECT * FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentBudgets]')
    AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[DepartmentBudgets]
    (
        [DepartmentName]  VARCHAR(100)    NOT NULL,
        [Year]            INT             NOT NULL,
        [Quarter]         VARCHAR(10)     NOT NULL,
        [AllocatedAmount] DECIMAL(18, 2)  NOT NULL DEFAULT 0,
        [SpentAmount]     DECIMAL(18, 2)  NOT NULL DEFAULT 0
    );

    PRINT 'DepartmentBudgets table created successfully.';
END
ELSE
BEGIN
    PRINT 'DepartmentBudgets table already exists. Skipping creation.';
END
GO

-- Seed demo data (matches the existing Budgets/Departments seed data for consistency)
IF NOT EXISTS (SELECT 1 FROM [dbo].[DepartmentBudgets])
BEGIN
    INSERT INTO [dbo].[DepartmentBudgets] (DepartmentName, Year, Quarter, AllocatedAmount, SpentAmount)
    VALUES
        ('IT',         2026, 'Q3', 50000.00, 58500.00),  -- Exceeds budget (demo)
        ('HR',         2026, 'Q3', 30000.00, 22000.00),  -- Under budget
        ('Marketing',  2026, 'Q3', 45000.00, 41200.00),  -- Under budget
        ('Operations', 2026, 'Q3', 60000.00, 51000.00);  -- Under budget

    PRINT 'DepartmentBudgets seeded with 4 demo records.';
END
ELSE
BEGIN
    PRINT 'DepartmentBudgets already has data. Skipping seed.';
END
GO
