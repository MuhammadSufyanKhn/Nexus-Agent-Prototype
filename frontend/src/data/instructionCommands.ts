export interface InstructionCommandItem {
  id: string;
  sectionId: string;
  sectionTitle: string;
  badge: string;
  category: string;
  label: string;
  text: string;
  desc: string;
  intent: string;
  keywords: string[];
  aliases: string[];
}

export const INSTRUCTION_COMMANDS_DATA: InstructionCommandItem[] = [
  // 1. Console Guide Commands
  {
    id: 'inst_c1',
    sectionId: 'console',
    sectionTitle: 'AI Assistant & Natural Language Console',
    badge: 'CORE ENGINE',
    category: 'Instructions Guide',
    label: 'Onboard Sarah Jenkins (Console Guide)',
    text: 'Onboard Sarah Jenkins as Senior Software Engineer in Engineering with a salary of $120,000. and send welcome email at [your email]',
    desc: 'Triggers full onboarding workflow, salary band validation, and credentials setup.',
    intent: 'EMPLOYEE_ONBOARDING',
    keywords: [
      'onboard', 'onboarding', 'sarah', 'jenkins', 'sarah jenkins', 'senior software engineer',
      'engineering', '120000', '120,000', 'salary', 'welcome email', 'email'
    ],
    aliases: [
      'Onboard Sarah Jenkins as Senior Software Engineer',
      'Onboard Sarah Jenkins with salary 120k and send welcome email',
      'onboard sarah jenkins in engineering'
    ]
  },
  {
    id: 'inst_c2',
    sectionId: 'console',
    sectionTitle: 'AI Assistant & Natural Language Console',
    badge: 'CORE ENGINE',
    category: 'Instructions Guide',
    label: 'Allocate $150,000 Budget to IT for Q3 (Console Guide)',
    text: 'Allocate $150,000 budget to the IT department for Q3.',
    desc: 'Increases quarterly department budget pool from master allocation.',
    intent: 'BUDGET_UPDATE',
    keywords: [
      'allocate', 'alloc', 'budget', '150000', '150k', 'it', 'it department', 'q3', 'quarter 3'
    ],
    aliases: [
      'Allocate $150,000 budget to IT',
      'Add 150000 budget to IT for Q3',
      'allocate budget to it department'
    ]
  },
  {
    id: 'inst_c3',
    sectionId: 'console',
    sectionTitle: 'AI Assistant & Natural Language Console',
    badge: 'CORE ENGINE',
    category: 'Instructions Guide',
    label: 'Increase IT Budget by Amount (Console Guide)',
    text: 'Increase IT budget by [your amount] for Q3.',
    desc: 'Transfers budget allocation between department nodes seamlessly.',
    intent: 'BUDGET_UPDATE',
    keywords: [
      'increase', 'increase it budget', 'it budget', 'budget', 'amount', 'q3', 'budget increase'
    ],
    aliases: [
      'Increase IT department budget for Q3',
      'Increase IT budget by amount',
      'add budget to it department'
    ]
  },
  {
    id: 'inst_c4',
    sectionId: 'console',
    sectionTitle: 'AI Assistant & Natural Language Console',
    badge: 'CORE ENGINE',
    category: 'Instructions Guide',
    label: "Log Marcus's Sick Day (Console Guide)",
    text: "Log Marcus's sick day today and notify his team on Slack.",
    desc: 'Records leave entry in attendance log and triggers team notifications.',
    intent: 'LEAVE_CREATE',
    keywords: [
      'log', 'marcus', "marcus's", 'sick', 'sick day', 'sick day today', 'slack', 'notify slack', 'leave', 'attendance'
    ],
    aliases: [
      "Log Marcus's sick day today",
      "Marcus is sick today notify on Slack",
      "Record sick leave for Marcus"
    ]
  },

  // 2. Talent Acquisition & CV Resume Screening
  {
    id: 'inst_j1',
    sectionId: 'jobs_cv',
    sectionTitle: 'Talent Acquisition & CV Resume Screening',
    badge: 'RECRUITMENT',
    category: 'Instructions Guide',
    label: 'Job Requisition Template for .NET Developer',
    text: 'Create a new job opening for .NET Developer in IT department with location Remote / Hybrid, salary $50,000 - $60,000. Role Overview: Lead enterprise architecture, cloud modernization, and system scalability for IT department. Key Technical Requirements: ASP.NET, C#, Entity Framework, Web API development, Database Management, SQL, LINQ. Core Responsibilities: Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles. • Collaborate across multidisciplinary engineering, UX, and AI agent automation pods. • Optimize query execution, conduct peer code reviews, and champion continuous automated testing.',
    desc: 'Creates a new job opening with full overview, requirements, and responsibilities.',
    intent: 'JOB_OPENING_CREATE',
    keywords: [
      'create', 'create a new job opening', 'job opening', '.net developer', 'net developer', 'job template',
      'requisition', 'it department', 'remote', 'hybrid', '50000', '60000', 'asp.net', 'c#'
    ],
    aliases: [
      'Create job opening for .NET Developer in IT',
      'Post .NET Developer job requisition',
      'Job requisition template prompt'
    ]
  },
  {
    id: 'inst_j2',
    sectionId: 'jobs_cv',
    sectionTitle: 'Talent Acquisition & CV Resume Screening',
    badge: 'RECRUITMENT',
    category: 'Instructions Guide',
    label: 'Screen Candidate Resume Fit Score (Instructions Guide)',
    text: 'Screen candidate resume fit score for Senior Full Stack Developer position.',
    desc: 'Runs AI evaluation on submitted candidate CVs and outputs match score.',
    intent: 'CV_SCREEN',
    keywords: [
      'screen', 'screen candidate', 'resume', 'fit score', 'resume fit score', 'senior full stack developer',
      'cv', 'cv screening', 'candidate cv'
    ],
    aliases: [
      'Screen candidate resume fit score',
      'Score candidate resumes for Full Stack Developer',
      'Screen CV for Senior Full Stack Developer'
    ]
  },
  {
    id: 'inst_j3',
    sectionId: 'jobs_cv',
    sectionTitle: 'Talent Acquisition & CV Resume Screening',
    badge: 'RECRUITMENT',
    category: 'Instructions Guide',
    label: 'Generate Interview Question Recommendations for Hammad',
    text: "Generate interview question recommendations based on Hammad's CV and the Database Administrator position he applied for",
    desc: 'Produces role-tailored technical & behavioral interview questions based on candidate CV and applied position.',
    intent: 'CV_SCREEN',
    keywords: [
      'generate', 'generate interview question recommendations', 'interview', 'interview question recommendations', 'hammad', "hammad's", "hammad's cv", 'database administrator', 'applied for', 'recommendations', 'cv'
    ],
    aliases: [
      "Generate interview question recommendations based on Hammad's CV and the Database Administrator position he applied for",
      "Generate interview question recommendations based on Hammad's CV",
      "Recommend interview questions for Hammad Database Administrator",
      "Generate interview question recommendations based on candidate CV."
    ]
  },
  {
    id: 'inst_j4',
    sectionId: 'jobs_cv',
    sectionTitle: 'Talent Acquisition & CV Resume Screening',
    badge: 'RECRUITMENT',
    category: 'Instructions Guide',
    label: 'Screen Resume Against React Requirements',
    text: 'Screen candidate resume against Senior React Developer position requirements.',
    desc: 'Compares applicant skills in React, TypeScript, and state management.',
    intent: 'CV_SCREEN',
    keywords: [
      'screen', 'screen candidate resume', 'react', 'senior react developer', 'react requirements', 'resume against react', 'react developer'
    ],
    aliases: [
      'Screen candidate resume against Senior React Developer',
      'Evaluate React candidate CV',
      'Match resume to React Developer role'
    ]
  },

  // 3. Employee Directory & Attendance / Leave Management
  {
    id: 'inst_e1',
    sectionId: 'employees',
    sectionTitle: 'Employee Directory & Attendance / Leave',
    badge: 'WORKFORCE MANAGEMENT',
    category: 'Instructions Guide',
    label: 'Show Active Employees in Engineering',
    text: 'Show all active employees in the Engineering department.',
    desc: 'Lists engineering team members, designations, and assigned managers.',
    intent: 'EMPLOYEE_READ',
    keywords: [
      'show', 'show all active employees', 'active employees', 'engineering', 'engineering department', 'list employees'
    ],
    aliases: [
      'Show active employees in Engineering',
      'List all Engineering employees',
      'View active employees in Engineering department'
    ]
  },
  {
    id: 'inst_e2',
    sectionId: 'employees',
    sectionTitle: 'Employee Directory & Attendance / Leave',
    badge: 'WORKFORCE MANAGEMENT',
    category: 'Instructions Guide',
    label: 'Find Employee Records for Sufyan',
    text: 'Find employee records for sufyan, display current designation, and retrieve current annual salary',
    desc: 'Fetches comprehensive employee profile with current designation and annual salary details.',
    intent: 'EMPLOYEE_READ',
    keywords: [
      'find', 'find employee records for sufyan', 'sufyan', 'designation', 'annual salary', 'current designation', 'retrieve current annual salary', 'salary', 'records for sufyan'
    ],
    aliases: [
      'Find employee records for sufyan, display current designation, and retrieve current annual salary',
      'Find employee records for sufyan',
      'Show sufyan designation and current annual salary',
      'Find employee records for Sarah Ahmed and show current designation and salary.'
    ]
  },
  {
    id: 'inst_e3',
    sectionId: 'employees',
    sectionTitle: 'Employee Directory & Attendance / Leave',
    badge: 'WORKFORCE MANAGEMENT',
    category: 'Instructions Guide',
    label: 'Update Designation for Ali Khan to Senior .NET Developer',
    text: 'Update Designation for Ali Khan to Senior .NET Developer.',
    desc: 'Updates job title in SQL database and records designation history.',
    intent: 'EMPLOYEE_UPDATE',
    keywords: [
      'update', 'update designation', 'ali khan', 'senior .net developer', 'ali', 'designation', 'job title', 'promote'
    ],
    aliases: [
      'Update Ali Khan designation to Senior .NET Developer',
      'Promote Ali Khan to Senior .NET Developer',
      'Change Ali Khan title to Senior .NET Developer'
    ]
  },
  {
    id: 'inst_e4',
    sectionId: 'employees',
    sectionTitle: 'Employee Directory & Attendance / Leave',
    badge: 'WORKFORCE MANAGEMENT',
    category: 'Instructions Guide',
    label: 'Log Sick Day and Notify on Gmail',
    text: 'Log [name] sick day today and notify his team on gmail.',
    desc: 'Registers full-day sick leave entry in HR attendance logs.',
    intent: 'LEAVE_CREATE',
    keywords: [
      'log', 'log sick day', 'sick day today', 'gmail', 'notify on gmail', 'leave', 'attendance', 'sick leave'
    ],
    aliases: [
      'Log sick day today and notify team on gmail',
      'Register sick leave and send gmail notification',
      'Record employee sick day and email team'
    ]
  },

  // 4. Department Operations & Master Corporate Budgeting
  {
    id: 'inst_d1',
    sectionId: 'departments',
    sectionTitle: 'Department Operations & Master Budgeting',
    badge: 'FINANCIAL GOVERNANCE',
    category: 'Instructions Guide',
    label: 'Create AI Innovations Department with Head Tariq Mahmood',
    text: 'Create AI Innovations department with head Tariq Mahmood',
    desc: 'Establishes a new department node with assigned department head.',
    intent: 'DEPARTMENT_CREATE',
    keywords: [
      'create', 'create ai innovations', 'ai innovations', 'tariq mahmood', 'department', 'head', 'head tariq mahmood', 'new department'
    ],
    aliases: [
      'Create AI Innovations department with Tariq Mahmood as head',
      'Add new department AI Innovations head Tariq Mahmood',
      'Create department AI Innovations'
    ]
  },
  {
    id: 'inst_d2',
    sectionId: 'departments',
    sectionTitle: 'Department Operations & Master Budgeting',
    badge: 'FINANCIAL GOVERNANCE',
    category: 'Instructions Guide',
    label: 'Allocate $150,000 Budget to IT Department for Q3',
    text: 'Allocate $150,000 budget to the IT department for Q3.',
    desc: 'Increases quarterly department budget pool from master allocation.',
    intent: 'BUDGET_UPDATE',
    keywords: [
      'allocate', 'budget', '150,000', '150000', 'it department', 'q3', 'quarterly budget', 'master allocation'
    ],
    aliases: [
      'Allocate 150000 to IT department for Q3',
      'Increase IT budget by 150,000 in Q3',
      'Allocate budget to IT department'
    ]
  },
  {
    id: 'inst_d3',
    sectionId: 'departments',
    sectionTitle: 'Department Operations & Master Budgeting',
    badge: 'FINANCIAL GOVERNANCE',
    category: 'Instructions Guide',
    label: 'Show Departments with Allocated Budgets Over Amount',
    text: 'Show departments with allocated budgets over [amount].',
    desc: 'Lists departments with more budget than the amount.',
    intent: 'BUDGET_READ',
    keywords: [
      'show', 'show departments', 'allocated budgets', 'budgets over', 'budget amount', 'filter departments', 'budget list'
    ],
    aliases: [
      'Show departments with budget over amount',
      'List departments with allocated budget over 100k',
      'Find departments with budget greater than amount'
    ]
  },
  {
    id: 'inst_d4',
    sectionId: 'departments',
    sectionTitle: 'Department Operations & Master Budgeting',
    badge: 'FINANCIAL GOVERNANCE',
    category: 'Instructions Guide',
    label: 'Calculate Average Employee Salary in Engineering',
    text: 'Calculate average employee salary in the Engineering department.',
    desc: 'Calculates average salary of employees in the Engineering department',
    intent: 'SQL_AGENT',
    keywords: [
      'calculate', 'calculate average', 'average salary', 'average employee salary', 'engineering', 'engineering department', 'salary'
    ],
    aliases: [
      'Calculate average salary in Engineering department',
      'What is the average employee salary in Engineering',
      'Average compensation in Engineering'
    ]
  },

  // 5. HR Policy Center & Expense Compliance
  {
    id: 'inst_p1',
    sectionId: 'policies_expenses',
    sectionTitle: 'HR Policy Center & Expense Compliance',
    badge: 'COMPLIANCE',
    category: 'Instructions Guide',
    label: 'Run AI Policy Compliance Sweep on Expense Claims',
    text: 'Run AI Policy Compliance Sweep on all submitted employee expense claims.',
    desc: 'Audits pending claims against meal ($50) & travel ($250) limits.',
    intent: 'EXPENSE_COMPLIANCE_SWEEP',
    keywords: [
      'run', 'run ai policy compliance sweep', 'compliance sweep', 'expense claims', 'sweep', 'audit expenses', 'policy sweep', 'expenses'
    ],
    aliases: [
      'Run AI policy compliance sweep on all expense claims',
      'Audit all submitted employee expense claims',
      'Check expenses for policy violations'
    ]
  },
  {
    id: 'inst_p2',
    sectionId: 'policies_expenses',
    sectionTitle: 'HR Policy Center & Expense Compliance',
    badge: 'COMPLIANCE',
    category: 'Instructions Guide',
    label: 'Display Remote Work & Home Office Stipend Policy',
    text: 'Display remote work and home office equipment stipend policy.',
    desc: 'Retrieves full remote work policy text and equipment allowance rules.',
    intent: 'POLICY_READ',
    keywords: [
      'display', 'display remote work', 'remote work', 'home office', 'stipend policy', 'equipment stipend', 'remote work policy', 'policy'
    ],
    aliases: [
      'Show remote work and home office stipend policy',
      'What is the remote work equipment stipend policy',
      'Get remote work policy details'
    ]
  },
  {
    id: 'inst_p3',
    sectionId: 'policies_expenses',
    sectionTitle: 'HR Policy Center & Expense Compliance',
    badge: 'COMPLIANCE',
    category: 'Instructions Guide',
    label: 'Explain Policy for Meal',
    text: 'explain policy for meal',
    desc: 'Returns the reimbursement rules for meals and dining expenses',
    intent: 'POLICY_READ',
    keywords: [
      'explain', 'explain policy for meal', 'meal', 'meal policy', 'dining policy', 'meal cap', 'meal reimbursement', 'policy'
    ],
    aliases: [
      'Explain meal reimbursement policy',
      'What is the policy for meal expenses',
      'Show meal cap limit policy'
    ]
  },
  {
    id: 'inst_p4',
    sectionId: 'policies_expenses',
    sectionTitle: 'HR Policy Center & Expense Compliance',
    badge: 'COMPLIANCE',
    category: 'Instructions Guide',
    label: 'Show Corporate Compensation Policy POL-HR-001',
    text: 'Show the current corporate compensation policy POL-HR-001',
    desc: 'Retrieves policy handbook guidelines for Compensation Policy (POL-HR-001).',
    intent: 'POLICY_READ',
    keywords: [
      'show', 'show the current corporate compensation policy', 'compensation policy', 'pol-hr-001', 'pol hr 001', 'salary band policy', 'compensation'
    ],
    aliases: [
      'Show compensation policy POL-HR-001',
      'View corporate compensation policy POL-HR-001',
      'What is policy POL-HR-001'
    ]
  },
  {
    id: 'inst_p5',
    sectionId: 'policies_expenses',
    sectionTitle: 'HR Policy Center & Expense Compliance',
    badge: 'EXPENSE',
    category: 'Instructions Guide',
    label: 'Submit Meal Expense Claim for Client Dinner',
    text: 'Submit meal expense claim of $40.00 for Client Dinner under Khattak',
    desc: 'Submits a $40 client dinner meal claim under employee Khattak and evaluates against POL-FIN-002.',
    intent: 'EXPENSE_CREATE',
    keywords: [
      'submit', 'submit meal expense', 'meal expense claim', 'client dinner', 'khattak', 'expense claim', '40.00', 'dining claim'
    ],
    aliases: [
      'Submit meal claim of $40 for Client Dinner under Khattak',
      'Add meal expense claim for Khattak $40',
      'Record client dinner expense for Khattak'
    ]
  },

  // 6. Employee Onboarding Hub & Email Automation
  {
    id: 'inst_o1',
    sectionId: 'onboarding',
    sectionTitle: 'Employee Onboarding Hub & Email Automation',
    badge: 'ONBOARDING',
    category: 'Instructions Guide',
    label: 'Onboard Sarah Jenkins in Engineering ($120,000)',
    text: 'Onboard Sarah Jenkins as Senior Software Engineer in Engineering at $120,000.',
    desc: 'Initializes employee record, policy verification, and onboarding portal.',
    intent: 'EMPLOYEE_ONBOARDING',
    keywords: [
      'onboard', 'onboard sarah jenkins', 'sarah jenkins', 'senior software engineer', 'engineering', '120,000', '120000', 'onboarding'
    ],
    aliases: [
      'Onboard Sarah Jenkins as Senior Software Engineer in Engineering',
      'Add new hire Sarah Jenkins with salary 120k',
      'Start onboarding for Sarah Jenkins'
    ]
  },
  {
    id: 'inst_o2',
    sectionId: 'onboarding',
    sectionTitle: 'Employee Onboarding Hub & Email Automation',
    badge: 'ONBOARDING',
    category: 'Instructions Guide',
    label: 'Resend Official Onboarding Welcome Email',
    text: 'Resend official onboarding welcome email to Ahmed Khan at ahmed@company.com.',
    desc: 'Triggers official onboarding welcome communication email dispatch.',
    intent: 'ONBOARDING_READ',
    keywords: [
      'resend', 'resend official onboarding welcome email', 'welcome email', 'ahmed khan', 'ahmed@company.com', 'email dispatch', 'onboarding email'
    ],
    aliases: [
      'Resend welcome email to Ahmed Khan',
      'Send onboarding email to ahmed@company.com',
      'Trigger welcome email dispatch for Ahmed Khan'
    ]
  },
  {
    id: 'inst_o3',
    sectionId: 'onboarding',
    sectionTitle: 'Employee Onboarding Hub & Email Automation',
    badge: 'ONBOARDING',
    category: 'Instructions Guide',
    label: 'Generate Complete Onboarding Package Document for Ali Ahmed',
    text: 'Generate complete Onboarding Package document for new hire Ali Ahmed',
    desc: 'Renders formal HR appointment letter & policy document packet for new hire Ali Ahmed.',
    intent: 'EMPLOYEE_ONBOARDING',
    keywords: [
      'generate', 'generate complete onboarding package document', 'onboarding package', 'ali ahmed', 'new hire ali ahmed', 'new hire', 'appointment letter', 'onboarding document'
    ],
    aliases: [
      'Generate complete Onboarding Package document for new hire Ali Ahmed',
      'Generate onboarding package document for Ali Ahmed',
      'Create appointment letter and onboarding pack for Ali Ahmed',
      'Generate complete Onboarding Package document for new hire Ali Khan.'
    ]
  },
  {
    id: 'inst_o4',
    sectionId: 'onboarding',
    sectionTitle: 'Employee Onboarding Hub & Email Automation',
    badge: 'ONBOARDING',
    category: 'Instructions Guide',
    label: 'Find Employee Records for Ahmed Khan',
    text: 'Find employee records for Ahmed Khan, display current designation, and retrieve current annual salary',
    desc: 'Fetches comprehensive employee profile with designation and annual salary details.',
    intent: 'EMPLOYEE_READ',
    keywords: [
      'find', 'find employee records for ahmed khan', 'ahmed khan', 'designation', 'annual salary', 'current designation', 'retrieve salary', 'ahmed'
    ],
    aliases: [
      'Find Ahmed Khan employee profile, designation and salary',
      'Show Ahmed Khan designation and current annual salary',
      'Retrieve employee details for Ahmed Khan'
    ]
  },
  // 7. Enterprise HR Intelligence & Document Artifacts
  {
    id: 'inst_rep1',
    sectionId: 'reports_documents',
    sectionTitle: 'Enterprise HR Intelligence & Document Artifacts',
    badge: 'EXECUTIVE REPORT',
    category: 'Instructions Guide',
    label: 'Generate Comprehensive HR Report',
    text: 'Generate comprehensive HR reports covering workforce, recruitment, compensation, expenses, budgets, compliance, onboarding, staffing, and employee data.',
    desc: 'Compiles multi-domain 16-section executive HR report grounded in live database data.',
    intent: 'DASHBOARD_ANALYTICS',
    keywords: [
      'generate', 'comprehensive', 'hr reports', 'workforce', 'recruitment', 'compensation', 'expenses', 'budgets', 'compliance', 'onboarding', 'staffing', 'employee data', 'report'
    ],
    aliases: [
      'Generate comprehensive HR reports covering workforce, recruitment, compensation, expenses, budgets, compliance, onboarding, staffing, and employee data.',
      'Generate comprehensive HR report',
      'Generate complete multi-section HR report'
    ]
  },
  {
    id: 'inst_rep2',
    sectionId: 'reports_documents',
    sectionTitle: 'Enterprise HR Intelligence & Document Artifacts',
    badge: 'INDUCTION SCHEDULE',
    category: 'Instructions Guide',
    label: 'Generate 5-Day Summer Intern Orientation Schedule',
    text: 'Generate 5-day workforce orientation and induction schedule for summer engineering interns in IT.',
    desc: 'Compiles official 5-day orientation framework and technical induction curriculum for IT interns.',
    intent: 'ONBOARDING_DOCUMENT_GENERATE',
    keywords: [
      'generate', '5-day', 'workforce orientation', 'induction schedule', 'summer engineering interns', 'it', 'interns', 'orientation schedule', '5 day'
    ],
    aliases: [
      'Generate 5-day workforce orientation and induction schedule for summer engineering interns in IT.',
      'Generate 5-day orientation schedule for interns in IT',
      'Generate summer engineering intern induction schedule'
    ]
  },
  {
    id: 'inst_rep3',
    sectionId: 'reports_documents',
    sectionTitle: 'Enterprise HR Intelligence & Document Artifacts',
    badge: 'EXPENSE AUDIT',
    category: 'Instructions Guide',
    label: 'Generate Corporate Expense Audit & Compliance Report',
    text: 'Generate corporate expense audit and compliance report.',
    desc: 'Generates corporate expense audit with KPI metrics, category analysis, POL-FIN-002 violations, and claims ledger.',
    intent: 'EXPENSE_READ',
    keywords: [
      'generate', 'corporate expense audit', 'compliance report', 'expense audit', 'pol-fin-002', 'claims ledger', 'audit report'
    ],
    aliases: [
      'Generate corporate expense audit and compliance report.',
      'Generate corporate expense audit',
      'Create expense compliance audit report'
    ]
  },
  {
    id: 'inst_rep4',
    sectionId: 'reports_documents',
    sectionTitle: 'Enterprise HR Intelligence & Document Artifacts',
    badge: 'EXPENSE FILTER',
    category: 'Instructions Guide',
    label: 'Show Meal Expense Claims Exceeding $20 Daily Limit',
    text: 'Show all meal expense claims exceeding the $20 daily limit.',
    desc: 'Filters meal claims against a custom $20.00 daily threshold and computes policy variance.',
    intent: 'EXPENSE_READ',
    keywords: [
      'show', 'meal expense claims', 'exceeding', '$20', 'daily limit', 'variance', 'meal limit'
    ],
    aliases: [
      'Show all meal expense claims exceeding the $20 daily limit.',
      'Show meal claims over $20',
      'List meal expenses exceeding $20 daily limit'
    ]
  }
];
