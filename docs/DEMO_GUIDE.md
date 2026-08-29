# Hackathon Demo & Presentation Guide

This guide walks you through **5 demonstration scenarios** showcasing the capabilities of **NEXUS-AGENT LITE**.

---

## Demo Setup Checklist

1. **Mock Legacy Portal**: Start running at `http://127.0.0.1:8088` (`py mock-portal/server.py`).
2. **Backend API**: Running at `https://localhost:7160` / `http://localhost:5160` (`dotnet run`).
3. **Frontend UI**: Open browser at `http://localhost:3000` (`npm run dev`).

---

## 🎬 5 Core Demo Scenarios

### Demo 1: Multi-System Employee Onboarding
* **Goal**: Show how a single natural language request triggers a multi-system workflow (SQL database, legacy portal form, SAP ERP, and welcome email).
* **Prompt to Type**:
  > `"Onboard Ahmed Khan as a mid-level .NET developer in IT according to company policy. Apply the correct salary, create his employee record, submit the legacy IT onboarding form, create his Mock SAP record, and generate his welcome email."`
* **What to Highlight**:
  1. Policy `POL-HR-001` is evaluated ($68,000.00 salary limit).
  2. Plan of Action is generated in status `AWAITING_APPROVAL`.
  3. **Zero database mutations occur** before human approval.
  4. Clicking **Authorize & Execute Plan** in the Approval Center creates the SQL record, fills out the Playwright form, provisions SAP, and sends the welcome email.

---

### Demo 2: Sick Leave Logging & Email Notification
* **Goal**: Demonstrate database lookup, sick leave record persistence, and dual email dispatch.
* **Prompt to Type**:
  > `"Log Sufyan's sick day today and notify his team on gmail."`
* **What to Highlight**:
  1. Searches SQL Server database for employee Sufyan (`4t195es@gmail.com`).
  2. Persists a sick leave entry in the `Leaves` SQL table.
  3. Dispatches HTML sick day alert emails to both `nexusagent.notifications@gmail.com` and the employee's database email.

---

### Demo 3: SQL Analytics Sandbox
* **Goal**: Show natural language to secure T-SQL query conversion.
* **Prompt to Type**:
  > `"Show me departments exceeding their allocated Q3 budget."`
* **What to Highlight**:
  1. Converts prompt into T-SQL query (`SELECT DepartmentName, SUM(AllocatedAmount)...`).
  2. Security sandbox verifies query is read-only `SELECT` (rejecting any `UPDATE` or `DROP`).
  3. Displays over-budget departments (IT department allocated $50,000 vs $58,500 spent).

---

### Demo 4: Human-in-the-Loop Approval Gate (Salary Increase)
* **Goal**: Show financial impact calculations and record change diff tables.
* **Prompt to Type**:
  > `"Increase salaries of all IT developers by 10%."`
* **What to Highlight**:
  1. Calculates total financial impact preview (+$23,800.00).
  2. Generates a visual diff table comparing previous salary vs. proposed new salary.
  3. Pauses execution until an administrator clicks **Authorize & Execute Plan**.

---

### Demo 5: Policy Compliance Evaluation
* **Goal**: Evaluate expense claims against company policy rules.
* **Prompt to Type**:
  > `"Check whether Ahmed's latest expense complies with company policy."`
* **What to Highlight**:
  1. Compares claim ($85.00 meal expense) against policy `POL-FIN-002` ($50.00 per-person meal limit).
  2. Highlights the $35.00 non-compliant overflow with audit evidence citations.
