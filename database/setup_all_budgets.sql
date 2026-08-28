-- ============================================================================
-- NEXUS AGENT LITE — Complete Budget Management SQL Setup Script
-- Run this script in SQL Server Management Studio (SSMS) against NexusAgentDb
-- ============================================================================

USE NexusAgentDb;
GO

PRINT '================================================================';
PRINT 'Starting Nexus Agent Lite Database Budget Schema & Seed Setup...';
PRINT '================================================================';

-- 1. Create MasterBudgets Table (Corporate Budget Pool)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MasterBudgets')
BEGIN
    CREATE TABLE [dbo].[MasterBudgets] (
        [Id]               INT IDENTITY(1,1) PRIMARY KEY,
        [Year]             INT NOT NULL DEFAULT 2026,
        [FiscalYear]       NVARCHAR(50) NOT NULL DEFAULT '2026-2027',
        [TotalBudgetPool]  DECIMAL(18,2) NOT NULL DEFAULT 1000000000.00,
        [UpdatedAt]        DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT '✓ Table [MasterBudgets] created successfully.';
END
ELSE
BEGIN
    PRINT '✓ Table [MasterBudgets] already exists.';
END
GO

-- Seed MasterBudgets initial $1 Billion pool if empty
IF NOT EXISTS (SELECT 1 FROM [dbo].[MasterBudgets])
BEGIN
    INSERT INTO [dbo].[MasterBudgets] ([Year], [FiscalYear], [TotalBudgetPool], [UpdatedAt])
    VALUES (2026, '2026-2027', 1000000000.00, GETUTCDATE());
    PRINT '✓ Seeded [MasterBudgets] with $1,000,000,000.00 Total Corporate Budget Pool.';
END
GO


-- 2. Create DepartmentBudgets Table (SQL Analytics & Intent Parser Query Target)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DepartmentBudgets')
BEGIN
    CREATE TABLE [dbo].[DepartmentBudgets] (
        [Id]              INT IDENTITY(1,1) PRIMARY KEY,
        [DepartmentName]  VARCHAR(100) NOT NULL,
        [Year]            INT NOT NULL DEFAULT 2026,
        [Quarter]         VARCHAR(10) NOT NULL DEFAULT 'Q3',
        [AllocatedAmount] DECIMAL(18, 2) NOT NULL DEFAULT 0,
        [SpentAmount]     DECIMAL(18, 2) NOT NULL DEFAULT 0
    );
    PRINT '✓ Table [DepartmentBudgets] created successfully.';
END
ELSE
BEGIN
    PRINT '✓ Table [DepartmentBudgets] already exists.';
END
GO

-- Seed / Update DepartmentBudgets initial data
IF NOT EXISTS (SELECT 1 FROM [dbo].[DepartmentBudgets])
BEGIN
    INSERT INTO [dbo].[DepartmentBudgets] (DepartmentName, Year, Quarter, AllocatedAmount, SpentAmount)
    VALUES
        ('IT',          2026, 'Q3', 100000.00, 25000.00),
        ('HR',          2026, 'Q3',  50000.00, 10000.00),
        ('Marketing',   2026, 'Q3',  75000.00, 30000.00),
        ('Operations',  2026, 'Q3',  80000.00, 20000.00),
        ('R&D',         2026, 'Q3', 120000.00, 40000.00);
    PRINT '✓ Seeded [DepartmentBudgets] with 5 corporate department records.';
END
GO


-- 3. Ensure Budgets Table Columns Exist (EF Core Table)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'IsFrozen')
    ALTER TABLE [dbo].[Budgets] ADD IsFrozen BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'FreezeReason')
    ALTER TABLE [dbo].[Budgets] ADD FreezeReason NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'FrozenAt')
    ALTER TABLE [dbo].[Budgets] ADD FrozenAt DATETIME2 NULL;

PRINT '✓ Verified [Budgets] column schema (IsFrozen, FreezeReason, FrozenAt).';
GO


-- 4. Sync Budgets Table with Departments Table IDs
MERGE INTO [dbo].[Budgets] AS target
USING (
    SELECT d.Id AS DepartmentId, 2026 AS [Year], 'Q3' AS Quarter,
           CASE 
             WHEN d.Name LIKE '%IT%' THEN 100000.00
             WHEN d.Name LIKE '%HR%' OR d.Name LIKE '%Human%' THEN 50000.00
             WHEN d.Name LIKE '%Marketing%' THEN 75000.00
             WHEN d.Name LIKE '%Operations%' THEN 80000.00
             ELSE 120000.00
           END AS AllocatedAmount,
           CASE 
             WHEN d.Name LIKE '%IT%' THEN 25000.00
             WHEN d.Name LIKE '%HR%' OR d.Name LIKE '%Human%' THEN 10000.00
             WHEN d.Name LIKE '%Marketing%' THEN 30000.00
             WHEN d.Name LIKE '%Operations%' THEN 20000.00
             ELSE 40000.00
           END AS SpentAmount
    FROM [dbo].[Departments] d
) AS source
ON (target.DepartmentId = source.DepartmentId AND target.[Year] = source.[Year])
WHEN MATCHED AND target.AllocatedAmount = 0 THEN
    UPDATE SET target.AllocatedAmount = source.AllocatedAmount, target.SpentAmount = source.SpentAmount
WHEN NOT MATCHED THEN
    INSERT (DepartmentId, [Year], Quarter, AllocatedAmount, SpentAmount, IsFrozen)
    VALUES (source.DepartmentId, source.[Year], source.Quarter, source.AllocatedAmount, source.SpentAmount, 0);

PRINT '✓ Synced EF Core [Budgets] table with [Departments] records.';
GO


-- 5. Verification Queries
PRINT '----------------------------------------------------------------';
PRINT 'VERIFICATION DATA PREVIEW:';
PRINT '----------------------------------------------------------------';

SELECT 'MasterBudgets' AS TableName, Year, FiscalYear, TotalBudgetPool FROM [dbo].[MasterBudgets];
SELECT 'DepartmentBudgets' AS TableName, DepartmentName, AllocatedAmount, SpentAmount FROM [dbo].[DepartmentBudgets];
SELECT 'Budgets (EF Core)' AS TableName, b.Id, d.Name AS DepartmentName, b.AllocatedAmount, b.SpentAmount, b.IsFrozen FROM [dbo].[Budgets] b LEFT JOIN [dbo].[Departments] d ON b.DepartmentId = d.Id;

PRINT '================================================================';
PRINT 'All budget tables and initial data successfully set up!';
PRINT '================================================================';
GO
