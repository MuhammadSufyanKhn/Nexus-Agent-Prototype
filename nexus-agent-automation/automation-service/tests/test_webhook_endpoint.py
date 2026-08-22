"""
tests/test_webhook_endpoint.py
==============================
Integration tests for the /webhook FastAPI endpoint.

Uses FastAPI's TestClient (synchronous) – no real HTTP server needed.
All external calls (email, Slack, Redis) are mocked.

Tests cover:
  • Auth enforcement
  • Idempotency deduplication
  • Correct handler dispatch per eventType
  • Unsupported event type handling
  • Handler failure → HTTP 500
  • /health endpoint
"""

import pytest
from unittest.mock import patch, MagicMock
from fastapi.testclient import TestClient

# Patch settings BEFORE importing app.main so the app initialises with test values
import os
os.environ.setdefault("API_KEY", "test-secret-key-12345")
os.environ.setdefault("SMTP_HOST", "localhost")
os.environ.setdefault("SMTP_FROM", "test@example.com")

from app.main import app  # noqa: E402  (must be after env setup)

TEST_API_KEY = "test-secret-key-12345"

EMPLOYEE_ONBOARDED_PAYLOAD = {
    "eventId": "550e8400-e29b-41d4-a716-446655440001",
    "eventType": "EmployeeOnboarded",
    "entityId": 42,
    "data": {
        "employeeName": "Bob Smith",
        "department": "Finance",
        "roleTitle": "Financial Analyst",
        "salary": 85000.0,
        "email": "bob@nexusagent.io",
    },
    "executedBy": "admin@nexusagent.io",
    "timestamp": "2026-08-21T12:00:00Z",
}


@pytest.fixture()
def client():
    with TestClient(app) as c:
        yield c


@pytest.fixture(autouse=True)
def reset_idempotency_store():
    """Clear the in-memory idempotency store between tests."""
    from app import idempotency
    idempotency._memory_store.clear()
    yield
    idempotency._memory_store.clear()


# ── /health ────────────────────────────────────────────────────────────────

class TestHealthEndpoint:
    def test_health_returns_ok(self, client):
        resp = client.get("/health")
        assert resp.status_code == 200
        assert resp.json()["status"] == "ok"


# ── Authentication ─────────────────────────────────────────────────────────

class TestAuthentication:
    def test_missing_api_key_returns_401(self, client):
        resp = client.post("/webhook", json=EMPLOYEE_ONBOARDED_PAYLOAD)
        assert resp.status_code == 401

    def test_wrong_api_key_returns_401(self, client):
        resp = client.post(
            "/webhook",
            json=EMPLOYEE_ONBOARDED_PAYLOAD,
            headers={"X-Api-Key": "wrong-key"},
        )
        assert resp.status_code == 401

    @patch("app.handlers.email_service.send_onboarding_email")
    @patch("app.handlers.slack.notify_slack_new_hire", return_value=False)
    def test_correct_api_key_returns_200(self, mock_slack, mock_email, client):
        resp = client.post(
            "/webhook",
            json=EMPLOYEE_ONBOARDED_PAYLOAD,
            headers={"X-Api-Key": TEST_API_KEY},
        )
        assert resp.status_code == 200
        assert resp.json()["status"] == "success"


# ── Idempotency ────────────────────────────────────────────────────────────

class TestIdempotency:
    @patch("app.handlers.email_service.send_onboarding_email")
    @patch("app.handlers.slack.notify_slack_new_hire", return_value=False)
    def test_duplicate_event_returns_already_processed(self, mock_slack, mock_email, client):
        headers = {"X-Api-Key": TEST_API_KEY}

        # First call
        resp1 = client.post("/webhook", json=EMPLOYEE_ONBOARDED_PAYLOAD, headers=headers)
        assert resp1.status_code == 200
        assert resp1.json()["status"] == "success"

        # Second call with same eventId
        resp2 = client.post("/webhook", json=EMPLOYEE_ONBOARDED_PAYLOAD, headers=headers)
        assert resp2.status_code == 200
        assert resp2.json()["status"] == "already_processed"

        # Email should only have been sent ONCE
        mock_email.assert_called_once()


# ── Event dispatching ──────────────────────────────────────────────────────

class TestEventDispatching:
    def test_unsupported_event_type_returns_200_unsupported(self, client):
        payload = {**EMPLOYEE_ONBOARDED_PAYLOAD, "eventType": "SomeUnknownEvent",
                   "eventId": "550e8400-e29b-41d4-a716-446655440002"}
        resp = client.post(
            "/webhook", json=payload, headers={"X-Api-Key": TEST_API_KEY}
        )
        assert resp.status_code == 200
        assert resp.json()["status"] == "unsupported_event"

    @patch("app.handlers.email_service.send_budget_exceeded_alert")
    @patch("app.handlers.slack.notify_slack_budget_exceeded", return_value=False)
    def test_budget_exceeded_event_dispatched_correctly(self, mock_slack, mock_email, client):
        payload = {
            "eventId": "550e8400-e29b-41d4-a716-446655440003",
            "eventType": "BudgetExceeded",
            "entityId": 7,
            "data": {
                "departmentId": 7,
                "exceededAmount": 5000.0,
                "currentSpend": 105000.0,
                "allocatedBudget": 100000.0,
            },
            "executedBy": "system",
            "timestamp": "2026-08-21T12:00:00Z",
        }
        resp = client.post("/webhook", json=payload, headers={"X-Api-Key": TEST_API_KEY})
        assert resp.status_code == 200
        assert resp.json()["status"] == "success"
        mock_email.assert_called_once()

    @patch("app.handlers.email_service.send_onboarding_email", side_effect=Exception("SMTP down"))
    @patch("app.handlers.slack.notify_slack_new_hire", return_value=False)
    def test_handler_exception_returns_500(self, mock_slack, mock_email, client):
        payload = {**EMPLOYEE_ONBOARDED_PAYLOAD,
                   "eventId": "550e8400-e29b-41d4-a716-446655440004"}
        resp = client.post("/webhook", json=payload, headers={"X-Api-Key": TEST_API_KEY})
        # 500 tells the C# Polly policy to retry
        assert resp.status_code == 500
        assert resp.json()["status"] == "failed"


# ── Schema validation ──────────────────────────────────────────────────────

class TestSchemaValidation:
    def test_missing_required_fields_returns_422(self, client):
        resp = client.post(
            "/webhook",
            json={"eventType": "EmployeeOnboarded"},  # missing required fields
            headers={"X-Api-Key": TEST_API_KEY},
        )
        assert resp.status_code == 422
