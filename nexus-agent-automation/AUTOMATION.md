# Nexus Agent — Automation Layer

## End-to-End Architecture

```
ASP.NET Core (NexusAgent.Api)
  │
  ├─ Controller receives HTTP request
  │
  ├─ CommandExecutionService
  │    ├─ Validates input (business rules)
  │    ├─ Persists to SQL Server via EF Core
  │    ├─ Commits transaction  ← ✅ data is safe from here on
  │    │
  │    └─ await _automationService.PublishAsync(event)
  │           ↓ (fire-and-forget with error isolation)
  │
  ├─ WebhookAutomationService (IAutomationService)
  │    ├─ Builds wire payload (camelCase JSON)
  │    ├─ Attaches X-Api-Key header
  │    ├─ HTTP POST → Python service (with Polly retry)
  │    └─ Catches ALL exceptions – never propagates
  │
  └─ Returns HTTP response to client (unaffected by automation)

         ↓  HTTP POST /webhook

Python Automation Service (FastAPI)
  │
  ├─ Validates X-Api-Key (constant-time compare)
  ├─ Parses JSON → AutomationEvent (Pydantic validation)
  ├─ Checks idempotency (in-memory / Redis)
  ├─ Routes to handler via DISPATCHER dict
  │
  ├─ handle_employee_onboarded()
  │    ├─ send_onboarding_email() → SMTP
  │    └─ notify_slack_new_hire() → Slack Webhook
  │
  ├─ handle_budget_exceeded()
  │    ├─ send_budget_exceeded_alert() → SMTP (finance + HR)
  │    └─ notify_slack_budget_exceeded() → Slack Webhook
  │
  └─ handle_budget_allocated()
       ├─ send_budget_allocated_confirmation() → SMTP
       └─ Writes to structured log (audit trail)
```

---

## Supported Event Types

| eventType | Trigger | Actions |
|---|---|---|
| `EmployeeOnboarded` | New employee record committed | Welcome email + Slack #new-hires |
| `BudgetExceeded` | Department spend > allocation | Alert email to finance+HR + Slack #finance-alerts |
| `BudgetAllocated` | Budget allocation committed | Confirmation email to finance + audit log |

---

## Environment Variables

### Backend (appsettings.json / environment)

| Key | Default | Description |
|---|---|---|
| `Automation:Provider` | `PythonWebhook` | Automation provider selector |
| `Automation:PythonServiceUrl` | `http://localhost:8000/webhook` | Python service webhook URL |
| `Automation:ApiKey` | *(required)* | Shared secret for header auth |
| `Automation:RetryCount` | `3` | Polly retry attempts |
| `Automation:TimeoutSeconds` | `10` | Per-attempt HTTP timeout |

> **Production**: Override `Automation:ApiKey` via environment variable `Automation__ApiKey` (double underscore = section separator in .NET).

### Python Service (.env / container env vars)

| Variable | Required | Description |
|---|---|---|
| `API_KEY` | ✅ | Must match backend's `Automation:ApiKey` |
| `SMTP_HOST` | ✅ | SMTP server hostname |
| `SMTP_PORT` | ✅ | SMTP port (587 or 465) |
| `SMTP_USER` | ✅ | SMTP username |
| `SMTP_PASSWORD` | ✅ | SMTP password |
| `SMTP_FROM` | ✅ | From address |
| `SMTP_USE_TLS` | `true` | Use STARTTLS (true) or SSL (false) |
| `FINANCE_ALERT_EMAIL` | `finance@nexusagent.io` | Budget alert recipient |
| `HR_ALERT_EMAIL` | `hr@nexusagent.io` | HR alert recipient |
| `SLACK_WEBHOOK_URL` | ⬜ | Slack incoming webhook URL |
| `TEAMS_WEBHOOK_URL` | ⬜ | Teams incoming webhook URL |
| `REDIS_URL` | ⬜ | Redis URL for distributed idempotency |
| `IDEMPOTENCY_TTL_SECONDS` | `86400` | Event ID expiry (24h) |
| `LOG_LEVEL` | `INFO` | Python log level |
| `ENVIRONMENT` | `development` | Disables /docs in production |

---

## Starting the Python Service

### Option A: Directly with uvicorn

```bash
cd automation-service
cp .env.example .env
# Fill in .env with real values
pip install -r requirements.txt
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

### Option B: Docker

```bash
cd automation-service
docker build -t nexus-agent-automation .
docker run -p 8000:8000 --env-file .env nexus-agent-automation
```

### Option C: Docker Compose (recommended for full stack)

Add to your solution-root `docker-compose.yml`:

```yaml
version: "3.9"
services:
  backend:
    build: ./NexusAgent.Api
    environment:
      - Automation__PythonServiceUrl=http://python-automation:8000/webhook
      - Automation__ApiKey=${AUTOMATION_API_KEY}
    depends_on:
      - python-automation

  python-automation:
    build: ./automation-service
    ports:
      - "8000:8000"
    environment:
      - API_KEY=${AUTOMATION_API_KEY}
      - SMTP_HOST=${SMTP_HOST}
      - SMTP_PORT=${SMTP_PORT}
      - SMTP_USER=${SMTP_USER}
      - SMTP_PASSWORD=${SMTP_PASSWORD}
      - SMTP_FROM=${SMTP_FROM}
      - SLACK_WEBHOOK_URL=${SLACK_WEBHOOK_URL}
      - REDIS_URL=redis://redis:6379/0
    depends_on:
      - redis
    healthcheck:
      test: ["CMD", "python", "-c", "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')"]
      interval: 30s
      timeout: 5s
      retries: 3

  redis:
    image: redis:7-alpine
    command: redis-server --save "" --appendonly no
```

Set `AUTOMATION_API_KEY` in a root `.env` file (same value for both services).

---

## Backend Program.cs Registration

```csharp
// Program.cs
using NexusAgent.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ... other registrations ...

// One line to wire the entire automation layer
builder.Services.AddAutomation(builder.Configuration);

var app = builder.Build();
// ...
```

---

## Verifying Integration

### Step 1: Start the Python service

```bash
cd automation-service && uvicorn app.main:app --reload
```

### Step 2: Send a manual test event

```bash
curl -X POST http://localhost:8000/webhook \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-super-secret-shared-key" \
  -d '{
    "eventId": "test-event-001",
    "eventType": "EmployeeOnboarded",
    "entityId": 1,
    "data": {
      "employeeName": "Test User",
      "department": "QA",
      "roleTitle": "Test Engineer",
      "salary": 80000,
      "email": "testuser@yourcompany.com"
    },
    "executedBy": "manual-test",
    "timestamp": "2026-08-21T12:00:00Z"
  }'
```

Expected response:
```json
{"status": "success", "eventId": "test-event-001", "result": {...}}
```

### Step 3: Verify health check

```bash
curl http://localhost:8000/health
# → {"status":"ok","service":"nexus-agent-automation","version":"1.0.0"}
```

### Step 4: Run the backend and trigger an employee onboard

Use the existing Nexus Agent API to create a new employee. Observe:
- Python service logs show the event received
- A welcome email arrives at the employee's address
- Slack #new-hires receives a Block Kit message (if configured)

---

## Retry and Resilience

| Scenario | Behaviour |
|---|---|
| Python service down | Backend Polly retries 3× (1s, 2s, 4s), then logs permanent failure. Business transaction unaffected. |
| SMTP server down | Python handler returns `{"status": "failed"}` with HTTP 500. Backend retries. |
| Duplicate event (backend retry) | Python idempotency store returns `already_processed` (HTTP 200). No double-send. |
| Invalid API key | Python returns HTTP 401. Backend logs auth failure. No retry. |
| Unknown event type | Python returns `unsupported_event` (HTTP 200). Backend logs warning. |

---

## Running All Tests

### Python tests

```bash
cd automation-service
pip install pytest httpx
pytest tests/ -v --tb=short
```

### C# tests

```bash
cd NexusAgent.Tests
dotnet test --verbosity normal
```

---

## Security Checklist

- [ ] `API_KEY` is set via environment variable, not hardcoded
- [ ] `SMTP_PASSWORD` is stored in a secrets manager (Azure Key Vault, AWS Secrets Manager, Docker Secrets)
- [ ] Python service is not publicly accessible (behind a private network or API gateway)
- [ ] HTTPS is enforced in production (TLS termination at reverse proxy)
- [ ] `/docs` (Swagger UI) is disabled in production (`ENVIRONMENT=production`)
- [ ] Redis (if used) is password-protected and not publicly exposed
- [ ] `Automation__ApiKey` in .NET is overridden by environment variable, never committed

---

## Adding a New Event Type (Checklist)

- [ ] Add C# event class in `NexusAgent.Application/Automation/`
- [ ] Add Python data model in `app/models.py`
- [ ] Add Python handler function in `app/handlers.py`
- [ ] Register handler in `DISPATCHER` dict in `app/handlers.py`
- [ ] Fire event in the relevant command service (after DB commit)
- [ ] Add unit tests for the new handler
- [ ] Update this document with the new event type
