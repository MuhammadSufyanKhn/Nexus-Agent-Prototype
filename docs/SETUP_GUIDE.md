# NEXUS-AGENT LITE — Setup & Installation Guide

This document provides step-by-step instructions to set up, configure, and execute the complete **NEXUS-AGENT LITE** stack from a clean environment.

---

## 1. System Requirements & Dependencies

| Component | Required Version | Notes |
|---|---|---|
| **OS** | Windows 10/11, macOS, or Linux | Tested on Windows 11 with `py` launcher |
| **.NET SDK** | .NET 10 (v10.0+) | Target framework across all backend projects |
| **Node.js** | Node v20.0+ & npm | Required for React Vite frontend |
| **Python** | Python 3.12+ | Script runner & Playwright automation |
| **Playwright** | Playwright Chromium | Headless browser for Legacy HR Portal automation |
| **Database** | SQL Server / LocalDB / EF Core In-Memory | Seed script provided under `database/seed.sql` |
| **Local LLM** | Ollama (`llama3.2` or `llama3`) | Running at `http://localhost:11434` |

---

## 2. Local LLM Setup (Ollama)

NEXUS-AGENT LITE uses a local open-source LLM runtime (Ollama) to ensure complete data privacy without sending corporate data to cloud AI services.

1. **Install Ollama**: Download from [ollama.com](https://ollama.com/).
2. **Pull the Llama 3.2 Model**:
   ```bash
   ollama pull llama3.2
   ```
3. **Verify Local Inference Server**:
   Ensure Ollama is running at `http://localhost:11434`.
   ```bash
   curl http://localhost:11434/api/tags
   ```

*Note: If Ollama is offline during testing, NEXUS-AGENT LITE automatically falls back to deterministic rule-based intent parsing and sandbox execution.*

---

## 3. Python Automation & Playwright Setup

1. **Verify Python 3.12 Installation**:
   ```bash
   py --version
   # Output: Python 3.12.x
   ```

2. **Install Playwright Dependencies**:
   ```bash
   pip install playwright
   py -m playwright install chromium
   ```

3. **Verify Automation Runner Script**:
   Test CLI runner dispatch against legacy portal operation:
   ```bash
   py automation/runner.py onboarding.submit_legacy_form "employeeName=Ahmed Khan,department=IT"
   ```

---

## 4. SQL Server Database Setup

1. **Database Schema & Seed**:
   Execute `database/seed.sql` against your local SQL Server instance or LocalDB:
   ```bash
   sqlcmd -S (localdb)\MSSQLLocalDB -i database/seed.sql
   ```
2. **Entity Framework Core Database Migration**:
   ```bash
   cd backend/Nexus.Api
   dotnet ef database update
   ```

---

## 5. Starting the Complete Application Stack

To run the complete system, launch the services in separate terminal windows:

### Terminal 1: Start Mock Legacy HR Portal
```bash
py mock-portal/server.py
```
*Portal runs at `http://127.0.0.1:8088/index.html`*

### Terminal 2: Start Backend ASP.NET Core API
```bash
cd backend/Nexus.Api
dotnet run --launch-profile https
```
*API runs at `https://localhost:7160` / `http://localhost:5160` (Swagger UI at `/openapi`)*

### Terminal 3: Start React Command Center Frontend
```bash
cd frontend
npm install
npm run dev
```
*Command Center UI runs at `http://localhost:3000`*

---

## 6. Verifying System Health

Open the Command Center UI at `http://localhost:3000`:
- Check the top right header badge: `Ollama (llama3.2): ONLINE`.
- Select `Admin` in the Role switcher.
- Click quick prompt badge `"Onboard Ahmed Khan as a mid-level .NET developer in IT..."`.
