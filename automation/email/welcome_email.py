import sys
import json
from datetime import datetime

def generate_welcome_email(args: dict) -> dict:
    name = args.get("name", "New Employee")
    designation = args.get("designation", "Team Member")
    department = args.get("department", "IT")
    
    first_name = name.split()[0] if name else "Team Member"
    email_address = args.get("email") or f"{name.lower().replace(' ', '.')}@nexus.local"
    
    subject = f"Welcome to Nexus Agent Lite - {department} Department!"
    
    body = (
        f"Dear {name},\n\n"
        f"Welcome aboard! We are thrilled to have you join our team as a {designation} in the {department} Department.\n\n"
        f"Your employee profile and credentials have been established in our enterprise directory.\n"
        f"Official Email: {email_address}\n"
        f"Department: {department}\n"
        f"Designation: {designation}\n\n"
        f"Please check in with your department manager to receive your onboarding checklist.\n\n"
        f"Best regards,\n"
        f"Nexus Agent Lite Onboarding System"
    )
    
    return {
        "status": "success",
        "operation": "email.welcome",
        "recipientName": name,
        "recipientEmail": email_address,
        "department": department,
        "designation": designation,
        "subject": subject,
        "body": body,
        "generatedAt": datetime.utcnow().isoformat() + "Z"
    }

if __name__ == "__main__":
    raw_args = sys.argv[1] if len(sys.argv) > 1 else "{}"
    try:
        args_dict = json.loads(raw_args)
    except Exception:
        args_dict = {}
    
    result = generate_welcome_email(args_dict)
    print(json.dumps(result))
