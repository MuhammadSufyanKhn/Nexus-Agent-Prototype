"""
app/email_service.py
====================
SMTP email composition and delivery.

Using Python's built-in smtplib + email.mime for zero extra dependencies.
All credentials come from app.config.settings (never hardcoded).

Functions:
  send_onboarding_email(employee_name, email, department, role) -> None
  send_budget_exceeded_alert(dept_id, exceeded, current, allocated) -> None
  send_budget_allocated_confirmation(dept_id, amount, new_total) -> None
"""

import logging
import smtplib
import ssl
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from typing import List

from app.config import settings

logger = logging.getLogger(__name__)


# ── Internal helpers ───────────────────────────────────────────────────────

def _build_message(to: List[str], subject: str, html_body: str) -> MIMEMultipart:
    msg = MIMEMultipart("alternative")
    msg["Subject"] = subject
    msg["From"]    = settings.smtp_from
    msg["To"]      = ", ".join(to)
    msg.attach(MIMEText(html_body, "html", "utf-8"))
    return msg


def _send(msg: MIMEMultipart, to: List[str]) -> None:
    """Low-level SMTP send. Raises on failure (callers decide how to handle)."""
    host = settings.smtp_host
    port = settings.smtp_port

    if settings.smtp_use_tls:
        # STARTTLS (port 587)
        with smtplib.SMTP(host, port, timeout=15) as smtp:
            smtp.ehlo()
            smtp.starttls(context=ssl.create_default_context())
            smtp.ehlo()
            if settings.smtp_user:
                smtp.login(settings.smtp_user, settings.smtp_password)
            smtp.sendmail(settings.smtp_from, to, msg.as_string())
    else:
        # SSL (port 465)
        context = ssl.create_default_context()
        with smtplib.SMTP_SSL(host, port, context=context, timeout=15) as smtp:
            if settings.smtp_user:
                smtp.login(settings.smtp_user, settings.smtp_password)
            smtp.sendmail(settings.smtp_from, to, msg.as_string())

    logger.info("Email sent to %s | subject: %s", to, msg["Subject"])


# ── Public API ─────────────────────────────────────────────────────────────

def send_onboarding_email(
    employee_name: str,
    email: str,
    department: str,
    role: str,
    salary: float,
) -> None:
    """
    Send a professional welcome email to the newly onboarded employee.
    """
    subject = f"Welcome to Nexus Agent, {employee_name}! 🎉"
    html_body = f"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>Welcome</title>
      <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #f4f6f8; margin: 0; padding: 0; }}
        .wrapper {{ max-width: 600px; margin: 40px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,.08); }}
        .header {{ background: linear-gradient(135deg, #1e3a5f 0%, #2563eb 100%); padding: 40px 32px; text-align: center; }}
        .header h1 {{ color: #ffffff; margin: 0; font-size: 26px; font-weight: 700; letter-spacing: -0.3px; }}
        .header p {{ color: #bfdbfe; margin: 8px 0 0; font-size: 14px; }}
        .body {{ padding: 36px 32px; }}
        .body p {{ color: #374151; line-height: 1.7; margin: 0 0 16px; }}
        .info-card {{ background: #f0f7ff; border-left: 4px solid #2563eb; border-radius: 4px; padding: 16px 20px; margin: 24px 0; }}
        .info-card table {{ width: 100%; border-collapse: collapse; }}
        .info-card td {{ padding: 6px 0; color: #1e3a5f; font-size: 14px; }}
        .info-card td:first-child {{ font-weight: 600; width: 140px; }}
        .cta {{ text-align: center; margin: 32px 0 8px; }}
        .cta a {{ background: #2563eb; color: #fff; padding: 14px 32px; border-radius: 6px; text-decoration: none; font-weight: 600; font-size: 15px; display: inline-block; }}
        .footer {{ background: #f9fafb; padding: 20px 32px; text-align: center; color: #9ca3af; font-size: 12px; border-top: 1px solid #e5e7eb; }}
      </style>
    </head>
    <body>
      <div class="wrapper">
        <div class="header">
          <h1>Welcome to Nexus Agent! 🚀</h1>
          <p>We're thrilled to have you on board</p>
        </div>
        <div class="body">
          <p>Hi <strong>{employee_name}</strong>,</p>
          <p>
            Congratulations and a very warm welcome to the <strong>Nexus Agent</strong> family!
            We are excited to have you join us and look forward to seeing the great work
            you'll accomplish in your new role.
          </p>
          <div class="info-card">
            <table>
              <tr><td>👤 Name</td><td>{employee_name}</td></tr>
              <tr><td>🏢 Department</td><td>{department}</td></tr>
              <tr><td>💼 Role</td><td>{role}</td></tr>
            </table>
          </div>
          <p>
            Your account has been set up. Please log in to the Nexus Agent portal to
            complete your onboarding checklist and explore your workspace.
          </p>
          <div class="cta">
            <a href="https://app.nexusagent.io/onboarding">Start Onboarding →</a>
          </div>
          <p>If you have any questions, reach out to <a href="mailto:hr@nexusagent.io">hr@nexusagent.io</a>.</p>
          <p>Welcome aboard,<br><strong>The Nexus Agent Team</strong></p>
        </div>
        <div class="footer">
          © 2026 Nexus Agent Inc. · <a href="https://nexusagent.io/privacy" style="color:#6b7280;">Privacy Policy</a>
          <br>This email was generated automatically. Please do not reply directly.
        </div>
      </div>
    </body>
    </html>
    """
    msg = _build_message([email], subject, html_body)
    _send(msg, [email])


def send_budget_exceeded_alert(
    department_id: int,
    exceeded_amount: float,
    current_spend: float,
    allocated_budget: float,
) -> None:
    """
    Send an urgent budget overspend alert to the finance and HR teams.
    """
    overspend_pct = (exceeded_amount / allocated_budget * 100) if allocated_budget else 0
    subject = f"⚠️ BUDGET ALERT: Department #{department_id} has exceeded its budget"
    html_body = f"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <title>Budget Alert</title>
      <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #fef2f2; margin: 0; padding: 0; }}
        .wrapper {{ max-width: 600px; margin: 40px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,.08); }}
        .header {{ background: linear-gradient(135deg, #7f1d1d 0%, #dc2626 100%); padding: 32px; text-align: center; }}
        .header h1 {{ color: #fff; margin: 0; font-size: 22px; font-weight: 700; }}
        .body {{ padding: 32px; }}
        .body p {{ color: #374151; line-height: 1.7; }}
        .alert-card {{ background: #fef2f2; border: 2px solid #dc2626; border-radius: 6px; padding: 20px; margin: 20px 0; }}
        .alert-card table {{ width: 100%; border-collapse: collapse; }}
        .alert-card td {{ padding: 8px 4px; font-size: 14px; color: #1f2937; }}
        .alert-card td:first-child {{ font-weight: 700; width: 180px; color: #dc2626; }}
        .badge {{ display: inline-block; background: #dc2626; color: #fff; padding: 4px 12px; border-radius: 99px; font-size: 13px; font-weight: 700; }}
        .footer {{ background: #f9fafb; padding: 16px 32px; text-align: center; color: #9ca3af; font-size: 12px; border-top: 1px solid #e5e7eb; }}
      </style>
    </head>
    <body>
      <div class="wrapper">
        <div class="header">
          <h1>⚠️ Budget Exceeded Alert</h1>
        </div>
        <div class="body">
          <p>This is an automated alert. Department <strong>#{department_id}</strong> has exceeded its allocated budget.</p>
          <div class="alert-card">
            <table>
              <tr><td>Department ID</td><td>#{department_id}</td></tr>
              <tr><td>Allocated Budget</td><td>${allocated_budget:,.2f}</td></tr>
              <tr><td>Current Spend</td><td>${current_spend:,.2f}</td></tr>
              <tr><td>Exceeded By</td><td><span class="badge">${exceeded_amount:,.2f} ({overspend_pct:.1f}%)</span></td></tr>
            </table>
          </div>
          <p>
            <strong>Immediate action may be required.</strong> Please review the department's
            expenditures and contact the department head to discuss corrective measures.
          </p>
          <p style="color:#6b7280;font-size:13px;">
            This alert was triggered automatically by Nexus Agent at the time of spend detection.
          </p>
        </div>
        <div class="footer">
          © 2026 Nexus Agent Inc. · Automated Finance Alert System
        </div>
      </div>
    </body>
    </html>
    """
    recipients = [settings.finance_alert_email, settings.hr_alert_email]
    msg = _build_message(recipients, subject, html_body)
    _send(msg, recipients)


def send_budget_allocated_confirmation(
    department_id: int,
    amount: float,
    new_total_spend: float,
) -> None:
    """
    Send a confirmation email to finance when a budget allocation is processed.
    """
    subject = f"✅ Budget Allocated: Department #{department_id} – ${amount:,.2f}"
    html_body = f"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <title>Budget Allocated</title>
      <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #f0fdf4; margin: 0; padding: 0; }}
        .wrapper {{ max-width: 600px; margin: 40px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,.08); }}
        .header {{ background: linear-gradient(135deg, #14532d 0%, #16a34a 100%); padding: 32px; text-align: center; }}
        .header h1 {{ color: #fff; margin: 0; font-size: 22px; }}
        .body {{ padding: 32px; }}
        .info-card {{ background: #f0fdf4; border-left: 4px solid #16a34a; border-radius: 4px; padding: 16px 20px; margin: 20px 0; }}
        .info-card table {{ width: 100%; border-collapse: collapse; }}
        .info-card td {{ padding: 6px 0; font-size: 14px; color: #1f2937; }}
        .info-card td:first-child {{ font-weight: 600; width: 160px; color: #166534; }}
        .footer {{ background: #f9fafb; padding: 16px 32px; text-align: center; color: #9ca3af; font-size: 12px; border-top: 1px solid #e5e7eb; }}
      </style>
    </head>
    <body>
      <div class="wrapper">
        <div class="header"><h1>✅ Budget Allocation Confirmed</h1></div>
        <div class="body">
          <p>A budget allocation has been successfully processed by Nexus Agent.</p>
          <div class="info-card">
            <table>
              <tr><td>Department ID</td><td>#{department_id}</td></tr>
              <tr><td>Allocated Amount</td><td>${amount:,.2f}</td></tr>
              <tr><td>New Total Spend</td><td>${new_total_spend:,.2f}</td></tr>
            </table>
          </div>
          <p style="color:#6b7280;font-size:13px;">Nexus Agent Automated Finance System</p>
        </div>
        <div class="footer">© 2026 Nexus Agent Inc.</div>
      </div>
    </body>
    </html>
    """
    recipients = [settings.finance_alert_email]
    msg = _build_message(recipients, subject, html_body)
    _send(msg, recipients)
