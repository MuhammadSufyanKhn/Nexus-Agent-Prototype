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

// Register Local Open-Source LLM Subsystem
builder.Services.Configure<LLMOptions>(builder.Configuration.GetSection(LLMOptions.SectionName));

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
        client.Timeout = TimeSpan.FromSeconds(60); // outer HTTP timeout
    });
    builder.Logging.AddFilter("System.Net.Http.HttpClient.GeminiLLMService", LogLevel.Warning);
}
else
{
    builder.Services.AddHttpClient<ILLMService, LocalLLMService>();
}
builder.Services.AddScoped<IIntentParser, IntentParser>();

// Register Python Automation Subsystem
builder.Services.AddSingleton<Nexus.Tools.Automation.IPythonAutomationService, Nexus.Tools.Automation.PythonAutomationService>();

// Register SAP Subsystem (Mock SAP Connector)
builder.Services.AddScoped<Nexus.Tools.Sap.ISapConnector, Nexus.Tools.Sap.MockSapConnector>();

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
builder.Services.AddScoped<BrowserAutomationTool>();
builder.Services.AddScoped<MockSapTool>();

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
    registry.RegisterTool(sp.GetRequiredService<BrowserAutomationTool>());
    registry.RegisterTool(sp.GetRequiredService<MockSapTool>());
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
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database initial connection warning: EnsureCreated skipped or failed.");
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
