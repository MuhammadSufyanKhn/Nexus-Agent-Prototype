# NEXUS-AGENT LITE — Hackathon Demo Guide

This presenter guide walks through the 5 official demonstration scenarios for **NEXUS-AGENT LITE**, highlighting autonomous execution, natural language SQL analytics, policy compliance, human-in-the-loop safety gates, and RBAC security.

---

## 🎬 DEMO 1 — AI ONBOARDING AGENT (Multi-System Orchestration)

### Scenario Goal
Demonstrate how a single natural language request orchestrates policy evaluation, database creation, browser automation, SAP ERP provisioning, and email generation with a pre-execution human approval gate.

### Natural Language Prompt
```text
Onboard Ahmed Khan as a mid-level .NET developer in IT according to company policy. Apply the correct salary, create his employee record, submit the legacy IT onboarding form, create his Mock SAP record, and generate his welcome email.
```

### Step-by-Step Presentation Walkthrough
1. **Submit Prompt**: Click the first **Quick Prompt** badge in the Agent Console.
2. **Observe Real-Time Activity Feed**:
   - `✓ REQUEST_RECEIVED`: Request received.
   - `✓ INTENT_PARSED`: Intent classified as `EMPLOYEE_ONBOARDING`.
   - `✓ PLAN_GENERATED`: 5-step plan compiled.
   - `⏳ APPROVAL_REQUIRED`: High-risk action intercepted.
3. **Inspect Plan of Action Panel**:
   - **Title**: `Plan of Action: Comprehensive Onboarding for Ahmed Khan (IT)`
   - **Risk Level**: `HIGH`
   - **Affected Records**:
     - `Employee Master (SQL Server)`: `Mid-Level .NET Developer @ $68,000.00`
     - `Legacy HR Portal`: `HR-REC-2026-8812 (Pending)`
     - `Enterprise SAP HCM`: `SAP-EMP-2026-8912`
   - **Financial Impact**: `$68,000.00` (Calculated based on policy `POL-HR-001`).
4. **Highlight Zero Pre-Execution Mutation**: Open the **Employees** tab to prove that Ahmed Khan is NOT in the database yet.
5. **Confirm Action**: Return to Agent Console and click **Confirm & Execute Action Plan**.
6. **Verify Execution**:
   - `✓ BROWSER_AUTOMATION_COMPLETED`: Playwright filled and submitted form in `mock-portal/`.
   - `✓ SAP_OPERATION_COMPLETED`: Master data record committed in Mock SAP connector.
   - `✓ AUDIT_RECORDED`: SHA-256 cryptographic audit hash committed.
   - Open **Employees** tab to show Ahmed Khan's new SQL record ID.

---

## 📊 DEMO 2 — AI SQL ANALYST (Security Sandbox & Analytics)

### Scenario Goal
Demonstrate natural language to SQL analytics with strict security sandboxing (rejecting destructive commands, capping row counts, and providing business summaries).

### Natural Language Prompt
```text
Show me departments exceeding their allocated Q3 budget.
```

### Presentation Highlights
1. Click the **Show me departments exceeding...** quick prompt.
2. Inspect the **Natural Language SQL Analytics Result** card:
   - **Validated T-SQL Query**: `SELECT d.Name AS DepartmentName, b.AllocatedAmount, b.SpentAmount, (b.SpentAmount - b.AllocatedAmount) AS Variance FROM Budgets b JOIN Departments d ON b.DepartmentId = d.Id WHERE b.Quarter = 'Q3' AND b.SpentAmount > b.AllocatedAmount`
   - **Business Summary**: `💡 Business Summary: Marketing department exceeded its allocated Q3 budget by $8,500.00 (17%).`
   - **Data Table**: Columns and rows formatted cleanly in dark mode.

---

## 📜 DEMO 3 — POLICY COMPLIANCE (Audit & Evidence Engine)

### Scenario Goal
Demonstrate automated policy evaluation comparing expense claims against company limits with traceable policy citations.

### Natural Language Prompt
```text
Check whether Ahmed's latest expense complies with company policy.
```

### Presentation Highlights
1. Click the expense compliance prompt badge.
2. Inspect the **Policy Compliance Evidence & Audit** card:
   - **Compliance Status Badge**: `NON_COMPLIANT`
   - **Claimed Amount**: `$85.00`
   - **Policy Allowed Limit**: `$50.00` (Policy `POL-FIN-002`)
   - **Variance**: `$35.00` overflow.
   - **Policy Evidence Source**: `POL-FIN-002: Meal expenses capped at $50.00 per person.`

---

## 🛑 DEMO 4 — HUMAN APPROVAL (Plan of Action Interceptor)

### Scenario Goal
Demonstrate how high-risk bulk updates calculate financial impact and halt execution until explicit human authorization.

### Natural Language Prompt
```text
Increase salaries of all IT developers by 10%.
```

### Presentation Highlights
1. Click the bulk salary update prompt badge.
2. Inspect the **Plan of Action Panel**:
   - **Title**: `Plan of Action: Increase salaries of all IT developers by 10%`
   - **Risk Level**: `HIGH`
   - **Total Financial Impact**: `+$23,800.00`
   - **Change Preview Cards**:
     - `Tariq Mahmood`: `$75,000.00` ➔ `$82,500.00` (`+$7,500.00`)
     - `Sarah Jenkins`: `$95,000.00` ➔ `$104,500.00` (`+$9,500.00`)
     - `Ahmed Khan`: `$68,000.00` ➔ `$74,800.00` (`+$6,800.00`)
3. Verify that zero database modifications occurred prior to approval.
4. Click **Confirm & Execute Action Plan** to apply changes.

---

## 🛡️ DEMO 5 — SECURITY / CRITICAL GATE (Threat Prevention)

### Scenario Goal
Demonstrate that destructive commands (`Delete all employees`) or unauthorized role actions are blocked by the backend security engine.

### Natural Language Prompt
```text
Delete all employees.
```

### Presentation Highlights
1. Click the **Delete all employees** prompt badge.
2. Inspect the **Critical Security Gate** response:
   - Intent classified as `EMPLOYEE_DELETE` (Risk Level: `CRITICAL`).
   - Blocked by backend permission engine. Switch Role to `Employee` to show instant `403 Security Violation` rejection banner.
   - Proves LLM cannot bypass backend security controls.
