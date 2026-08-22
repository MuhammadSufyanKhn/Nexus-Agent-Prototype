"""
app/main.py
===========
FastAPI application entry point.

Endpoints:
  POST /webhook  – Receives automation events from the ASP.NET Core backend.
  GET  /health   – Liveness probe for container orchestrators / load balancers.

Security:
  Every request to /webhook is authenticated via the X-Api-Key header.
  The key is compared using hmac.compare_digest to prevent timing attacks.

Idempotency:
  Every successfully processed event is recorded in the idempotency store.
  Duplicate deliveries (backend retries) return HTTP 200 without re-processing.

Error contract:
  • HTTP 401 → bad or missing API key
  • HTTP 422 → malformed JSON / schema validation failure (FastAPI auto-response)
  • HTTP 200 + {"status": "already_processed"} → duplicate event
  • HTTP 200 + {"status": "unsupported_event"} → eventType not in DISPATCHER
  • HTTP 200 + {"status": "success"} → processed OK
  • HTTP 500 + {"status": "failed"} → handler raised an unrecoverable error
    (triggers Polly retry on the backend side)
"""

import hmac
import logging
import logging.config
from contextlib import asynccontextmanager
from typing import Any, Dict

from fastapi import FastAPI, Header, HTTPException, Request, status
from fastapi.responses import JSONResponse

from app.config import settings
from app.handlers import DISPATCHER
from app.idempotency import is_processed, mark_processed
from app.models import AutomationEvent

# ── Logging setup ──────────────────────────────────────────────────────────
logging.basicConfig(
    level=getattr(logging, settings.log_level.upper(), logging.INFO),
    format="%(asctime)s | %(levelname)-8s | %(name)s | %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%SZ",
)
logger = logging.getLogger(__name__)


# ── Lifespan (startup / shutdown) ─────────────────────────────────────────
@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info(
        "Nexus Agent Automation Service starting | env=%s | log_level=%s",
        settings.environment, settings.log_level,
    )
    logger.info("Registered event handlers: %s", list(DISPATCHER.keys()))
    yield
    logger.info("Nexus Agent Automation Service shutting down.")


# ── App factory ────────────────────────────────────────────────────────────
app = FastAPI(
    title="Nexus Agent – Automation Service",
    description=(
        "Internal webhook receiver for the Nexus Agent enterprise platform. "
        "Processes structured automation events from the ASP.NET Core backend."
    ),
    version="1.0.0",
    docs_url="/docs" if settings.environment != "production" else None,
    redoc_url=None,
    lifespan=lifespan,
)


# ── Auth helper ────────────────────────────────────────────────────────────

def _verify_api_key(x_api_key: str) -> None:
    """
    Constant-time comparison to prevent timing-based key enumeration attacks.
    Raises HTTP 401 if the key is missing or incorrect.
    """
    provided = x_api_key.encode("utf-8") if x_api_key else b""
    expected = settings.api_key.encode("utf-8")
    if not hmac.compare_digest(provided, expected):
        logger.warning("Rejected request: invalid or missing X-Api-Key header.")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or missing API key.",
        )


# ── Routes ─────────────────────────────────────────────────────────────────

@app.get("/health", tags=["Infrastructure"])
async def health_check() -> Dict[str, str]:
    """Liveness probe. Returns HTTP 200 when the service is running."""
    return {"status": "ok", "service": "nexus-agent-automation", "version": "1.0.0"}


@app.post("/webhook", tags=["Automation"])
async def receive_webhook(
    event: AutomationEvent,
    x_api_key: str = Header(default="", alias="X-Api-Key"),
) -> JSONResponse:
    """
    Primary webhook endpoint. Accepts automation events from the C# backend.

    Processing pipeline:
      1. Authenticate via X-Api-Key header.
      2. Check idempotency store – skip if already processed.
      3. Dispatch to registered handler.
      4. Mark as processed on success.
      5. Return structured result.
    """
    # ── Step 1: Authenticate ─────────────────────────────────────────
    _verify_api_key(x_api_key)

    log_context = f"eventType={event.eventType} eventId={event.eventId} entityId={event.entityId}"
    logger.info("Webhook received | %s | executedBy=%s", log_context, event.executedBy)

    # ── Step 2: Idempotency check ────────────────────────────────────
    if is_processed(event.eventId):
        logger.info("Duplicate event ignored | %s", log_context)
        return JSONResponse(
            status_code=status.HTTP_200_OK,
            content={"status": "already_processed", "eventId": event.eventId},
        )

    # ── Step 3: Dispatch ─────────────────────────────────────────────
    handler = DISPATCHER.get(event.eventType)
    if handler is None:
        logger.warning("No handler registered for eventType '%s'.", event.eventType)
        return JSONResponse(
            status_code=status.HTTP_200_OK,
            content={
                "status": "unsupported_event",
                "eventType": event.eventType,
                "message": f"No handler registered for '{event.eventType}'. Event acknowledged but not processed.",
            },
        )

    # ── Step 4: Execute handler ──────────────────────────────────────
    try:
        result: Dict[str, Any] = handler(event.data)

        # ── Step 5: Mark processed ───────────────────────────────────
        mark_processed(event.eventId)

        logger.info("Event processed successfully | %s | result=%s", log_context, result)
        return JSONResponse(
            status_code=status.HTTP_200_OK,
            content={"status": "success", "eventId": event.eventId, "result": result},
        )

    except Exception as exc:
        # Return 500 so the backend's Polly policy triggers a retry.
        logger.error(
            "Handler '%s' raised an unrecoverable error for %s: %s",
            event.eventType, log_context, exc,
            exc_info=True,
        )
        return JSONResponse(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            content={
                "status": "failed",
                "eventId": event.eventId,
                "error": str(exc),
            },
        )


# ── Exception handler for unexpected errors ────────────────────────────────

@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    logger.critical("Unhandled exception on %s: %s", request.url, exc, exc_info=True)
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content={"status": "error", "detail": "Internal server error."},
    )
