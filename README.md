# 🚀 NEXUS-AGENT LITE — Enterprise Autonomous AI Workforce & Talent Platform

> **Turn Plain English Requests into Safe, Automated Enterprise Operations.**

**NEXUS-AGENT LITE** is a modern, full-stack enterprise workforce management platform powered by **C# .NET 10 Web API**, **Entity Framework Core**, **Google Gemini AI**, **Python Automation**, and **React 19**.

It empowers HR leaders, department managers, and recruiters to manage employees, track department budgets, query corporate policies, audit expenses, and screen job applicants using natural language.

---

## 📋 System Prerequisites

Before running the project, make sure you have the following installed on your computer:

| Requirement | Minimum Version | Note |
| :--- | :--- | :--- |
| **.NET SDK** | `.NET 9.0` or `.NET 10.0` | Powers the backend Web API |
| **Node.js & npm** | `Node.js v18+` & `npm v9+` | Powers the React frontend |
| **Python** | `Python 3.10+` | Powers background automation & email services |
| **Database** | *Zero manual setup* | Automatically connects to **SQL Server LocalDB** if available, or falls back to **InMemory DB**! |

---

## 🔑 AI Configuration (Google Gemini API Key)

To enable conversational AI and smart CV screening, configure a Google Gemini API key:

1. **Get a Free Key:** Visit **[Google AI Studio](https://aistudio.google.com/app/apikey)** and generate a free API key (takes 10 seconds).
2. **Add Key to Backend:** Open `backend/Nexus.Api/appsettings.json` and insert your key:
   ```json
   {
     "LLM": {
       "Provider": "Gemini",
       "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
       "Model": "gemini-3.5-flash"
     }
   }
   ```
   *(Alternatively, you can create a private `backend/Nexus.Api/appsettings.Development.json` or set the environment variable: `export LLM__ApiKey="your-key"`).*

---

## 🚀 Quick Start Guide (3 Simple Steps)

### Step 1: Start the Backend API (Port 5160)
Open a terminal in the root folder:
```bash
cd backend/Nexus.Api
dotnet run --launch-profile http
```
* **API URL:** `http://localhost:5160`
* **Swagger Documentation:** `http://localhost:5160/swagger`

---

### Step 2: Start the HR Admin Portal & AI Assistant (Port 3000)
Open a second terminal:
```bash
cd frontend
npm install
npm run dev
```
* **HR Admin App URL:** Open **`http://localhost:3000`** in your browser.

---

### Step 3: (Optional) Start the Public Candidate Portal (Port 3001)
Open a third terminal to test the public applicant experience:
```bash
cd frontend
npm run dev:portal
```
* **Public Careers Portal URL:** Open **`http://localhost:3001`** in your browser.

---

## 🌟 Key Platform Modules

### 1. 🤖 Conversational AI Assistant
Execute real organizational actions from natural language commands:
* **Workforce:** *"Onboard Ahmed Khan as a mid-level .NET developer in IT at $75,000"*
* **Leaves:** *"Log sick leave for Sarah today and notify her team"*
* **Budgets:** *"Reallocate $30,000 from Marketing budget to Engineering"*
* **Analytics:** *"Show me departments exceeding their Q3 allocated budget"*

---

### 2. 🎯 AI Talent Acquisition & CV Screening Pipeline
* **Dedicated Candidate Portal (`:3001`):** Filter roles by department (IT, Marketing, HR), review compensation/perks, and upload PDF resumes.
* **In-Browser PDF Viewer & Word Extraction:** Uses `UglyToad.PdfPig` to extract every word token from submitted PDF resumes.
* **Neural Match Scoring:** Calculates transparent 0–100% match scores against role requirements.
* **Tailored Interview Questions:** Dynamically generates 5 technical interview questions based on the candidate's actual projects and skill gaps.
* **1-Click Scheduling & Onboarding:** Dispatches styled HTML interview invitations and promotes candidates directly into the employee directory.

---

### 3. 💳 Expense Tracker & 1-Click AI Audit Engine
* **Employee Expense Logging:** Submit claims with categories (Meals, Travel, Equipment, Software).
* **Automated AI Policy Audit:** Scans claims against company spending caps (e.g. $50 meal limit, $500 travel pre-approval).
* **Policy Variance ($):** Calculates exact over-budget dollar amounts and flags unapproved weekend expenses.

---

### 4. 🏛️ Department Operations & Safe Budget Controls
* **Live Financial Overview:** Real-time visibility into Allocated Funds vs. Spent Budgets across all business units.
* **Safe Reallocations:** Built-in financial guards prevent negative balances or unauthorized budget transfers.

---

### 5. 📋 Corporate Policy Knowledge Base
* **Document Ingestion:** Ingests official policy documentation (Remote Work, Travel, Leave policies).
* **Instant Q&A:** Answers employee questions (e.g. *"Can I work remotely on Mondays?"*) with direct citations.

---

### 6. 🛡️ Enterprise Safety & Cryptographic Ledger
* **Human-in-the-Loop Approvals:** High-risk actions (salary changes, terminations, budget reallocations) require explicit manager sign-off.
* **Read-Only SQL Sandbox:** Strict AST validator blocks destructive SQL keywords (`DROP`, `DELETE`, `TRUNCATE`).
* **SHA-256 Cryptographic Audit Ledger:** Immutable audit trail tracking every agent-executed action with hash verification.

---

## 🧪 Running Automated Tests

To run the complete automated test suite (Unit tests, Intent parser tests, and Security verification):

```bash
dotnet test backend/Nexus.slnx
```
*(All 91 unit and integration tests run cleanly with 0 failures!)*

---

## 👥 Team & Authors

* **Muhammad Sufyan Khan** — Full-Stack & QA Engineer
* **Umar Danish** — Software Architecture & AI Automation Engineer
