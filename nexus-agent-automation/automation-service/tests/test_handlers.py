"""
tests/test_handlers.py
======================
Unit tests for all event handlers.
External calls (email, Slack) are mocked so these tests run without
SMTP servers, Redis, or Slack accounts.
"""

import pytest
from unittest.mock import patch, MagicMock

from app.handlers import (
    handle_employee_onboarded,
    handle_budget_exceeded,
    handle_budget_allocated,
    DISPATCHER,
)


# ── Fixtures ───────────────────────────────────────────────────────────────

VALID_ONBOARD_DATA = {
    "employeeName": "Alice Johnson",
    "department": "Engineering",
    "roleTitle": "Senior Software Engineer",
    "salary": 120000.0,
    "email": "alice@nexusagent.io",
}

VALID_BUDGET_EXCEEDED_DATA = {
    "departmentId": 7,
    "exceededAmount": 15000.0,
    "currentSpend": 115000.0,
    "allocatedBudget": 100000.0,
}

VALID_BUDGET_ALLOCATED_DATA = {
    "departmentId": 7,
    "amount": 50000.0,
    "newTotalSpend": 75000.0,
}


# ── DISPATCHER registry tests ──────────────────────────────────────────────

class TestDispatcher:
    def test_all_expected_event_types_registered(self):
        expected = {"EmployeeOnboarded", "BudgetExceeded", "BudgetAllocated"}
        assert expected.issubset(set(DISPATCHER.keys()))

    def test_dispatcher_values_are_callable(self):
        for key, fn in DISPATCHER.items():
            assert callable(fn), f"DISPATCHER['{key}'] is not callable"


# ── EmployeeOnboarded handler ──────────────────────────────────────────────

class TestHandleEmployeeOnboarded:

    @patch("app.handlers.slack.notify_slack_new_hire", return_value=True)
    @patch("app.handlers.email_service.send_onboarding_email")
    def test_success_sends_email_and_slack(self, mock_email, mock_slack):
        result = handle_employee_onboarded(VALID_ONBOARD_DATA)

        mock_email.assert_called_once_with(
            employee_name="Alice Johnson",
            email="alice@nexusagent.io",
            department="Engineering",
            role="Senior Software Engineer",
            salary=120000.0,
        )
        mock_slack.assert_called_once()
        assert result["handler"] == "EmployeeOnboarded"
        actions = {a["type"]: a["status"] for a in result["actions"]}
        assert actions["email"] == "sent"
        assert actions["slack"] == "sent"

    @patch("app.handlers.slack.notify_slack_new_hire", return_value=False)
    @patch("app.handlers.email_service.send_onboarding_email")
    def test_slack_skipped_does_not_fail_handler(self, mock_email, mock_slack):
        result = handle_employee_onboarded(VALID_ONBOARD_DATA)
        actions = {a["type"]: a["status"] for a in result["actions"]}
        assert actions["slack"] == "skipped"  # slack returned False (not configured)

    @patch("app.handlers.slack.notify_slack_new_hire", return_value=True)
    @patch("app.handlers.email_service.send_onboarding_email", side_effect=Exception("SMTP timeout"))
    def test_email_failure_is_recorded_not_raised(self, mock_email, mock_slack):
        # Handler must NOT raise – it must record the failure and continue
        result = handle_employee_onboarded(VALID_ONBOARD_DATA)
        actions = {a["type"]: a["status"] for a in result["actions"]}
        assert actions["email"] == "failed"
        assert "SMTP timeout" in result["actions"][0]["error"]

    def test_invalid_data_raises_validation_error(self):
        with pytest.raises((ValueError, TypeError, Exception)):
            handle_employee_onboarded({"employeeName": "No Email Field"})


# ── BudgetExceeded handler ─────────────────────────────────────────────────

class TestHandleBudgetExceeded:

    @patch("app.handlers.slack.notify_slack_budget_exceeded", return_value=True)
    @patch("app.handlers.email_service.send_budget_exceeded_alert")
    def test_success_sends_alert(self, mock_email, mock_slack):
        result = handle_budget_exceeded(VALID_BUDGET_EXCEEDED_DATA)

        mock_email.assert_called_once_with(
            department_id=7,
            exceeded_amount=15000.0,
            current_spend=115000.0,
            allocated_budget=100000.0,
        )
        assert result["handler"] == "BudgetExceeded"
        actions = {a["type"]: a["status"] for a in result["actions"]}
        assert actions["email"] == "sent"
        assert actions["slack"] == "sent"

    @patch("app.handlers.slack.notify_slack_budget_exceeded", side_effect=Exception("Slack down"))
    @patch("app.handlers.email_service.send_budget_exceeded_alert")
    def test_slack_failure_does_not_abort_handler(self, mock_email, mock_slack):
        result = handle_budget_exceeded(VALID_BUDGET_EXCEEDED_DATA)
        actions = {a["type"]: a["status"] for a in result["actions"]}
        assert actions["email"] == "sent"
        assert actions["slack"] == "failed"


# ── BudgetAllocated handler ────────────────────────────────────────────────

class TestHandleBudgetAllocated:

    @patch("app.handlers.email_service.send_budget_allocated_confirmation")
    def test_success_sends_confirmation(self, mock_email):
        result = handle_budget_allocated(VALID_BUDGET_ALLOCATED_DATA)

        mock_email.assert_called_once_with(
            department_id=7,
            amount=50000.0,
            new_total_spend=75000.0,
        )
        assert result["handler"] == "BudgetAllocated"
        actions = {a["type"]: a["status"] for a in result["actions"]}
        assert actions["email"] == "sent"
        assert actions["audit_log"] == "written"
