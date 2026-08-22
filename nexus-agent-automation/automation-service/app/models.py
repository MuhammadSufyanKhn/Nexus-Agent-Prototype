"""
app/models.py
=============
Pydantic v2 models for incoming webhook payloads from the ASP.NET Core backend.
These models intentionally mirror the C# AutomationEventDto wire format.
"""

from pydantic import BaseModel, Field
from typing import Any, Dict
from datetime import datetime


class AutomationEvent(BaseModel):
    """
    Canonical incoming event envelope.
    All fields must be present; the Python service rejects malformed payloads
    with HTTP 422 (FastAPI's automatic validation response).
    """
    eventId: str = Field(..., description="UUID string. Used for idempotency deduplication.")
    eventType: str = Field(..., description="Discriminator: 'EmployeeOnboarded', 'BudgetExceeded', etc.")
    entityId: int = Field(..., description="Primary entity ID (employee ID, department ID, …).")
    data: Dict[str, Any] = Field(..., description="Event-specific payload dictionary.")
    executedBy: str = Field(..., description="Username / service principal that triggered the event.")
    timestamp: datetime = Field(..., description="UTC timestamp from the backend (post-commit).")


# ── Typed data sub-models (used inside handlers for safer access) ──────────

class EmployeeOnboardedData(BaseModel):
    employeeName: str
    department: str
    roleTitle: str
    salary: float
    email: str


class BudgetAllocatedData(BaseModel):
    departmentId: int
    amount: float
    newTotalSpend: float


class BudgetExceededData(BaseModel):
    departmentId: int
    exceededAmount: float
    currentSpend: float
    allocatedBudget: float
