# HR Policies & Compliance Reference

This document outlines the HR governance rules and financial thresholds enforced by **NEXUS-AGENT LITE**.

---

## 1. Active Policy Rules

### `POL-HR-001`: Salary Band Compliance
* **Standard Mid-Level Developer Salary**: Default allocation is set to **$68,000.00 / year**.
* **Rule**: Salary proposals outside designated bands trigger policy warnings in the Plan of Action.

### `POL-FIN-002`: Expense Claim Compliance
* **Meal Expense Limit**: Maximum allowed claim per person is **$50.00**.
* **Rule**: Claims exceeding $50.00 per person are flagged as `NonCompliant` with financial overflow citations.

### `POL-HR-003`: Leave & Sick Day Logging
* **Sick Leave Policy**: Employees can log single-day sick leave with automatic team email notifications dispatched.

---

## 2. Policy Enforcement Engine

When prompts are evaluated against company policies:
1. `PolicyService.cs` retrieves matching policy rules from SQL Server or database memory.
2. Compliance status (`Compliant` vs. `NonCompliant`) is evaluated against numerical limits.
3. Policy citations are appended to the Action Plan for human administrator review.
