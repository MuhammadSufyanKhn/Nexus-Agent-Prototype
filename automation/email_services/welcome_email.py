import sys
import json
import os
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from datetime import datetime

def generate_welcome_email(args: dict) -> dict:
    name = args.get("name") or args.get("employeeName") or args.get("candidate") or "New Employee"
    designation = args.get("designation") or args.get("role") or args.get("jobTitle") or "Team Member"
    department = args.get("department") or args.get("targetDepartment") or "IT"
    employee_id = args.get("employeeId") or args.get("id") or args.get("emp_id") or "NEX-2026-PENDING"
    joining_date = args.get("joiningDate") or args.get("startDate") or args.get("joining_date") or datetime.now().strftime("%B %d, %Y")
    manager = args.get("manager") or args.get("managerName") or args.get("reportingManager") or f"{department} Department Manager"
    
    sender_email = os.environ.get("SMTP_SENDER_EMAIL") or os.environ.get("GMAIL_SENDER_EMAIL") or "nexusagent.notifications@gmail.com"
    recipient_email = args.get("email") or args.get("recipient_email") or args.get("to_email") or args.get("officialEmail") or ""
    smtp_password = args.get("password") or args.get("smtp_password") or os.environ.get("GMAIL_APP_PASSWORD") or os.environ.get("SMTP_PASSWORD") or ""

    subject = f"Welcome to the Team, {name}! — Your Onboarding Details"

    plain_text_body = (
        f"Dear {name},\n\n"
        f"Welcome to Nexus Agent! We are thrilled to confirm that you have successfully joined our organization as a {designation} in the {department} Department.\n\n"
        f"OFFICIAL ONBOARDING DETAILS:\n"
        f"----------------------------------------\n"
        f"Employee Name    : {name}\n"
        f"Employee ID      : {employee_id}\n"
        f"Department       : {department}\n"
        f"Designation      : {designation}\n"
        f"Joining Date     : {joining_date}\n"
        f"Reporting Manager: {manager}\n"
        f"Official Email   : {recipient_email}\n"
        f"----------------------------------------\n\n"
        f"NEXT STEPS:\n"
        f"1. Complete identity and HR onboarding documentation.\n"
        f"2. Schedule your initial orientation session with {manager}.\n"
        f"3. Collect your IT workstation credentials and security badge.\n"
        f"4. Review company policies and compliance guidelines in the employee portal.\n\n"
        f"Welcome to the team, {name}. We look forward to your contributions and wish you a successful journey with Nexus Agent.\n\n"
        f"Best regards,\n"
        f"Nexus Agent | Human Resources\n"
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
      background-color: #f1f5f9;
      margin: 0;
      padding: 0;
      -webkit-font-smoothing: antialiased;
    }}
    .wrapper {{
      width: 100%;
      background-color: #f1f5f9;
      padding: 30px 0;
    }}
    .container {{
      max-width: 600px;
      margin: 0 auto;
      background-color: #ffffff;
      border-radius: 8px;
      overflow: hidden;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
      border: 1px solid #e2e8f0;
    }}
    .header {{
      background-color: #0f172a;
      padding: 24px 32px;
      text-align: left;
    }}
    .header h1 {{
      color: #ffffff;
      font-size: 20px;
      font-weight: 600;
      margin: 0;
      letter-spacing: 0.5px;
    }}
    .header span {{
      color: #38bdf8;
      font-weight: 700;
    }}
    .content {{
      padding: 32px;
      color: #334155;
      line-height: 1.6;
    }}
    .greeting {{
      font-size: 18px;
      font-weight: 600;
      color: #0f172a;
      margin-top: 0;
      margin-bottom: 16px;
    }}
    .intro {{
      font-size: 15px;
      color: #475569;
      margin-bottom: 24px;
    }}
    .card {{
      background-color: #f8fafc;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      padding: 20px 24px;
      margin-bottom: 24px;
    }}
    .card-title {{
      font-size: 14px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.8px;
      color: #475569;
      margin-top: 0;
      margin-bottom: 16px;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 8px;
    }}
    .info-table {{
      width: 100%;
      border-collapse: collapse;
    }}
    .info-table td {{
      padding: 6px 0;
      font-size: 14px;
      vertical-align: top;
    }}
    .info-label {{
      color: #64748b;
      font-weight: 500;
      width: 40%;
    }}
    .info-value {{
      color: #0f172a;
      font-weight: 600;
      width: 60%;
    }}
    .next-steps {{
      background-color: #ffffff;
      border-left: 4px solid #0284c7;
      padding: 16px 20px;
      margin-bottom: 24px;
      background-color: #f0f9ff;
      border-radius: 0 6px 6px 0;
    }}
    .next-steps h3 {{
      margin: 0 0 10px 0;
      font-size: 15px;
      color: #0369a1;
    }}
    .next-steps ul {{
      margin: 0;
      padding-left: 20px;
      color: #334155;
      font-size: 14px;
    }}
    .next-steps li {{
      margin-bottom: 6px;
    }}
    .closing {{
      font-size: 15px;
      color: #334155;
      margin-bottom: 24px;
    }}
    .signature {{
      font-size: 14px;
      color: #475569;
      border-top: 1px solid #e2e8f0;
      padding-top: 16px;
    }}
    .footer {{
      background-color: #f8fafc;
      padding: 16px 32px;
      text-align: center;
      font-size: 12px;
      color: #94a3b8;
      border-top: 1px solid #e2e8f0;
    }}
  </style>
</head>
<body>
  <div class="wrapper">
    <div class="container">
      <div class="header">
        <h1>NEXUS <span>AGENT</span> &nbsp;|&nbsp; Enterprise HR</h1>
      </div>
      <div class="content">
        <p class="greeting">Dear {name},</p>
        <p class="intro">
          Welcome to <strong>Nexus Agent</strong>! We are thrilled to confirm that you have successfully joined our organization as a <strong>{designation}</strong> in the <strong>{department} Department</strong>. Your profile has been initialized in our corporate directory.
        </p>

        <div class="card">
          <div class="card-title">Employee Onboarding Profile</div>
          <table class="info-table">
            <tr>
              <td class="info-label">Full Name</td>
              <td class="info-value">{name}</td>
            </tr>
            <tr>
              <td class="info-label">Employee ID</td>
              <td class="info-value">{employee_id}</td>
            </tr>
            <tr>
              <td class="info-label">Department</td>
              <td class="info-value">{department}</td>
            </tr>
            <tr>
              <td class="info-label">Designation / Title</td>
              <td class="info-value">{designation}</td>
            </tr>
            <tr>
              <td class="info-label">Joining Date</td>
              <td class="info-value">{joining_date}</td>
            </tr>
            <tr>
              <td class="info-label">Reporting Manager</td>
              <td class="info-value">{manager}</td>
            </tr>
            <tr>
              <td class="info-label">Official Email</td>
              <td class="info-value">{recipient_email}</td>
            </tr>
          </table>
        </div>

        <div class="next-steps">
          <h3>Next Steps</h3>
          <ul>
            <li>Complete your HR onboarding formalities and submit required identification.</li>
            <li>Meet with your reporting manager ({manager}) for team introduction and guidance.</li>
            <li>Collect your workstation equipment and security access credentials.</li>
            <li>Review company policies and operational guidelines in the employee portal.</li>
          </ul>
        </div>

        <p class="closing">
          Welcome to the team, <strong>{name}</strong>. We look forward to your contributions and wish you a successful journey with Nexus Agent.
        </p>

        <div class="signature">
          Best regards,<br>
          <strong>Nexus Agent | Human Resources</strong>
        </div>
      </div>
      <div class="footer">
        This is an automated operational notification generated by Nexus Agent Enterprise Onboarding Subsystem.
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
            delivery_note = f"SUCCESS: Live corporate HTML email delivered to {recipient_email} via Gmail SMTP ({sender_email})."
        except Exception as e:
            delivery_note = f"Gmail SMTP error: {str(e)}"
    elif is_mock_local:
        delivery_note = f"NOTICE: Live SMTP transmission skipped for local mock address '{recipient_email}'."
    else:
        delivery_note = (
            "NOTICE: Set GMAIL_APP_PASSWORD environment variable for SMTP authentication."
        )

    return {
        "status": "success" if (email_sent or not smtp_password) else "failed",
        "operation": "email.welcome",
        "senderEmail": sender_email,
        "recipientName": name,
        "recipientEmail": recipient_email,
        "department": department,
        "designation": designation,
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
    
    result = generate_welcome_email(args_dict)
    print(json.dumps(result, indent=2))


