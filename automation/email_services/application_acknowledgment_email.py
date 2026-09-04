import sys
import json
import os
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

def generate_application_acknowledgment_email(args: dict) -> dict:
    name = args.get("name") or args.get("candidateName") or "Candidate"
    position = args.get("position") or args.get("jobTitle") or args.get("title") or "Open Position"
    department = args.get("department") or "Engineering"
    recipient_email = args.get("email") or args.get("recipient_email") or args.get("to_email") or ""
    
    sender_email = os.environ.get("SMTP_SENDER_EMAIL") or os.environ.get("GMAIL_SENDER_EMAIL") or "nexusagent.notifications@gmail.com"
    smtp_password = os.environ.get("GMAIL_APP_PASSWORD") or os.environ.get("SMTP_PASSWORD") or ""
    
    date_str = datetime.now().strftime("%B %d, %Y")
    subject = f"Application Received: {position} — Nexus Enterprise"

    plain_text_body = (
        f"Dear {name},\n\n"
        f"Thank you for your interest in the {position} position at Nexus Enterprise. We are thrilled to have received your application.\n\n"
        f"Our talent acquisition team is in the process of reviewing the applications that we've received. Given the potential match between your skills and our needs, we anticipate moving forward with the next steps in our hiring process.\n\n"
        f"The next stage will involve a thorough review of your resume by the talent acquisition team. Should your profile align with our requirements, we will reach out to schedule an initial telephonic screening or an online assessment. This process typically takes 7 working days, and we appreciate your patience during this time. Please ensure to regularly check your inbox or spam folder for further communications from us.\n\n"
        f"Thank you for the time spent on your application and for considering Nexus Enterprise as your next employer.\n\n"
        f"Thanks,\n\n"
        f"Talent Acquisition Team\n"
        f"Nexus Enterprise Pvt. Ltd.\n"
    )

    html_body = f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{subject}</title>
  <style>
    body {{
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      background-color: #0d1117;
      color: #c9d1d9;
      margin: 0;
      padding: 0;
      -webkit-font-smoothing: antialiased;
    }}
    .wrapper {{
      width: 100%;
      background-color: #0d1117;
      padding: 36px 0;
    }}
    .container {{
      max-width: 600px;
      margin: 0 auto;
      background-color: #161b22;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5);
      border: 1px solid #30363d;
    }}
    .header {{
      background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
      padding: 28px 36px;
      border-bottom: 1px solid #30363d;
    }}
    .header h1 {{
      color: #ffffff;
      font-size: 20px;
      font-weight: 700;
      margin: 0;
      letter-spacing: 0.5px;
    }}
    .header span {{
      color: #38bdf8;
    }}
    .content {{
      padding: 36px;
      color: #e6edf3;
      line-height: 1.7;
    }}
    .greeting {{
      font-size: 18px;
      font-weight: 700;
      color: #ffffff;
      margin-top: 0;
      margin-bottom: 18px;
    }}
    .card {{
      background-color: #0d1117;
      border: 1px solid #30363d;
      border-radius: 8px;
      padding: 20px 24px;
      margin: 24px 0;
    }}
    .card-title {{
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 1px;
      color: #38bdf8;
      margin-top: 0;
      margin-bottom: 14px;
      border-bottom: 1px solid #21262d;
      padding-bottom: 8px;
    }}
    .info-table {{
      width: 100%;
      border-collapse: collapse;
    }}
    .info-table td {{
      padding: 6px 0;
      font-size: 13px;
      vertical-align: top;
    }}
    .info-label {{
      color: #8b949e;
      font-weight: 500;
      width: 38%;
    }}
    .info-value {{
      color: #f0f6fc;
      font-weight: 600;
      width: 62%;
    }}
    .callout {{
      background-color: rgba(56, 189, 248, 0.08);
      border-left: 4px solid #38bdf8;
      padding: 16px 20px;
      margin: 24px 0;
      border-radius: 0 8px 8px 0;
      font-size: 13px;
      color: #e6edf3;
    }}
    .signature {{
      font-size: 14px;
      color: #c9d1d9;
      border-top: 1px solid #21262d;
      padding-top: 20px;
      margin-top: 28px;
    }}
    .footer {{
      background-color: #0d1117;
      padding: 20px 36px;
      text-align: center;
      font-size: 11px;
      color: #8b949e;
      border-top: 1px solid #21262d;
    }}
  </style>
</head>
<body>
  <div class="wrapper">
    <div class="container">
      <div class="header">
        <h1>NEXUS <span>ENTERPRISE</span> &nbsp;|&nbsp; Talent Acquisition</h1>
      </div>
      <div class="content">
        <p class="greeting">Dear {name},</p>
        <p>
          Thank you for your interest in the <strong>{position}</strong> position at <strong>Nexus Enterprise</strong>. We are thrilled to have received your application.
        </p>

        <div class="card">
          <div class="card-title">Application Requisition Details</div>
          <table class="info-table">
            <tr>
              <td class="info-label">Candidate Name</td>
              <td class="info-value">{name}</td>
            </tr>
            <tr>
              <td class="info-label">Target Role</td>
              <td class="info-value">{position}</td>
            </tr>
            <tr>
              <td class="info-label">Department</td>
              <td class="info-value">{department}</td>
            </tr>
            <tr>
              <td class="info-label">Date Received</td>
              <td class="info-value">{date_str}</td>
            </tr>
            <tr>
              <td class="info-label">Application Status</td>
              <td class="info-value" style="color: #3fb950;">✓ Under Review by Talent Acquisition</td>
            </tr>
          </table>
        </div>

        <p>
          Our talent acquisition team is in the process of reviewing the applications that we’ve received. Given the potential match between your skills and our needs, we anticipate moving forward with the next steps in our hiring process.
        </p>

        <div class="callout">
          <strong>Next Steps:</strong> The next stage will involve a thorough review of your resume by the talent acquisition team. Should your profile align with our requirements, we will reach out to schedule an initial telephonic screening or an online assessment. This process typically takes <strong>7 working days</strong>, and we appreciate your patience during this time. Please ensure to regularly check your inbox or spam folder for further communications from us.
        </div>

        <p>
          Thank you for the time spent on your application and for considering Nexus Enterprise as your next employer.
        </p>

        <div class="signature">
          Thanks,<br><br>
          <strong style="color: #ffffff;">Talent Acquisition Team</strong><br>
          Nexus Enterprise Pvt. Ltd.<br>
          <span style="font-size: 12px; color: #8b949e;">nexusagent.notifications@gmail.com</span>
        </div>
      </div>
      <div class="footer">
        This is an automated operational notification generated by Nexus Agent Enterprise Careers Subsystem.
      </div>
    </div>
  </div>
</body>
</html>
"""

    email_sent = False
    delivery_note = None

    mock_domains = [".local", "devmail.com", "example.com", "test.com", "company.com", "nexus.local"]
    is_mock_local = not recipient_email or "@" not in recipient_email or any(recipient_email.lower().endswith(d) or f"@{d}" in recipient_email.lower() for d in mock_domains)

    if smtp_password and not is_mock_local:
        clean_password = smtp_password.replace(" ", "").strip()
        try:
            msg = MIMEMultipart("alternative")
            msg["From"] = sender_email
            msg["To"] = recipient_email
            msg["Subject"] = subject

            msg.attach(MIMEText(plain_text_body, "plain", "utf-8"))
            msg.attach(MIMEText(html_body, "html", "utf-8"))

            with smtplib.SMTP_SSL("smtp.gmail.com", 465, timeout=15) as server:
                server.login(sender_email, clean_password)
                msg["From"] = sender_email
                server.sendmail(sender_email, [recipient_email], msg.as_string())
            
            email_sent = True
            delivery_note = f"SUCCESS: Official candidate confirmation HTML email delivered to {recipient_email} via Gmail SMTP ({sender_email})."
        except Exception as e:
            delivery_note = f"Gmail SMTP error: {str(e)}"
    elif is_mock_local:
        delivery_note = f"NOTICE: Live SMTP transmission skipped for local mock address '{recipient_email}'."
    else:
        delivery_note = "NOTICE: Set GMAIL_APP_PASSWORD environment variable for SMTP authentication."

    return {
        "status": "success" if (email_sent or not smtp_password) else "failed",
        "operation": "email.application_acknowledgment",
        "senderEmail": sender_email,
        "recipientName": name,
        "recipientEmail": recipient_email,
        "position": position,
        "department": department,
        "subject": subject,
        "body": plain_text_body,
        "emailSent": email_sent,
        "deliveryNote": delivery_note,
        "generatedAt": datetime.utcnow().isoformat() + "Z"
    }

if __name__ == "__main__":
    raw_args = sys.argv[1] if len(sys.argv) > 1 else "{}"
    try:
        args_dict = json.loads(raw_args)
    except Exception:
        args_dict = {}
    
    result = generate_application_acknowledgment_email(args_dict)
    print(json.dumps(result, indent=2))
