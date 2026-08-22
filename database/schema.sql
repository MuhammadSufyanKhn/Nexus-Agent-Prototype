-- =================================================================
-- NEXUS-AGENT LITE — Database Schema (SQL Server / T-SQL DDL)
-- =================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'NexusAgentDb')
BEGIN
    CREATE DATABASE [NexusAgentDb];
END
GO

USE [NexusAgentDb];
GO

-- 1. Users Table
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(150) NOT NULL UNIQUE,
        [Role] NVARCHAR(50) NOT NULL DEFAULT 'User',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 2. Departments Table
IF OBJECT_ID('dbo.Departments', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Departments] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL
    );
END
GO

-- 3. Employees Table
IF OBJECT_ID('dbo.Employees', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Employees] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(150) NOT NULL,
        [DepartmentId] INT NOT NULL,
        [Designation] NVARCHAR(100) NOT NULL,
        [Salary] DECIMAL(18, 2) NOT NULL,
        [ExperienceYears] INT NOT NULL,
        [Status] INT NOT NULL DEFAULT 1, -- 1=Active, 2=Onboarding, 3=Inactive, 4=Terminated
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_Employees_DepartmentId] ON [dbo].[Employees]([DepartmentId]);
END
GO

-- 4. Budgets Table
IF OBJECT_ID('dbo.Budgets', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Budgets] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [DepartmentId] INT NOT NULL,
        [Year] INT NOT NULL,
        [Quarter] NVARCHAR(10) NOT NULL,
        [AllocatedAmount] DECIMAL(18, 2) NOT NULL,
        [SpentAmount] DECIMAL(18, 2) NOT NULL,
        CONSTRAINT [FK_Budgets_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Budgets_Department_Quarter] ON [dbo].[Budgets]([DepartmentId], [Year], [Quarter]);
END
GO

-- 5. Expenses Table
IF OBJECT_ID('dbo.Expenses', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Expenses] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EmployeeId] INT NOT NULL,
        [ExpenseType] INT NOT NULL, -- 1=Travel, 2=Meal, 3=Equipment, 4=Software, 5=Training, 6=Other
        [Amount] DECIMAL(18, 2) NOT NULL,
        [ExpenseDate] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [Status] INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Approved, 3=Rejected, 4=Compliant, 5=NonCompliant
        [Description] NVARCHAR(500) NOT NULL,
        CONSTRAINT [FK_Expenses_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Expenses_EmployeeId] ON [dbo].[Expenses]([EmployeeId]);
END
GO

-- 6. Policies Table
IF OBJECT_ID('dbo.Policies', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Policies] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Title] NVARCHAR(200) NOT NULL,
        [Category] NVARCHAR(100) NOT NULL,
        [DocumentPath] NVARCHAR(300) NOT NULL,
        [ContentSummary] NVARCHAR(MAX) NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX [IX_Policies_Category] ON [dbo].[Policies]([Category]);
END
GO

-- 7. AgentRuns Table
IF OBJECT_ID('dbo.AgentRuns', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AgentRuns] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [UserId] INT NULL,
        [OriginalPrompt] NVARCHAR(MAX) NOT NULL,
        [Intent] NVARCHAR(150) NOT NULL,
        [Status] INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Planning, 3=WaitingForApproval, 4=Executing, 5=Completed, 6=Failed
        [StartedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CompletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_AgentRuns_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
    );
END
GO

-- 8. AgentActions Table
IF OBJECT_ID('dbo.AgentActions', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AgentActions] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [AgentRunId] UNIQUEIDENTIFIER NOT NULL,
        [ToolName] NVARCHAR(100) NOT NULL,
        [ActionType] NVARCHAR(100) NOT NULL,
        [ArgumentsJson] NVARCHAR(MAX) NOT NULL,
        [ResultJson] NVARCHAR(MAX) NULL,
        [Status] INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Executing, 3=Completed, 4=Failed
        [RiskLevel] INT NOT NULL DEFAULT 1, -- 1=Low, 2=Medium, 3=High, 4=Critical
        [StartedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CompletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_AgentActions_AgentRuns_AgentRunId] FOREIGN KEY ([AgentRunId]) REFERENCES [dbo].[AgentRuns]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AgentActions_AgentRunId] ON [dbo].[AgentActions]([AgentRunId]);
END
GO

-- 9. Approvals Table
IF OBJECT_ID('dbo.Approvals', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Approvals] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [AgentRunId] UNIQUEIDENTIFIER NOT NULL,
        [ActionId] UNIQUEIDENTIFIER NULL,
        [RiskLevel] INT NOT NULL DEFAULT 3,
        [RequestedBy] NVARCHAR(100) NOT NULL,
        [ApprovedBy] NVARCHAR(100) NULL,
        [Status] INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Approved, 3=Rejected
        [Reason] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [ApprovedAt] DATETIME2 NULL,
        CONSTRAINT [FK_Approvals_AgentRuns_AgentRunId] FOREIGN KEY ([AgentRunId]) REFERENCES [dbo].[AgentRuns]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Approvals_AgentActions_ActionId] FOREIGN KEY ([ActionId]) REFERENCES [dbo].[AgentActions]([Id]) ON DELETE NO ACTION
    );
END
GO

-- 10. AuditLogs Table
IF OBJECT_ID('dbo.AuditLogs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AgentRunId] UNIQUEIDENTIFIER NULL,
        [UserId] INT NULL,
        [Action] NVARCHAR(150) NOT NULL,
        [ToolName] NVARCHAR(100) NOT NULL,
        [Target] NVARCHAR(200) NOT NULL,
        [Result] NVARCHAR(MAX) NOT NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [PreviousHash] NVARCHAR(128) NOT NULL DEFAULT 'GENESIS',
        [CurrentHash] NVARCHAR(128) NOT NULL,
        CONSTRAINT [FK_AuditLogs_AgentRuns_AgentRunId] FOREIGN KEY ([AgentRunId]) REFERENCES [dbo].[AgentRuns]([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
    );
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [dbo].[AuditLogs]([Timestamp]);
END
GO

-- 11. OnboardingTasks Table
IF OBJECT_ID('dbo.OnboardingTasks', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OnboardingTasks] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [EmployeeId] INT NOT NULL,
        [AgentRunId] UNIQUEIDENTIFIER NULL,
        [TaskName] NVARCHAR(150) NOT NULL,
        [SystemTarget] NVARCHAR(100) NOT NULL,
        [Status] INT NOT NULL DEFAULT 1, -- 1=Pending, 2=InProgress, 3=Completed, 4=Failed
        [DetailsJson] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [CompletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_OnboardingTasks_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OnboardingTasks_AgentRuns_AgentRunId] FOREIGN KEY ([AgentRunId]) REFERENCES [dbo].[AgentRuns]([Id]) ON DELETE SET NULL
    );
END
GO
