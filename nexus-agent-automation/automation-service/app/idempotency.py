"""
app/idempotency.py
==================
Event deduplication store.

Strategy:
  • If REDIS_URL is set → use Redis with per-key TTL (distributed, survives restarts).
  • Otherwise → use an in-memory dict (cleared on process restart; fine for single-instance).

Public API:
  is_processed(event_id: str) -> bool
  mark_processed(event_id: str) -> None
"""

import logging
import time
from typing import Dict, Optional

from app.config import settings

logger = logging.getLogger(__name__)

# ── Redis-backed store ─────────────────────────────────────────────────────

_redis_client = None

def _get_redis():
    """Lazy-initialise Redis client. Returns None if unavailable."""
    global _redis_client
    if _redis_client is not None:
        return _redis_client
    if not settings.redis_url:
        return None
    try:
        import redis  # type: ignore
        _redis_client = redis.from_url(settings.redis_url, decode_responses=True)
        _redis_client.ping()  # Verify connection at startup
        logger.info("Idempotency store: Redis connected at %s", settings.redis_url)
    except Exception as exc:
        logger.warning(
            "Failed to connect to Redis (%s). Falling back to in-memory idempotency store.", exc
        )
        _redis_client = None
    return _redis_client


# ── In-memory fallback store ───────────────────────────────────────────────
# Maps event_id → expiry_timestamp (float, epoch seconds)
_memory_store: Dict[str, float] = {}

TTL = settings.idempotency_ttl_seconds


def _memory_is_processed(event_id: str) -> bool:
    entry = _memory_store.get(event_id)
    if entry is None:
        return False
    if time.time() > entry:
        # Expired – remove it
        _memory_store.pop(event_id, None)
        return False
    return True


def _memory_mark_processed(event_id: str) -> None:
    _memory_store[event_id] = time.time() + TTL
    # Opportunistic clean-up: remove expired keys every 1000 marks to prevent memory leak
    if len(_memory_store) % 1000 == 0:
        now = time.time()
        expired = [k for k, v in _memory_store.items() if v < now]
        for k in expired:
            _memory_store.pop(k, None)
        if expired:
            logger.debug("Idempotency: evicted %d expired entries.", len(expired))


# ── Public API ─────────────────────────────────────────────────────────────

def is_processed(event_id: str) -> bool:
    """Return True if this event_id has already been handled successfully."""
    r = _get_redis()
    if r is not None:
        try:
            return r.exists(f"idempotency:{event_id}") == 1
        except Exception as exc:
            logger.warning("Redis read failed (%s). Falling back to in-memory check.", exc)
    return _memory_is_processed(event_id)


def mark_processed(event_id: str) -> None:
    """Record that this event_id has been handled. Idempotent itself."""
    r = _get_redis()
    if r is not None:
        try:
            r.setex(f"idempotency:{event_id}", TTL, "1")
            return
        except Exception as exc:
            logger.warning("Redis write failed (%s). Falling back to in-memory mark.", exc)
    _memory_mark_processed(event_id)
