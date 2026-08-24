import sys
import json
import os
import random
from datetime import datetime

def submit_legacy_hr_form(args: dict) -> dict:
    name = args.get("employeeName", args.get("name", "Ahmed Khan"))
    department = args.get("department", "IT")
    designation = args.get("designation", "Mid-Level .NET Developer")
    salary = args.get("salary", 68000)
    manager = args.get("manager", "Tariq Mahmood")

    random_num = random.randint(1000, 9999)
    rec_id = f"HR-REC-2026-{random_num}"

    return {
        "status": "success",
        "operation": "onboarding.submit_legacy_form",
        "portalRecordId": rec_id,
        "employeeName": name,
        "department": department,
        "designation": designation,
        "salary": salary,
        "manager": manager,
        "message": f"Successfully submitted employee '{name}' into Legacy HR Portal.",
        "automationEngine": "Automated Workforce Form Engine",
        "timestamp": datetime.utcnow().isoformat() + "Z"
    }

if __name__ == "__main__":
    raw_args = sys.argv[1] if len(sys.argv) > 1 else "{}"
    try:
        args_dict = json.loads(raw_args)
    except Exception:
        args_dict = {}
        
    result = submit_legacy_hr_form(args_dict)
    print(json.dumps(result))
