"""
app/handlers.py
===============
Business logic for each automation event type.

Design:
  • Each handler receives the raw `data` dict from AutomationEvent.
  • Data is validated by parsing into a typed sub-model (raises ValueError on bad shape).
  • External calls (email, Slack) are each wrapped individually – one failure doesn't
    prevent the others from running.
  • All handlers return a plain dict summarising what was done (used in log output).

Dispatcher:
  The module-level DISPATCHER dict maps eventType strings → handler functions.
  Adding a new event type = add a function + register it in DISPATCHER. Done.
"""

import logging
from typing import Any, Callable, Dict

from app.models import BudgetAllocatedData, BudgetExceededData, EmployeeOnboardedData
from app import email_service, slack

logger = logging.getLogger(__name__)


# ── Handler implementations ────────────────────────────────────────────────

def handle_employee_onboarded(data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Triggered after a new employee is persisted.
    Actions:
      1. Send welcome email to the employee.
      2. Post Slack new-hire announcement (optional).
    """
    payload = EmployeeOnboardedData(**data)

    results: Dict[str, Any] = {"handler": "EmployeeOnboarded", "actions": []}

    # ── Action 1: Welcome email ──────────────────────────────────────
    try:
        email_service.send_onboarding_email(
            employee_name=payload.employeeName,
            email=payload.email,
            department=payload.department,
            role=payload.roleTitle,
            salary=payload.salary,
        )
        results["actions"].append({"type": "email", "status": "sent", "to": payload.email})
        logger.info("Onboarding email sent to %s (%s).", payload.employeeName, payload.email)
    except Exception as exc:
        results["actions"].append({"type": "email", "status": "failed", "error": str(exc)})
        logger.error("Failed to send onboarding email to %s: %s", payload.email, exc)

    # ── Action 2: Slack announcement ─────────────────────────────────
    try:
        sent = slack.notify_slack_new_hire(
            employee_name=payload.employeeName,
            department=payload.department,
            role=payload.roleTitle,
        )
        results["actions"].append({"type": "slack", "status": "sent" if sent else "skipped"})
    except Exception as exc:
        results["actions"].append({"type": "slack", "status": "failed", "error": str(exc)})
        logger.error("Failed to send Slack new-hire notification: %s", exc)

    return results


def handle_budget_exceeded(data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Triggered when a department's spend surpasses its budget.
    Actions:
      1. Send urgent alert email to finance + HR.
      2. Post Slack alert to #finance-alerts (optional).
    """
    payload = BudgetExceededData(**data)

    results: Dict[str, Any] = {"handler": "BudgetExceeded", "actions": []}

    # ── Action 1: Alert email ────────────────────────────────────────
    try:
        email_service.send_budget_exceeded_alert(
            department_id=payload.departmentId,
            exceeded_amount=payload.exceededAmount,
            current_spend=payload.currentSpend,
            allocated_budget=payload.allocatedBudget,
        )
        results["actions"].append({"type": "email", "status": "sent"})
        logger.warning(
            "Budget exceeded alert email sent for department #%d (exceeded by $%.2f).",
            payload.departmentId, payload.exceededAmount,
        )
    except Exception as exc:
        results["actions"].append({"type": "email", "status": "failed", "error": str(exc)})
        logger.error("Failed to send budget exceeded email: %s", exc)

    # ── Action 2: Slack alert ────────────────────────────────────────
    try:
        sent = slack.notify_slack_budget_exceeded(
            department_id=payload.departmentId,
            exceeded_amount=payload.exceededAmount,
            current_spend=payload.currentSpend,
            allocated_budget=payload.allocatedBudget,
        )
        results["actions"].append({"type": "slack", "status": "sent" if sent else "skipped"})
    except Exception as exc:
        results["actions"].append({"type": "slack", "status": "failed", "error": str(exc)})
        logger.error("Failed to send Slack budget alert: %s", exc)

    return results


def handle_budget_allocated(data: Dict[str, Any]) -> Dict[str, Any]:
    """
    Triggered when a budget allocation is committed.
    Actions:
      1. Send confirmation email to finance team.
      2. Log the allocation for audit trail.
    """
    payload = BudgetAllocatedData(**data)

    results: Dict[str, Any] = {"handler": "BudgetAllocated", "actions": []}

    # ── Action 1: Confirmation email ─────────────────────────────────
    try:
        email_service.send_budget_allocated_confirmation(
            department_id=payload.departmentId,
            amount=payload.amount,
            new_total_spend=payload.newTotalSpend,
        )
        results["actions"].append({"type": "email", "status": "sent"})
        logger.info(
            "Budget allocation confirmation sent for department #%d ($%.2f allocated).",
            payload.departmentId, payload.amount,
        )
    except Exception as exc:
        results["actions"].append({"type": "email", "status": "failed", "error": str(exc)})
        logger.error("Failed to send budget allocation confirmation: %s", exc)

    # ── Action 2: Audit log ──────────────────────────────────────────
    logger.info(
        "AUDIT | BudgetAllocated | dept=%d | amount=%.2f | newTotalSpend=%.2f",
        payload.departmentId, payload.amount, payload.newTotalSpend,
    )
    results["actions"].append({"type": "audit_log", "status": "written"})

    return results


# ── Dispatcher ────────────────────────────────────────────────────────────
# Maps eventType (string) → handler (callable).
# To add a new event type: define a handler function above and register here.

DISPATCHER: Dict[str, Callable[[Dict[str, Any]], Dict[str, Any]]] = {
    "EmployeeOnboarded": handle_employee_onboarded,
    "BudgetExceeded":    handle_budget_exceeded,
    "BudgetAllocated":   handle_budget_allocated,
}
