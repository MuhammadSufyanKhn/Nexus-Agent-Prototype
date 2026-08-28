using Microsoft.EntityFrameworkCore;
using Nexus.Agent.Intent;
using Nexus.Data.LLM;
using Nexus.Agent.Orchestration;
using Nexus.Data.Policies;
using Nexus.Data;
using Nexus.Data.Services;
using Nexus.Security.Sql;
using Nexus.Tools.Core;
using Nexus.Tools.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Add Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=NexusAgentDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<NexusDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("Nexus.Data")));

// Register Security & Policy Engines
builder.Services.AddSingleton<ISqlValidator, SqlValidator>();
builder.Services.AddSingleton<Nexus.Security.Risk.IRiskEngine, Nexus.Security.Risk.RiskEngine>();
builder.Services.AddSingleton<Nexus.Security.Permissions.IPermissionService, Nexus.Security.Permissions.PermissionService>();
builder.Services.AddScoped<IPolicyRuleEngine, PolicyRuleEngine>();
builder.Services.AddScoped<IPolicyComplianceEngine, PolicyComplianceEngine>();

// Register Domain Application Services (DI Container)
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();

// Register Local Open-Source LLM Subsystem
builder.Services.Configure<LLMOptions>(builder.Configuration.GetSection(LLMOptions.SectionName));

// Startup validation — catch the known bad model name early
var llmModel = builder.Configuration.GetSection(LLMOptions.SectionName).GetValue<string>("Model") ?? string.Empty;
if (llmModel.Contains("3.6", StringComparison.OrdinalIgnoreCase))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[NEXUS WARNING] LLM:Model '{llmModel}' does not exist. Change to 'gemini-2.0-flash' in appsettings.json.");
    Console.ResetColor();
}

// Provider-aware LLM service registration:
//   Provider = "Gemini"  → GeminiLLMService  (Google Generative Language API)
//   Provider = "Ollama"  → LocalLLMService   (local Ollama runtime)
var llmProvider = builder.Configuration
    .GetSection(LLMOptions.SectionName)
    .GetValue<string>("Provider") ?? "Gemini";

if (llmProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ILLMService, GeminiLLMService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
    });
    builder.Logging.AddFilter("System.Net.Http.HttpClient.GeminiLLMService", LogLevel.Warning);
}
else
{
    builder.Services.AddHttpClient<ILLMService, LocalLLMService>();
}
builder.Services.AddScoped<IIntentParser, IntentParser>();

// Register Document Generation Subsystem
builder.Services.AddScoped<IDocumentService, PdfDocumentService>();

// Register Python Automation Subsystem
builder.Services.AddSingleton<Nexus.Tools.Automation.IPythonAutomationService, Nexus.Tools.Automation.PythonAutomationService>();

// Register SAP Subsystem (Mock SAP Connector)
builder.Services.AddScoped<Nexus.Tools.Sap.ISapConnector, Nexus.Tools.Sap.MockSapConnector>();

// Register Gemini Function Calling Service
builder.Services.AddHttpClient<Nexus.Agent.LLM.GeminiFunctionCallingService>();

// Register Tool Subsystem
builder.Services.AddScoped<EmployeeCreateTool>();
builder.Services.AddScoped<EmployeeReadTool>();
builder.Services.AddScoped<EmployeeUpdateTool>();
builder.Services.AddScoped<EmployeeDeleteTool>();
builder.Services.AddScoped<DepartmentReadTool>();
builder.Services.AddScoped<BudgetReadTool>();
builder.Services.AddScoped<ExpenseReadTool>();
builder.Services.AddScoped<SqlAnalyticsTool>();
builder.Services.AddScoped<PolicyEvaluationTool>();
builder.Services.AddScoped<ComplianceTool>();
builder.Services.AddScoped<WelcomeEmailTool>();
builder.Services.AddScoped<CreateTicketTool>();
builder.Services.AddScoped<MockSapTool>();

// CRUD Tools
builder.Services.AddScoped<PolicyCrudTool>();
builder.Services.AddScoped<DepartmentCrudTool>();
builder.Services.AddScoped<BudgetUpdateTool>();
builder.Services.AddScoped<CvAnalysisTool>();
// Enterprise Workflow Tools
builder.Services.AddScoped<EmployeeTransferTool>();
builder.Services.AddScoped<EmployeeOffboardTool>();
builder.Services.AddScoped<LeaveTool>();
builder.Services.AddScoped<SlackNotifyTool>();
builder.Services.AddScoped<BudgetReallocateTool>();
builder.Services.AddScoped<BudgetFreezeTool>();
builder.Services.AddScoped<PayrollActionTool>();
builder.Services.AddScoped<BulkEmployeeUpdateTool>();
// Document & Onboarding/Offboarding/Orientation Tools
builder.Services.AddScoped<OnboardingPrepareTool>();
builder.Services.AddScoped<OffboardingInitiateTool>();
builder.Services.AddScoped<OrientationScheduleTool>();

builder.Services.AddScoped<IToolRegistry>(sp =>
{
    var registry = new ToolRegistry();
    registry.RegisterTool(sp.GetRequiredService<EmployeeCreateTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeReadTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeUpdateTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeDeleteTool>());
    registry.RegisterTool(sp.GetRequiredService<DepartmentReadTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetReadTool>());
    registry.RegisterTool(sp.GetRequiredService<ExpenseReadTool>());
    registry.RegisterTool(sp.GetRequiredService<SqlAnalyticsTool>());
    registry.RegisterTool(sp.GetRequiredService<PolicyEvaluationTool>());
    registry.RegisterTool(sp.GetRequiredService<ComplianceTool>());
    registry.RegisterTool(sp.GetRequiredService<WelcomeEmailTool>());
    registry.RegisterTool(sp.GetRequiredService<CreateTicketTool>());
    registry.RegisterTool(sp.GetRequiredService<MockSapTool>());

    registry.RegisterTool(sp.GetRequiredService<PolicyCrudTool>());
    registry.RegisterTool(sp.GetRequiredService<DepartmentCrudTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetUpdateTool>());
    registry.RegisterTool(sp.GetRequiredService<CvAnalysisTool>());
    // Enterprise Workflow Tools
    registry.RegisterTool(sp.GetRequiredService<EmployeeTransferTool>());
    registry.RegisterTool(sp.GetRequiredService<EmployeeOffboardTool>());
    registry.RegisterTool(sp.GetRequiredService<LeaveTool>());
    registry.RegisterTool(sp.GetRequiredService<SlackNotifyTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetReallocateTool>());
    registry.RegisterTool(sp.GetRequiredService<BudgetFreezeTool>());
    registry.RegisterTool(sp.GetRequiredService<PayrollActionTool>());
    registry.RegisterTool(sp.GetRequiredService<BulkEmployeeUpdateTool>());
    // Document & Onboarding/Offboarding/Orientation Tools
    registry.RegisterTool(sp.GetRequiredService<OnboardingPrepareTool>());
    registry.RegisterTool(sp.GetRequiredService<OffboardingInitiateTool>());
    registry.RegisterTool(sp.GetRequiredService<OrientationScheduleTool>());
    return registry;
});

// Register Real-Time Activity Event Broadcaster & SignalR
builder.Services.AddSingleton<Nexus.Agent.Events.IAgentEventBroadcaster, Nexus.Agent.Events.AgentEventBroadcaster>();
builder.Services.AddSignalR();

// Register Agent Orchestrator
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

// Configure CORS for local development UI & Vite Proxy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddOpenApi();

var app = builder.Build();

// Automatically ensure database schema & seed data exist on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        db.Database.EnsureCreated();

        var sql = @"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'Location')
                ALTER TABLE Employees ADD Location NVARCHAR(255) NULL;
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'ManagerName')
                ALTER TABLE Employees ADD ManagerName NVARCHAR(255) NULL;
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'StartDate')
                ALTER TABLE Employees ADD StartDate DATETIME2 NULL;

            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'UpdatedAt')
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Employees') AND name = 'DF_Employees_UpdatedAt')
                    ALTER TABLE Employees ADD CONSTRAINT DF_Employees_UpdatedAt DEFAULT GETUTCDATE() FOR UpdatedAt;
            END;

            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'CreatedAt')
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Employees') AND name = 'DF_Employees_CreatedAt')
                    ALTER TABLE Employees ADD CONSTRAINT DF_Employees_CreatedAt DEFAULT GETUTCDATE() FOR CreatedAt;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'IsFrozen')
                ALTER TABLE Budgets ADD IsFrozen BIT NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'FreezeReason')
                ALTER TABLE Budgets ADD FreezeReason NVARCHAR(500) NULL;
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Budgets') AND name = 'FrozenAt')
                ALTER TABLE Budgets ADD FrozenAt DATETIME2 NULL;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Leaves')
            BEGIN
                CREATE TABLE Leaves (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    EmployeeId INT NOT NULL FOREIGN KEY REFERENCES Employees(Id) ON DELETE CASCADE,
                    LeaveType INT NOT NULL,
                    StartDate DATETIME2 NOT NULL,
                    EndDate DATETIME2 NOT NULL,
                    Status INT NOT NULL DEFAULT 1,
                    Reason NVARCHAR(500) NULL,
                    Notes NVARCHAR(500) NULL,
                    ApprovedBy NVARCHAR(255) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tickets')
            BEGIN
                CREATE TABLE Tickets (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    TicketId NVARCHAR(50) NOT NULL,
                    EmployeeName NVARCHAR(150) NOT NULL,
                    Department NVARCHAR(100) NOT NULL,
                    RequestType NVARCHAR(150) NOT NULL,
                    Priority NVARCHAR(50) NOT NULL DEFAULT 'High',
                    Status NVARCHAR(50) NOT NULL DEFAULT 'Open',
                    Details NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MasterBudgets')
            BEGIN
                CREATE TABLE MasterBudgets (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Year INT NOT NULL DEFAULT 2026,
                    FiscalYear NVARCHAR(50) NOT NULL DEFAULT '2026-2027',
                    TotalBudgetPool DECIMAL(18,2) NOT NULL DEFAULT 1000000000.00,
                    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
                INSERT INTO MasterBudgets (Year, FiscalYear, TotalBudgetPool, UpdatedAt)
                VALUES (2026, '2026-2027', 1000000000.00, GETUTCDATE());
            END;
        ";
        db.Database.ExecuteSqlRaw(sql);

        // Auto-reseed defaults if database tables were purged
        if (!db.Departments.Any())
        {
            db.Departments.AddRange(
                new Nexus.Data.Entities.Department { Name = "IT", Description = "Information Technology and Systems Software Development" },
                new Nexus.Data.Entities.Department { Name = "HR", Description = "Human Resources & Talent Management" },
                new Nexus.Data.Entities.Department { Name = "Marketing", Description = "Brand Strategy, Growth & Digital Marketing" },
                new Nexus.Data.Entities.Department { Name = "Operations", Description = "Business Operations, Logistics & Maintenance" }
            );
            db.SaveChanges();
        }

        if (!db.Employees.Any())
        {
            var itDept = db.Departments.FirstOrDefault(d => d.Name == "IT") ?? db.Departments.First();
            var hrDept = db.Departments.FirstOrDefault(d => d.Name == "HR") ?? db.Departments.First();
            var opsDept = db.Departments.FirstOrDefault(d => d.Name == "Operations") ?? db.Departments.First();

            db.Employees.AddRange(
                new Nexus.Data.Entities.Employee { Name = "Tariq Mahmood", Email = "tariq.mahmood@nexus.local", DepartmentId = itDept.Id, Designation = "Senior .NET Developer", Salary = 75000.00m, ExperienceYears = 5, Status = Nexus.Data.Enums.EmployeeStatus.Active, CreatedAt = DateTime.UtcNow },
                new Nexus.Data.Entities.Employee { Name = "Sarah Jenkins", Email = "sarah.jenkins@nexus.local", DepartmentId = itDept.Id, Designation = "Lead IT Architect", Salary = 95000.00m, ExperienceYears = 8, Status = Nexus.Data.Enums.EmployeeStatus.Active, CreatedAt = DateTime.UtcNow },
                new Nexus.Data.Entities.Employee { Name = "Maria Garcia", Email = "maria.garcia@nexus.local", DepartmentId = hrDept.Id, Designation = "HR Specialist", Salary = 65000.00m, ExperienceYears = 4, Status = Nexus.Data.Enums.EmployeeStatus.Active, CreatedAt = DateTime.UtcNow },
                new Nexus.Data.Entities.Employee { Name = "Bilal Ahmed", Email = "bilal.ahmed@nexus.local", DepartmentId = opsDept.Id, Designation = "Operations Lead", Salary = 70000.00m, ExperienceYears = 6, Status = Nexus.Data.Enums.EmployeeStatus.Active, CreatedAt = DateTime.UtcNow }
            );
            db.SaveChanges();
        }

        if (!db.Tickets.Any())
        {
            db.Tickets.AddRange(
                new Nexus.Data.Entities.Ticket { TicketId = "TCK-2026-4829", EmployeeName = "Tariq Mahmood", Department = "IT", RequestType = "Hardware & Software Provisioning", Priority = "High", Status = "Resolved", Details = "Workstation laptop, Visual Studio Pro, VPN access provisioned.", CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new Nexus.Data.Entities.Ticket { TicketId = "TCK-2026-5102", EmployeeName = "Sarah Jenkins", Department = "IT", RequestType = "Security Clearance & Admin Access", Priority = "High", Status = "Resolved", Details = "Elevated admin privileges and cloud infrastructure access granted.", CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Nexus.Data.Entities.Ticket { TicketId = "TCK-2026-6941", EmployeeName = "Ahmed Khan", Department = "IT", RequestType = "Hardware Provisioning", Priority = "High", Status = "Open", Details = "MacBook Pro M3 Max 32GB, Dual 4K Monitors, YubiKey setup.", CreatedAt = DateTime.UtcNow.AddDays(-2) }
            );
            db.SaveChanges();
        }

    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database initial connection warning: Schema sync skipped or failed.");
    }
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "NEXUS-AGENT LITE API",
    status = "Online",
    version = "1.0.0",
    openApi = "/openapi/v1.json",
    health = "/api/agent/health"
}));
app.MapHub<Nexus.Api.Hubs.AgentActivityHub>("/hubs/agent-activity");

app.Run();
