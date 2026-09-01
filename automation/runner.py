import sys
import json
import os

current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

from email_services.welcome_email import generate_welcome_email
from email_services.sick_leave_email import generate_sick_leave_email
from email_services.application_acknowledgment_email import generate_application_acknowledgment_email
from email_services.interview_invitation_email import generate_interview_invitation_email
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
        pass

    try:
        import ast
        val = ast.literal_eval(raw)
        if isinstance(val, dict):
            return val
    except Exception:
        pass

    import re
    try:
        clean = raw.strip("{}")
        res = {}
        items = re.findall(r'([a-zA-Z0-9_]+)\s*:\s*([^,{}]+)', clean)
        for k, v in items:
            val_str = v.strip().strip('"\'')
            res[k.strip()] = val_str
        if res:
            return res
    except Exception:
        pass

    try:
        fixed = re.sub(r'([{,]\s*)([a-zA-Z0-9_]+)\s*:', r'\1"\2":', raw)
        return json.loads(fixed)
    except Exception:
        pass

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
    elif operation in ["email.sick_leave", "email.notify", "slack.notify", "sick_leave_email"]:
        result = generate_sick_leave_email(args_dict)
    elif operation in ["email.application_acknowledgment", "email.candidate_ack", "candidate_application_email"]:
        result = generate_application_acknowledgment_email(args_dict)
    elif operation in ["email.interview_invitation", "email.interview", "interview_invitation"]:
        result = generate_interview_invitation_email(args_dict)
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
