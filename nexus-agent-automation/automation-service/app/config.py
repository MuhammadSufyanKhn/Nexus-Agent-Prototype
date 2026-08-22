"""
app/config.py
=============
Pydantic-Settings based configuration loader.
All values come from environment variables (or .env file in development).

In production:
  • Set env vars directly on the container / host.
  • Never commit real secrets to .env or source control.
"""

from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field
from typing import Optional


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        # Allow extra fields in .env without raising validation errors
        extra="ignore",
    )

    # ── Security ────────────────────────────────────────────────────
    # Must match backend's Automation:ApiKey.
    # Production: set via environment variable API_KEY.
    api_key: str = Field(..., description="Shared secret sent in X-Api-Key header by the backend.")

    # ── SMTP ────────────────────────────────────────────────────────
    smtp_host: str = Field("localhost", description="SMTP server hostname.")
    smtp_port: int = Field(587, description="SMTP server port (587 for STARTTLS, 465 for SSL).")
    smtp_user: str = Field("", description="SMTP authentication username.")
    smtp_password: str = Field("", description="SMTP authentication password.")
    smtp_from: str = Field("noreply@nexusagent.io", description="From address for outgoing emails.")
    smtp_use_tls: bool = Field(True, description="Enable STARTTLS (port 587). Set False for SSL on 465.")

    # ── Finance / HR alert recipients ───────────────────────────────
    finance_alert_email: str = Field(
        "finance@nexusagent.io",
        description="Email address that receives BudgetExceeded alerts."
    )
    hr_alert_email: str = Field(
        "hr@nexusagent.io",
        description="Email address that receives HR-related alerts."
    )

    # ── Slack / Teams (optional) ────────────────────────────────────
    slack_webhook_url: Optional[str] = Field(
        None,
        description="Incoming Webhook URL for Slack. Leave empty to disable Slack notifications."
    )
    teams_webhook_url: Optional[str] = Field(
        None,
        description="Incoming Webhook URL for Microsoft Teams. Leave empty to disable."
    )

    # ── Redis (optional idempotency store) ──────────────────────────
    redis_url: Optional[str] = Field(
        None,
        description=(
            "Redis connection URL (e.g., redis://localhost:6379/0). "
            "If not set, falls back to an in-memory dict (cleared on restart)."
        )
    )
    idempotency_ttl_seconds: int = Field(
        86400,  # 24 hours
        description="How long a processed event ID is cached before expiry."
    )

    # ── Service ─────────────────────────────────────────────────────
    log_level: str = Field("INFO", description="Python logging level (DEBUG, INFO, WARNING, ERROR).")
    environment: str = Field("development", description="Runtime environment label.")


# Module-level singleton – import `settings` everywhere.
settings = Settings()
