import sys
import json
import random
from datetime import datetime

def create_provisioning_ticket(args: dict) -> dict:
    employee = args.get("employee", args.get("name", "Ahmed Khan"))
    department = args.get("department", "IT")
    request_type = args.get("requestType", "Hardware & Software Provisioning")
    
    ticket_num = random.randint(1000, 9999)
    year = datetime.now().year
    ticket_id = f"TCK-{year}-{ticket_num}"
    
    details = (
        f"Automated IT Ticket created for employee '{employee}' in '{department}'. "
        f"Provisioning items: Workstation laptop, IDE software licenses, and VPN access."
    )
    
    return {
        "status": "success",
        "operation": "ticket.create",
        "ticketId": ticket_id,
        "employee": employee,
        "department": department,
        "requestType": request_type,
        "priority": "High",
        "ticketStatus": "Open",
        "details": details,
        "createdAt": datetime.utcnow().isoformat() + "Z"
    }

if __name__ == "__main__":
    raw_args = sys.argv[1] if len(sys.argv) > 1 else "{}"
    try:
        args_dict = json.loads(raw_args)
    except Exception:
        args_dict = {}
        
    result = create_provisioning_ticket(args_dict)
    print(json.dumps(result))
