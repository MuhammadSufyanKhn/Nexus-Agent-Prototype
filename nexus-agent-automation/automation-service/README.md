# Nexus Agent — Python Automation Service

A standalone FastAPI microservice that receives structured webhook events from the ASP.NET Core backend and performs downstream automation actions: sending emails, posting Slack/Teams notifications, and logging audit trails.

---

## Quick Start (Local Development)

### Prerequisites
- Python 3.11+
- `pip` or `pipx`

### 1. Clone and install

```bash
cd automation-service
cp .env.example .env
# Edit .env with your SMTP credentials and API key
pip install -r requirements.txt
```

### 2. Set environment variables

Edit `.env` (at minimum):

| Variable | Required | Description |
|---|---|---|
| `API_KEY` | ✅ | Shared secret. Must match `Automation:ApiKey` in the backend. |
| `SMTP_HOST` | ✅ | Your SMTP server hostname |
| `SMTP_PORT` | ✅ | Usually `587` (STARTTLS) or `465` (SSL) |
| `SMTP_USER` | ✅ | SMTP login username |
| `SMTP_PASSWORD` | ✅ | SMTP login password |
| `SMTP_FROM` | ✅ | From address for outgoing emails |
| `SLACK_WEBHOOK_URL` | ⬜ | Slack incoming webhook URL |
| `TEAMS_WEBHOOK_URL` | ⬜ | Microsoft Teams incoming webhook URL |
| `REDIS_URL` | ⬜ | Redis URL for distributed idempotency |

### 3. Run

```bash
uvicorn app.main:app --reload --port 8000
```

Service will be live at `http://localhost:8000`.

- **API docs**: http://localhost:8000/docs (disabled in production)
- **Health check**: http://localhost:8000/health

---

## Running with Docker

```bash
# Build
docker build -t nexus-agent-automation .

# Run (passing env vars directly)
docker run -p 8000:8000 \
  -e API_KEY=your-super-secret-key \
  -e SMTP_HOST=smtp.example.com \
  -e SMTP_PORT=587 \
  -e SMTP_USER=myuser \
  -e SMTP_PASSWORD=mypassword \
  -e SMTP_FROM=noreply@nexusagent.io \
  nexus-agent-automation
```

Or use Docker Compose (see `AUTOMATION.md` for the full compose file).

---

## Running Tests

```bash
pip install pytest httpx
pytest tests/ -v
```

Expected output: all tests pass without any real SMTP/Slack connections.

---

## Project Structure

```
automation-service/
├── app/
│   ├── __init__.py
│   ├── main.py          # FastAPI app, /webhook and /health endpoints
│   ├── config.py        # Pydantic-Settings configuration
│   ├── models.py        # Pydantic request models
│   ├── handlers.py      # Event handler functions + DISPATCHER registry
│   ├── email_service.py # SMTP email composition and delivery
│   ├── slack.py         # Slack Block Kit + Teams notifications
│   └── idempotency.py   # In-memory / Redis deduplication store
├── tests/
│   ├── test_handlers.py            # Unit tests for handlers
│   └── test_webhook_endpoint.py   # Integration tests via TestClient
├── requirements.txt
├── Dockerfile
├── .env.example
└── README.md
```

---

## Adding a New Event Type

1. **Define the data model** in `app/models.py`:
   ```python
   class MyNewEventData(BaseModel):
       someField: str
       anotherField: int
   ```

2. **Add a handler** in `app/handlers.py`:
   ```python
   def handle_my_new_event(data: Dict[str, Any]) -> Dict[str, Any]:
       payload = MyNewEventData(**data)
       # ... perform actions ...
       return {"handler": "MyNewEvent", "actions": [...]}
   ```

3. **Register in the dispatcher**:
   ```python
   DISPATCHER: Dict[str, Callable] = {
       ...,
       "MyNewEvent": handle_my_new_event,  # Add this line
   }
   ```

4. **Add the corresponding C# event** in `NexusAgent.Application/Automation/`.

No other changes required. The endpoint automatically routes to the new handler.

---

## Security Notes

- The `/webhook` endpoint validates every request against the `API_KEY` environment variable using constant-time string comparison (`hmac.compare_digest`).
- Never expose this service directly to the public internet without a reverse proxy enforcing TLS.
- SMTP credentials and API keys are loaded exclusively from environment variables — never from source code.
- The Swagger UI (`/docs`) is disabled in production (`ENVIRONMENT=production`).

---

## Integration with ASP.NET Core Backend

The backend calls this service at `POST /webhook` after every successful database transaction. Configure the backend by adding to `appsettings.json`:

```json
"Automation": {
  "Provider": "PythonWebhook",
  "PythonServiceUrl": "http://localhost:8000/webhook",
  "ApiKey": "your-super-secret-shared-key"
}
```

And register in `Program.cs`:

```csharp
builder.Services.AddAutomation(builder.Configuration);
```
