"""
app/slack.py
============
Slack and Microsoft Teams notification helpers.
Both are optional – if SLACK_WEBHOOK_URL / TEAMS_WEBHOOK_URL are not configured,
the functions silently skip (log a debug message).

Uses httpx for async-compatible HTTP (kept synchronous here for simplicity;
promote to async if needed for high-throughput scenarios).
"""

import logging
import json
from typing import Optional

import httpx

from app.config import settings

logger = logging.getLogger(__name__)


# ── Slack ──────────────────────────────────────────────────────────────────

def send_slack_message(text: str, blocks: Optional[list] = None) -> bool:
    """
    Post a message to the configured Slack incoming webhook.
    Returns True on success, False if Slack is not configured or the call fails.
    """
    if not settings.slack_webhook_url:
        logger.debug("Slack webhook not configured – skipping notification.")
        return False

    payload: dict = {"text": text}
    if blocks:
        payload["blocks"] = blocks

    try:
        resp = httpx.post(
            settings.slack_webhook_url,
            content=json.dumps(payload),
            headers={"Content-Type": "application/json"},
            timeout=10,
        )
        resp.raise_for_status()
        logger.info("Slack notification sent successfully.")
        return True
    except httpx.HTTPError as exc:
        logger.error("Failed to send Slack notification: %s", exc)
        return False


def notify_slack_new_hire(employee_name: str, department: str, role: str) -> bool:
    """Sends a formatted new-hire announcement to Slack #new-hires."""
    blocks = [
        {
            "type": "header",
            "text": {"type": "plain_text", "text": "🎉 New Team Member!", "emoji": True},
        },
        {
            "type": "section",
            "fields": [
                {"type": "mrkdwn", "text": f"*Name:*\n{employee_name}"},
                {"type": "mrkdwn", "text": f"*Department:*\n{department}"},
                {"type": "mrkdwn", "text": f"*Role:*\n{role}"},
            ],
        },
        {
            "type": "context",
            "elements": [
                {
                    "type": "mrkdwn",
                    "text": "Please join me in welcoming our newest Nexus Agent team member! 👋",
                }
            ],
        },
    ]
    return send_slack_message(
        text=f"🎉 Welcome {employee_name} to the {department} team as {role}!",
        blocks=blocks,
    )


def notify_slack_budget_exceeded(
    department_id: int, exceeded_amount: float, current_spend: float, allocated_budget: float
) -> bool:
    """Sends an urgent budget alert to Slack #finance-alerts."""
    blocks = [
        {
            "type": "header",
            "text": {"type": "plain_text", "text": "⚠️ Budget Exceeded Alert", "emoji": True},
        },
        {
            "type": "section",
            "fields": [
                {"type": "mrkdwn", "text": f"*Department:*\n#{department_id}"},
                {"type": "mrkdwn", "text": f"*Allocated:*\n${allocated_budget:,.2f}"},
                {"type": "mrkdwn", "text": f"*Current Spend:*\n${current_spend:,.2f}"},
                {"type": "mrkdwn", "text": f"*Exceeded By:*\n*${exceeded_amount:,.2f}*"},
            ],
        },
        {
            "type": "context",
            "elements": [
                {"type": "mrkdwn", "text": "🚨 Immediate review required. Contact department head."}
            ],
        },
    ]
    return send_slack_message(
        text=f"⚠️ Department #{department_id} exceeded budget by ${exceeded_amount:,.2f}!",
        blocks=blocks,
    )


# ── Microsoft Teams ────────────────────────────────────────────────────────

def send_teams_message(title: str, text: str, color: str = "0078D4") -> bool:
    """
    Post an Adaptive Card message to a Teams incoming webhook.
    Returns True on success, False if Teams is not configured or fails.
    """
    if not settings.teams_webhook_url:
        logger.debug("Teams webhook not configured – skipping notification.")
        return False

    payload = {
        "@type": "MessageCard",
        "@context": "https://schema.org/extensions",
        "themeColor": color,
        "summary": title,
        "sections": [{"activityTitle": title, "activityText": text}],
    }

    try:
        resp = httpx.post(
            settings.teams_webhook_url,
            content=json.dumps(payload),
            headers={"Content-Type": "application/json"},
            timeout=10,
        )
        resp.raise_for_status()
        logger.info("Teams notification sent successfully.")
        return True
    except httpx.HTTPError as exc:
        logger.error("Failed to send Teams notification: %s", exc)
        return False
