import sys
import json
import os

current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

from email_services.welcome_email import generate_welcome_email
from tickets.create_ticket import create_provisioning_ticket
from browser.legacy_hr_portal import submit_legacy_hr_form

def sanitize_json(raw: str) -> dict:
    if not raw:
        return {}
    raw = raw.strip()
    if (raw.startswith("'") and raw.endswith("'")) or (raw.startswith('"') and raw.endswith('"')):
        raw = raw[1:-1]
    
    raw = raw.replace('\\"', '"')
    
    try:
        return json.loads(raw)
    except Exception:
        return {}

def main():
    if len(sys.argv) < 2:
        error_res = {
            "status": "error",
            "message": "Usage: python runner.py <operation_name> '<json_arguments>'"
        }
        print(json.dumps(error_res))
        sys.exit(0)
        
    operation = sys.argv[1].lower()
    raw_args = " ".join(sys.argv[2:]) if len(sys.argv) > 2 else "{}"
    args_dict = sanitize_json(raw_args)
        
    if operation in ["email.welcome", "email.generate_welcome", "welcome_email"]:
        result = generate_welcome_email(args_dict)
    elif operation in ["ticket.create", "create_ticket"]:
        result = create_provisioning_ticket(args_dict)
    elif operation in ["onboarding.submit_legacy_form", "portal.legacy_submit", "submit_legacy_form"]:
        result = submit_legacy_hr_form(args_dict)
    else:
        result = {
            "status": "error",
            "message": f"Unrecognized or unregistered automation operation '{operation}'. Arbitrary code execution is strictly rejected."
        }
        print(json.dumps(result))
        sys.exit(0)
        
    print(json.dumps(result))

if __name__ == "__main__":
    main()
