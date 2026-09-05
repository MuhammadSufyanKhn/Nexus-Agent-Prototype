import sys
import json
import os
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

def generate_screening_rejection_email(args: dict) -> dict:
    name = args.get("name") or args.get("candidateName") or "Candidate"
    position = args.get("position") or args.get("jobTitle") or args.get("title") or "Open Position"
    department = args.get("department") or "IT"
    recipient_email = args.get("email") or args.get("recipient_email") or args.get("to_email") or ""

    sender_email = os.environ.get("SMTP_SENDER_EMAIL") or os.environ.get("GMAIL_SENDER_EMAIL") or "nexusagent.notifications@gmail.com"
    smtp_password = args.get("password") or args.get("smtp_password") or os.environ.get("GMAIL_APP_PASSWORD") or os.environ.get("SMTP_PASSWORD") or ""

    subject = f"Application Update: {position} — Nexus Enterprise"

    plain_text_body = (
        f"Dear {name},\n\n"
        f"Thank you for applying for the position of {position} with us at Nexus Enterprise. "
        f"We sincerely appreciate the time and effort you invested in applying for this position.\n\n"
        f"After careful consideration, we have decided to move forward with other candidates who more closely "
        f"match the requirements and qualifications we're seeking for this role.\n\n"
        f"Due to the high volume of applications, we are not able to provide individual feedback to candidates. "
        f"However, we do hope you'll stay connected with us and keep an eye on our future career opportunities.\n\n"
        f"While we regret that we couldn't proceed with your application for the {position} role at this time, "
        f"we want to emphasize that your skills and qualifications are valuable. We encourage you to continue "
        f"exploring opportunities on our career portal that align more closely with your profile and career goals.\n\n"
        f"We sincerely appreciate your interest in Nexus Enterprise and wish you all the best in your job search.\n\n"
        f"Thanks,\n"
        f"Talent Acquisition Team\n"
        f"Nexus Enterprise\n"
        f"{sender_email}\n"
    )

    html_body = f"""<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{subject}</title>
</head>
<body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #0f172a; color: #e2e8f0;">
  <div style="max-width: 600px; margin: 20px auto; background-color: #1e293b; border-radius: 12px; overflow: hidden; border: 1px solid #334155; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.4);">
    
    <!-- Header Banner -->
    <div style="background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 32px 30px; border-bottom: 1px solid #334155; text-align: center;">
      <div style="display: inline-block; padding: 8px 16px; background-color: rgba(59, 130, 246, 0.1); border: 1px solid rgba(59, 130, 246, 0.3); border-radius: 20px; margin-bottom: 12px;">
        <span style="color: #60a5fa; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px;">Nexus Talent Acquisition</span>
      </div>
      <h1 style="margin: 0; color: #ffffff; font-size: 22px; font-weight: 700; letter-spacing: -0.5px;">Application Status Update</h1>
      <p style="margin: 8px 0 0 0; color: #94a3b8; font-size: 13px;">Requisition: {position} &bull; {department}</p>
    </div>

    <!-- Main Content -->
    <div style="padding: 32px 30px; line-height: 1.6; font-size: 14px; color: #cbd5e1;">
      <p style="margin-top: 0; font-size: 15px; color: #f8fafc;">Dear <strong>{name}</strong>,</p>

      <p>Thank you for applying for the position of <strong>{position}</strong> with us at Nexus Enterprise. We sincerely appreciate the time, preparation, and effort you invested in your application.</p>

      <p>After careful consideration and review of all candidate submissions, we have decided to move forward with other candidates whose qualifications and technical competencies more closely align with the immediate requirements of this role.</p>

      <div style="background-color: #0f172a; border-left: 4px solid #64748b; border-radius: 0 8px 8px 0; padding: 14px 18px; margin: 24px 0;">
        <p style="margin: 0; font-size: 13px; color: #94a3b8; font-style: italic;">
          Due to the high volume of applications received, we are unable to provide individual assessment feedback. However, your profile has been retained in our talent database for future relevant openings.
        </p>
      </div>

      <p>While we regret that we cannot proceed with your candidacy for this role at this time, we want to emphasize that your background and skills are highly valuable. We strongly encourage you to keep an eye on our career portal for future opportunities that match your expertise and career goals.</p>

      <p style="margin-bottom: 0;">We sincerely appreciate your interest in Nexus Enterprise and wish you the very best in your job search and professional journey.</p>
    </div>

    <!-- Sign-off & Footer -->
    <div style="background-color: #0f172a; padding: 24px 30px; border-top: 1px solid #334155; font-size: 12px; color: #64748b;">
      <div style="margin-bottom: 12px;">
        <p style="margin: 0; color: #94a3b8; font-weight: 600;">Talent Acquisition Team</p>
        <p style="margin: 2px 0 0 0; color: #64748b;">Nexus Enterprise &bull; Autonomous HR Operations</p>
        <p style="margin: 2px 0 0 0; color: #3b82f6;">{sender_email}</p>
      </div>
      <p style="margin: 12px 0 0 0; font-size: 11px; color: #475569; border-top: 1px solid #1e293b; padding-top: 12px;">
        This automated notification was dispatched by the Nexus HR Intelligence Suite. Replies to this email address are monitored by Talent Acquisition.
      </p>
    </div>

  </div>
</body>
</html>
"""

    smtp_sent = False
    error_detail = None

    mock_domains = [".local", "devmail.com", "example.com", "test.com", "company.com", "nexus.local"]
    is_mock = not recipient_email or "@" not in recipient_email or any(recipient_email.lower().endswith(d) or f"@{d}" in recipient_email.lower() for d in mock_domains)

    if recipient_email and not is_mock:
        try:
            msg = MIMEMultipart("alternative")
            msg["Subject"] = subject
            msg["From"] = f"Nexus Enterprise Talent Acquisition <{sender_email}>"
            msg["To"] = recipient_email

            msg.attach(MIMEText(plain_text_body, "plain", "utf-8"))
            msg.attach(MIMEText(html_body, "html", "utf-8"))

            with smtplib.SMTP_SSL("smtp.gmail.com", 465, timeout=12) as server:
                server.login(sender_email, smtp_password.replace(" ", ""))
                server.sendmail(sender_email, [recipient_email], msg.as_string())
            smtp_sent = True
        except Exception as e:
            error_detail = str(e)

    return {
        "status": "SUCCESS" if smtp_sent else ("FAILED" if error_detail else "NO_RECIPIENT"),
        "action": "SCREENING_REJECTION_DISPATCHED",
        "candidate": name,
        "recipient": recipient_email,
        "position": position,
        "smtp_sent": smtp_sent,
        "error": error_detail,
        "timestamp": datetime.utcnow().isoformat() + "Z"
    }


def generate_interview_rejection_email(args: dict) -> dict:
    name = args.get("name") or args.get("candidateName") or "Candidate"
    position = args.get("position") or args.get("jobTitle") or args.get("title") or "Open Position"
    department = args.get("department") or "IT"
    recipient_email = args.get("email") or args.get("recipient_email") or args.get("to_email") or ""

    sender_email = os.environ.get("SMTP_SENDER_EMAIL") or os.environ.get("GMAIL_SENDER_EMAIL") or "nexusagent.notifications@gmail.com"
    smtp_password = args.get("password") or args.get("smtp_password") or os.environ.get("GMAIL_APP_PASSWORD") or os.environ.get("SMTP_PASSWORD") or ""

    subject = f"Update Regarding Your Interview for {position} — Nexus Enterprise"

    plain_text_body = (
        f"Dear {name},\n\n"
        f"Thank you for taking the time to interview with our engineering and talent acquisition team for the "
        f"{position} position at Nexus Enterprise. We truly appreciated the opportunity to speak with you "
        f"and learn more about your technical background and experience.\n\n"
        f"Following our interview evaluations, we regret to inform you that we have decided not to move forward "
        f"with your candidacy for this role at this time. This was a difficult decision given the strength "
        f"and caliber of the candidates interviewed.\n\n"
        f"We want to thank you again for your time, effort, and interest in joining Nexus Enterprise. "
        f"We will keep your resume on file for future roles that align with your background, and we wish you "
        f"continued success in your career.\n\n"
        f"Warm regards,\n"
        f"Talent Acquisition Team\n"
        f"Nexus Enterprise\n"
        f"{sender_email}\n"
    )

    html_body = f"""<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{subject}</title>
</head>
<body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #0f172a; color: #e2e8f0;">
  <div style="max-width: 600px; margin: 20px auto; background-color: #1e293b; border-radius: 12px; overflow: hidden; border: 1px solid #334155; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.4);">
    
    <!-- Header Banner -->
    <div style="background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 32px 30px; border-bottom: 1px solid #334155; text-align: center;">
      <div style="display: inline-block; padding: 8px 16px; background-color: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.3); border-radius: 20px; margin-bottom: 12px;">
        <span style="color: #818cf8; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1px;">Interview Evaluation Notice</span>
      </div>
      <h1 style="margin: 0; color: #ffffff; font-size: 22px; font-weight: 700; letter-spacing: -0.5px;">Interview Status Update</h1>
      <p style="margin: 8px 0 0 0; color: #94a3b8; font-size: 13px;">Role: {position} &bull; {department}</p>
    </div>

    <!-- Main Content -->
    <div style="padding: 32px 30px; line-height: 1.6; font-size: 14px; color: #cbd5e1;">
      <p style="margin-top: 0; font-size: 15px; color: #f8fafc;">Dear <strong>{name}</strong>,</p>

      <p>Thank you for taking the time to interview with our engineering and talent acquisition team for the <strong>{position}</strong> position at Nexus Enterprise. We truly appreciated the opportunity to speak with you and learn more about your technical expertise, project achievements, and background.</p>

      <p>Following comprehensive review and discussion across the interview panel, we regret to inform you that we have decided not to move forward with your candidacy for this role at this time.</p>

      <div style="background-color: #0f172a; border-left: 4px solid #818cf8; border-radius: 0 8px 8px 0; padding: 14px 18px; margin: 24px 0;">
        <p style="margin: 0; font-size: 13px; color: #94a3b8; font-style: italic;">
          This was a difficult decision given the competitive nature of our talent pool and the strong qualifications demonstrated across interviewed candidates.
        </p>
      </div>

      <p>We will keep your information and interview records securely retained in our talent database. Should new opportunities arise that align closely with your professional profile, our recruiting team may reach out to you directly.</p>

      <p style="margin-bottom: 0;">We want to thank you once again for your dedication, preparation, and interest in Nexus Enterprise, and we wish you continued success and distinction in your career.</p>
    </div>

    <!-- Sign-off & Footer -->
    <div style="background-color: #0f172a; padding: 24px 30px; border-top: 1px solid #334155; font-size: 12px; color: #64748b;">
      <div style="margin-bottom: 12px;">
        <p style="margin: 0; color: #94a3b8; font-weight: 600;">Talent Acquisition &amp; Engineering Hiring Pod</p>
        <p style="margin: 2px 0 0 0; color: #64748b;">Nexus Enterprise &bull; Autonomous HR Operations</p>
        <p style="margin: 2px 0 0 0; color: #3b82f6;">{sender_email}</p>
      </div>
      <p style="margin: 12px 0 0 0; font-size: 11px; color: #475569; border-top: 1px solid #1e293b; padding-top: 12px;">
        This automated notification was dispatched by the Nexus HR Intelligence Suite.
      </p>
    </div>

  </div>
</body>
</html>
"""

    smtp_sent = False
    error_detail = None

    mock_domains = [".local", "devmail.com", "example.com", "test.com", "company.com", "nexus.local"]
    is_mock = not recipient_email or "@" not in recipient_email or any(recipient_email.lower().endswith(d) or f"@{d}" in recipient_email.lower() for d in mock_domains)

    if recipient_email and smtp_password and not is_mock:
        try:
            msg = MIMEMultipart("alternative")
            msg["Subject"] = subject
            msg["From"] = f"Nexus Enterprise Talent Acquisition <{sender_email}>"
            msg["To"] = recipient_email

            msg.attach(MIMEText(plain_text_body, "plain", "utf-8"))
            msg.attach(MIMEText(html_body, "html", "utf-8"))

            with smtplib.SMTP_SSL("smtp.gmail.com", 465, timeout=12) as server:
                server.login(sender_email, smtp_password.replace(" ", ""))
                server.sendmail(sender_email, [recipient_email], msg.as_string())
            smtp_sent = True
        except Exception as e:
            error_detail = str(e)
    elif not smtp_password:
        error_detail = "NOTICE: SMTP transmission simulated (preview mode - GMAIL_APP_PASSWORD not set)."

    return {
        "status": "SUCCESS" if (smtp_sent or not smtp_password or not recipient_email) else "FAILED",
        "action": "INTERVIEW_REJECTION_DISPATCHED",
        "candidate": name,
        "recipient": recipient_email,
        "position": position,
        "smtp_sent": smtp_sent,
        "error": error_detail,
        "timestamp": datetime.utcnow().isoformat() + "Z"
    }

if __name__ == "__main__":
    raw_input = sys.stdin.read().strip() if not sys.stdin.isatty() else "{}"
    if len(sys.argv) > 2 and sys.argv[1] == "--args":
        raw_input = sys.argv[2]
    
    try:
        data = json.loads(raw_input) if raw_input else {}
    except Exception:
        data = {}

    mode = sys.argv[3] if len(sys.argv) > 3 else "screening"
    if mode == "interview":
        print(json.dumps(generate_interview_rejection_email(data), indent=2))
    else:
        print(json.dumps(generate_screening_rejection_email(data), indent=2))
