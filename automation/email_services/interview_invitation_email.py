import sys
import json
import os
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

def generate_interview_invitation_email(args: dict) -> dict:
    name = args.get("name") or args.get("candidateName") or "Candidate"
    position = args.get("position") or args.get("jobTitle") or args.get("title") or "Open Position"
    department = args.get("department") or "IT"
    recipient_email = args.get("email") or args.get("recipient_email") or args.get("to_email") or ""
    
    interview_date = args.get("interviewDate") or args.get("interview_date") or args.get("date") or "Upcoming"
    interview_time = args.get("interviewTime") or args.get("interview_time") or args.get("time") or "11:00 AM"
    mode = args.get("mode") or "Online"
    location_or_link = args.get("locationOrLink") or args.get("location") or args.get("link") or ("https://meet.google.com/nex-us-rec" if mode.lower() == "online" else "Nexus Enterprise Tech Tower, Level 4, IT Wing")
    notes = args.get("notes") or ""

    sender_email = os.environ.get("SMTP_SENDER_EMAIL") or os.environ.get("GMAIL_SENDER_EMAIL") or "nexusagent.notifications@gmail.com"
    smtp_password = os.environ.get("GMAIL_APP_PASSWORD") or os.environ.get("SMTP_PASSWORD") or "ibww vttv kyno zuti"
    
    subject = f"Interview Invitation: {position} — Nexus Enterprise"

    mode_display = "Online (Virtual Video Conference)" if "online" in mode.lower() else "Onsite (In-Person Interview)"
    location_label = "Meeting Link" if "online" in mode.lower() else "Office Location"

    plain_text_body = (
        f"Dear {name},\n\n"
        f"Following the review of your application and qualifications for the {position} role in our {department} department, "
        f"we are pleased to invite you for an official technical interview.\n\n"
        f"Interview Details:\n"
        f"• Position: {position} ({department})\n"
        f"• Date: {interview_date}\n"
        f"• Time: {interview_time}\n"
        f"• Mode: {mode_display}\n"
        f"• {location_label}: {location_or_link}\n"
        f"{f'• Special Instructions: {notes}' if notes else ''}\n\n"
        f"Please ensure you are available 5 minutes prior to the scheduled time with your working environment prepared.\n\n"
        f"If you need to reschedule or have any questions, please reply directly to this email.\n\n"
        f"Warm regards,\n\n"
        f"Talent Acquisition Team\n"
        f"Nexus Enterprise Pvt. Ltd.\n"
        f"{sender_email}\n"
    )

    notes_html = f"""
        <div style="margin-top: 14px; padding-top: 12px; border-top: 1px solid #30363d;">
          <span style="font-size: 11px; font-weight: 700; text-transform: uppercase; color: #94a3b8; letter-spacing: 0.5px;">Special Instructions:</span>
          <p style="margin: 4px 0 0 0; font-size: 13px; color: #e2e8f0;">{notes}</p>
        </div>
    """ if notes else ""

    link_button_html = f"""
        <div style="text-align: center; margin: 28px 0 12px 0;">
          <a href="{location_or_link}" style="background: linear-gradient(135deg, #4f46e5 0%, #6366f1 100%); color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 8px; font-weight: 600; font-size: 14px; display: inline-block; box-shadow: 0 4px 12px rgba(99, 102, 241, 0.35);">
            Join Interview Meeting
          </a>
        </div>
    """ if "http" in location_or_link else ""

    html_body = f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{subject}</title>
  <style>
    body {{
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      background-color: #0b0f19;
      color: #cbd5e1;
      margin: 0;
      padding: 0;
      -webkit-font-smoothing: antialiased;
    }}
    .wrapper {{
      width: 100%;
      background-color: #0b0f19;
      padding: 40px 0;
    }}
    .container {{
      max-width: 620px;
      margin: 0 auto;
      background-color: #111827;
      border-radius: 14px;
      overflow: hidden;
      box-shadow: 0 15px 35px -5px rgba(0, 0, 0, 0.6);
      border: 1px solid #1f2937;
    }}
    .header {{
      background: linear-gradient(135deg, #0f172a 0%, #1e1b4b 100%);
      padding: 32px 36px;
      border-bottom: 1px solid #312e81;
    }}
    .header h1 {{
      color: #ffffff;
      font-size: 22px;
      font-weight: 700;
      margin: 0;
      letter-spacing: 0.5px;
    }}
    .header span {{
      color: #818cf8;
    }}
    .header .subtitle {{
      color: #94a3b8;
      font-size: 12px;
      margin-top: 6px;
      text-transform: uppercase;
      letter-spacing: 1.5px;
      font-weight: 600;
    }}
    .content {{
      padding: 36px;
      color: #e2e8f0;
      line-height: 1.7;
    }}
    .greeting {{
      font-size: 19px;
      font-weight: 700;
      color: #ffffff;
      margin-top: 0;
      margin-bottom: 16px;
    }}
    .card {{
      background-color: #0f172a;
      border: 1px solid #334155;
      border-radius: 10px;
      padding: 22px 24px;
      margin: 24px 0;
    }}
    .card-header {{
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 1px;
      color: #818cf8;
      margin-bottom: 14px;
      border-bottom: 1px solid #1e293b;
      padding-bottom: 8px;
    }}
    .detail-row {{
      display: flex;
      justify-content: space-between;
      margin-bottom: 8px;
      font-size: 13px;
    }}
    .detail-label {{
      color: #94a3b8;
      font-weight: 500;
    }}
    .detail-value {{
      color: #f8fafc;
      font-weight: 600;
    }}
    .footer {{
      background-color: #0b0f19;
      padding: 24px 36px;
      border-top: 1px solid #1f2937;
      text-align: center;
      font-size: 12px;
      color: #64748b;
    }}
  </style>
</head>
<body>
  <div class="wrapper">
    <div class="container">
      <div class="header">
        <div class="subtitle">Nexus Autonomous Workforce Ecosystem</div>
        <h1>NEXUS <span>ENTERPRISE</span></h1>
      </div>
      <div class="content">
        <p class="greeting">Dear {name},</p>
        <p>
          We are pleased to inform you that after reviewing your profile and skills assessment, you have been 
          <strong style="color: #34d399;">shortlisted</strong> for the <strong>{position}</strong> requisition in our <strong>{department}</strong> department.
        </p>
        <p>
          We would like to formally invite you for an official technical & behavioral interview with our engineering panel.
        </p>

        <div class="card">
          <div class="card-header">🗓️ Scheduled Interview Details</div>
          <table style="width: 100%; border-collapse: collapse; font-size: 13px;">
            <tr>
              <td style="padding: 6px 0; color: #94a3b8; font-weight: 500;">Position:</td>
              <td style="padding: 6px 0; color: #f8fafc; font-weight: 600; text-align: right;">{position}</td>
            </tr>
            <tr>
              <td style="padding: 6px 0; color: #94a3b8; font-weight: 500;">Department:</td>
              <td style="padding: 6px 0; color: #f8fafc; font-weight: 600; text-align: right;">{department}</td>
            </tr>
            <tr>
              <td style="padding: 6px 0; color: #94a3b8; font-weight: 500;">Date:</td>
              <td style="padding: 6px 0; color: #38bdf8; font-weight: 700; text-align: right;">{interview_date}</td>
            </tr>
            <tr>
              <td style="padding: 6px 0; color: #94a3b8; font-weight: 500;">Time:</td>
              <td style="padding: 6px 0; color: #38bdf8; font-weight: 700; text-align: right;">{interview_time}</td>
            </tr>
            <tr>
              <td style="padding: 6px 0; color: #94a3b8; font-weight: 500;">Interview Mode:</td>
              <td style="padding: 6px 0; color: #a7f3d0; font-weight: 600; text-align: right;">{mode_display}</td>
            </tr>
            <tr>
              <td style="padding: 6px 0; color: #94a3b8; font-weight: 500;">{location_label}:</td>
              <td style="padding: 6px 0; color: #e0e7ff; font-weight: 600; text-align: right; word-break: break-all;">{location_or_link}</td>
            </tr>
          </table>
          {notes_html}
        </div>

        {link_button_html}

        <p style="font-size: 13px; color: #94a3b8;">
          Please ensure your audio, video, and environment are ready 5 minutes prior to the start time. 
          If you have any schedule conflicts or need to request an adjustment, feel free to reply directly to this email.
        </p>

        <p style="margin-top: 28px; margin-bottom: 0;">
          Best regards,<br>
          <strong style="color: #ffffff;">Talent Acquisition & People Operations</strong><br>
          Nexus Enterprise Pvt. Ltd.<br>
          <span style="font-size: 12px; color: #64748b;">nexusagent.notifications@gmail.com</span>
        </p>
      </div>
      <div class="footer">
        © 2026 Nexus Enterprise Systems Inc. All rights reserved.<br>
        Confidential Talent Acquisition Notification
      </div>
    </div>
  </div>
</body>
</html>
"""

    smtp_sent = False
    smtp_error = None

    if recipient_email:
        try:
            msg = MIMEMultipart("alternative")
            msg["Subject"] = subject
            msg["From"] = f"Nexus Enterprise Talent Acquisition <{sender_email}>"
            msg["To"] = recipient_email
            msg["Reply-To"] = sender_email

            part1 = MIMEText(plain_text_body, "plain", "utf-8")
            part2 = MIMEText(html_body, "html", "utf-8")
            msg.attach(part1)
            msg.attach(part2)

            with smtplib.SMTP_SSL("smtp.gmail.com", 465, timeout=15) as server:
                server.login(sender_email, smtp_password.replace(" ", ""))
                server.sendmail(sender_email, [recipient_email], msg.as_string())
            smtp_sent = True
        except Exception as e:
            smtp_error = str(e)

    return {
        "status": "SUCCESS" if (smtp_sent or not recipient_email) else "FAILED",
        "action": "INTERVIEW_INVITATION_DISPATCHED",
        "candidate": name,
        "recipient": recipient_email,
        "subject": subject,
        "smtp_sent": smtp_sent,
        "smtp_error": smtp_error,
        "date": interview_date,
        "time": interview_time,
        "mode": mode,
        "location": location_or_link
    }

if __name__ == "__main__":
    if len(sys.argv) > 1:
        try:
            input_args = json.loads(sys.argv[1])
        except Exception:
            input_args = {}
    else:
        input_args = {
            "name": "Muhammad Sufyan Khan",
            "position": "Junior SQA Engineer",
            "department": "IT",
            "email": "4t195es@gmail.com",
            "interviewDate": "September 5, 2026",
            "interviewTime": "11:00 AM PKT",
            "mode": "Online",
            "locationOrLink": "https://meet.google.com/nex-us-rec",
            "notes": "Please be prepared with a brief walkthrough of your QA testing projects."
        }
    res = generate_interview_invitation_email(input_args)
    print(json.dumps(res, indent=2))
