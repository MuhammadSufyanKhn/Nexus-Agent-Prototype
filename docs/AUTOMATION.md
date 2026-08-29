# Multi-System Automation Subsystem

This document explains how **NEXUS-AGENT LITE** automates tasks across external systems, web portals, email services, and ticketing platforms.

---

## 1. Automation Subsystem Overview

The automation layer is built using **Python 3.12** and **Playwright**. It is invoked asynchronously by the .NET backend using a Python runner (`automation/runner.py`).

```
┌────────────────────────────────────────────────────────┐
│               .NET Backend (Nexus.Tools)               │
└───────────────────────────┬────────────────────────────┘
                            │ Shell Execution (json args)
                            ▼
┌────────────────────────────────────────────────────────┐
│                 Python Runner (runner.py)              │
└───────────┬───────────────┬───────────────┬────────────┘
            │               │               │
            ▼               ▼               ▼
┌──────────────────┐┌──────────────────┐┌──────────────────┐
│  Email Services  ││ Ticket Creation  ││ Playwright Portal│
│ (Sick Leave /    ││ (IT Provisioning)││  (Legacy HR      │
│  Welcome Email)  ││                  ││   Automation)    │
└──────────────────┘└──────────────────┘└──────────────────┘
```

---

## 2. Key Automation Operations

### 2.1 Sick Leave & Team Notification Emails (`sick_leave_email.py`)
* **Purpose**: Generates and dispatches HTML sick leave alerts when an employee logs a sick day.
* **Dual Recipient Routing**: Dispatches emails to **both**:
  1. Team Notification Inbox: `nexusagent.notifications@gmail.com`
  2. Employee Inbox: The exact database email stored for the employee in SQL Server (e.g. `4t195es@gmail.com`).
* **Bounce Protection**: Automatically filters out `.local` placeholder domains from envelope recipients so SMTP never bounces.

#### CLI Testing Command:
```bash
py automation/runner.py email.sick_leave '{"employeeName":"Employee Sufyan","employeeEmail":"4t195es@gmail.com","department":"HR","startDate":"August 29, 2026"}'
```

---

### 2.2 Corporate Welcome Emails (`welcome_email.py`)
* **Purpose**: Dispatches styled onboarding welcome emails to newly hired employees.
* **Features**: Formats credentials, department details, IT support contact, and orientation schedules into a HTML email template.

#### CLI Testing Command:
```bash
py automation/runner.py email.welcome '{"candidateName":"Ahmed Khan","email":"ahmed.khan@gmail.com","department":"IT","designation":"Mid .NET Developer"}'
```

---

### 2.3 Legacy HR Web Portal Automation (`legacy_hr_portal.py`)
* **Purpose**: Automates legacy web forms where no modern API exists.
* **Technology**: Uses Playwright Chromium in headless mode.
* **Flow**:
  1. Launches headless browser.
  2. Navigates to `http://127.0.0.1:8088`.
  3. Fills fields (`#employeeName`, `#department`, `#designation`, `#salary`, `#manager`).
  4. Clicks submit and captures the generated record reference number (e.g. `HR-REC-2026-8812`).

#### CLI Testing Command:
```bash
py automation/runner.py onboarding.submit_legacy_form '{"candidateName":"Ahmed Khan","department":"IT","designation":"Developer","salary":68000}'
```

---

### 2.4 IT Provisioning Tickets (`create_ticket.py`)
* **Purpose**: Generates automated IT provisioning tickets for laptops, software licenses, and security clearance.

---

## 3. Registering New Automation Operations

To register a new Python script:
1. Create a Python function inside `automation/email_services/` or `automation/browser/`.
2. Map the operation keyword in `automation/runner.py` inside the `main()` function:
```python
if operation in ["email.sick_leave", "email.notify"]:
    result = generate_sick_leave_email(args_dict)
```
3. Call `IPythonAutomationService.ExecuteOperationAsync("email.sick_leave", jsonArgs)` from your C# tool.
