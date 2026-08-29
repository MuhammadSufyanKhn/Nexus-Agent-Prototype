# System Setup & Installation Guide

This document provides step-by-step instructions to set up, build, and run **NEXUS-AGENT LITE** on a local development machine.

---

## System Requirements

| Tool / Dependency | Version Required |
| :--- | :--- |
| **.NET SDK** | .NET 10.0+ |
| **Node.js** | Node.js v20.0+ (with `npm`) |
| **Python** | Python 3.12+ (with `py` launcher) |
| **Database** | SQL Server (LocalDB / Express / Developer) |

---

## Step 1: Clone Repository & Install Frontend Dependencies

```bash
# Open terminal in project root
cd frontend
npm install
```

---

## Step 2: Set Up Python Automation Subsystem

Install required Python packages:

```bash
# Run in project root
py -m pip install playwright
py -m playwright install chromium
```

---

## Step 3: Launch Mock Legacy HR Portal

Start the mock legacy HR portal server:

```bash
# Run in project root
py mock-portal/server.py
```
*(Confirms mock server is running at `http://127.0.0.1:8088/`)*

---

## Step 4: Build & Run ASP.NET Core Backend API

In a second terminal window:

```bash
cd backend/Nexus.Api
dotnet run
```
*(Confirms backend Web API is running at `https://localhost:7160` / `http://localhost:5160`)*

> **Note**: Database tables (`Employees`, `Departments`, `Budgets`, `Leaves`, `Expenses`, `Tickets`, `AuditLogs`) are created and updated automatically in SQL Server on startup.

---

## Step 5: Launch React Command Center Frontend

In a third terminal window:

```bash
cd frontend
npm run dev
```
*(Open browser at `http://localhost:3000`)*

---

## Troubleshooting & Verification

### Run Automated Backend Unit Tests:
```bash
cd backend
dotnet test Nexus.Tests/Nexus.Tests.csproj
```

### Validate Frontend TypeScript:
```bash
cd frontend
npx tsc --noEmit
```
