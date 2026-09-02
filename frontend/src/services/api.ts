export interface ConfirmationDetails {
  proposedAction: string;
  actionSummary: string;
  requiresUserAction: boolean;
}

export interface NextActionChoice {
  id: string;
  label: string;
  actionType: 'NAVIGATE' | 'EXECUTE_PROMPT' | 'OPEN_URL' | 'MODAL';
  targetTab?: string;
  promptToExecute?: string;
  url?: string;
  context?: Record<string, string>;
}

export interface AgentResult {
  runId: string;
  originalPrompt: string;
  intent: string;
  isSuccess: boolean;
  requiresApproval?: boolean;
  actionPlan?: ActionPlan;
  resultData?: any;
  executionFeed: AgentEvent[];
  errorMessage?: string;
  /** Set when the Gemini API itself failed — distinct from a business error */
  llmError?: string;
  executionTimeMs: number;
  choices?: NextActionChoice[];

  // ── Spec-aligned Workflow State Machine fields ────────────────────────────
  /** One of: CONFIRMATION_REQUIRED | CLARIFICATION_REQUIRED | READY_TO_EXECUTE | ANSWER_DIRECT */
  state?: string;
  /** The clear, concise message displayed directly on the UI to the user */
  userMessage?: string;
  /** Summary of the proposed action shown to the user before execution */
  confirmationDetails?: ConfirmationDetails;
  /** Target backend system: SQL_SERVER | N8N_WORKFLOW | ZAPIER | UNKNOWN */
  targetSystem?: string;
  /** Populated when state is READY_TO_EXECUTE. Contains the structured execution payload. */
  executionPayload?: any;
}


export interface AgentEvent {
  eventType: string;
  timestamp: string;
  message: string;
  details?: string;
}

export interface AgentActivityEvent {
  eventId: string;
  agentRunId: string;
  stepNumber: number;
  eventType: string;
  message: string;
  status: string;
  timestamp: string;
  details?: string;
}

export interface ChangePreview {
  fieldName: string;
  oldValue: string;
  newValue: string;
  difference?: string;
  valueSource?: string;
  isEditable?: boolean;
}

export interface AffectedRecord {
  recordId: number;
  entityName: string;
  primaryLabel: string;
  changes: ChangePreview[];
}

export interface ActionPlanStep {
  stepNumber: number;
  toolName: string;
  description: string;
  riskLevel: number;
}

export interface ActionPlan {
  approvalId: string;
  title: string;
  riskLevel: number;
  status: string;
  totalFinancialImpact: number;
  affectedCount: number;
  affectedRecords: AffectedRecord[];
  steps: ActionPlanStep[];
  warnings: string[];
}

export interface PendingApproval {
  id: string;
  agentRunId: string;
  riskLevel: number;
  requestedBy: string;
  status: number;
  reason: string;
  createdAt: string;
}

export interface Employee {
  id: number;
  name: string;
  email: string;
  departmentId: number;
  departmentName?: string;
  designation: string;
  salary: number;
  experienceYears: number;
  status: number;
  statusName: string;
  createdAt: string;
}

export interface Department {
  id: number;
  name: string;
  description?: string;
  employeeCount?: number;
  allocatedBudget?: number;
  actualSpent?: number;
  remainingBudget?: number;
  headOfDepartment?: string;
}

export interface Budget {
  id: number;
  departmentId: number;
  departmentName?: string;
  quarter: string;
  year: number;
  allocatedAmount: number;
  actualAmount: number;
  spentAmount?: number;
  status?: string;
}

export interface Expense {
  id: number;
  claimNumber?: string;
  employeeId: number;
  employeeName?: string;
  departmentName?: string;
  category: string;
  amount: number;
  description?: string;
  status: number | string;
  statusName?: string;
  complianceStatus?: string;
  policyLimit?: number;
  variance?: number;
  flagReason?: string;
  reviewedBy?: string;
  reviewedDate?: string;
  expenseDate?: string;
  submittedDate?: string;
  submittedAt?: string;
}

export interface AuditLogRecord {
  id: number;
  user: string;
  action: string;
  resource: string;
  tool: string;
  timestamp: string;
  hash: string;
}

export interface LLMHealthStatus {
  isAvailable: boolean;
  modelName: string;
  provider: string;
  responseLatencyMs: number;
  statusMessage: string;
}

export interface SqlAnalyticsResult {
  query: string;
  columns: string[];
  rows: any[];
  summary: string;
  keyInsights: string[];
}

export interface ComplianceResult {
  employeeName?: string;
  claimedAmount?: number;
  allowedAmount?: number;
  difference?: number;
  status: string;
  policySource?: string;
  reason?: string;
}

const API_BASE = '/api';

export async function executeAgent(prompt: string, userRole: string = 'Admin'): Promise<AgentResult> {
  const res = await fetch(`${API_BASE}/agent/execute`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prompt, userRole })
  });

  if (!res.ok) {
    const errorText = await res.text();
    try {
      return JSON.parse(errorText);
    } catch {
      throw new Error(`Execution failed: ${res.statusText}`);
    }
  }

  return res.json();
}

export const executeAgentPrompt = executeAgent;

export async function fetchPendingApprovals(): Promise<PendingApproval[]> {
  const res = await fetch(`${API_BASE}/approval/pending`);
  if (!res.ok) return [];
  return res.json();
}

export async function decideApproval(
  reqOrId: { approvalId: string; approved: boolean; approvedBy?: string; reason?: string; editedParameters?: Record<string, any> } | string,
  approvedArg?: boolean,
  approvedByArg: string = 'Executive Admin',
  reasonArg?: string,
  editedParametersArg?: Record<string, any>
): Promise<any> {
  let bodyObj: any;
  if (typeof reqOrId === 'object') {
    bodyObj = reqOrId;
  } else {
    bodyObj = {
      approvalId: reqOrId,
      approved: approvedArg,
      approvedBy: approvedByArg,
      reason: reasonArg,
      editedParameters: editedParametersArg
    };
  }

  const res = await fetch(`${API_BASE}/approval/decide`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(bodyObj)
  });

  return res.json();
}

export async function fetchRunActivityEvents(runId: string): Promise<AgentActivityEvent[]> {
  const res = await fetch(`${API_BASE}/agentfeed/runs/${runId}/events`);
  if (!res.ok) return [];
  return res.json();
}

export async function fetchEmployees(): Promise<Employee[]> {
  const res = await fetch(`${API_BASE}/employees`);
  if (!res.ok) return [];
  return res.json();
}

export async function fetchDepartments(): Promise<Department[]> {
  const res = await fetch(`${API_BASE}/departments`);
  if (!res.ok) return [];
  return res.json();
}

export interface MasterBudgetInfo {
  year: number;
  fiscalYear: string;
  totalBudgetPool: number;
  totalAllocatedAcrossDepartments: number;
  remainingUnallocatedPool: number;
  updatedAt: string;
}

export async function fetchMasterBudget(): Promise<MasterBudgetInfo> {
  const res = await fetch(`${API_BASE}/budgets/master`);
  if (!res.ok) {
    return {
      year: 2026,
      fiscalYear: "2026-2027",
      totalBudgetPool: 1000000000,
      totalAllocatedAcrossDepartments: 0,
      remainingUnallocatedPool: 1000000000,
      updatedAt: new Date().toISOString()
    };
  }
  return res.json();
}

export async function fetchBudgets(): Promise<Budget[]> {
  const res = await fetch(`${API_BASE}/budgets`);
  if (!res.ok) return [];
  return res.json();
}

export async function fetchExpenses(): Promise<Expense[]> {
  const res = await fetch(`${API_BASE}/expenses`);
  if (!res.ok) return [];
  return res.json();
}

export async function fetchAuditLogs(): Promise<AuditLogRecord[]> {
  const res = await fetch(`${API_BASE}/audit/logs`);
  if (!res.ok) return [];
  return res.json();
}

export interface PolicyItem {
  id: number;
  code: string;
  title: string;
  category: string;
  contentSummary: string;
  documentPath?: string;
  isActive: boolean;
  updatedAt: string;
}

export async function fetchPolicies(category?: string, search?: string): Promise<PolicyItem[]> {
  const params = new URLSearchParams();
  if (category) params.append('category', category);
  if (search) params.append('search', search);
  const url = `${API_BASE}/policies${params.toString() ? `?${params.toString()}` : ''}`;
  const res = await fetch(url);
  if (!res.ok) return [];
  return res.json();
}

export async function createPolicy(data: Partial<PolicyItem>): Promise<PolicyItem> {
  const res = await fetch(`${API_BASE}/policies`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) throw new Error('Failed to create policy');
  return res.json();
}

export async function updatePolicy(id: number, data: Partial<PolicyItem>): Promise<PolicyItem> {
  const res = await fetch(`${API_BASE}/policies/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) throw new Error('Failed to update policy');
  return res.json();
}

export async function deletePolicy(id: number): Promise<boolean> {
  const res = await fetch(`${API_BASE}/policies/${id}`, {
    method: 'DELETE'
  });
  return res.ok;
}

export async function uploadPolicyFile(file: File): Promise<{ documentPath: string; fileName: string }> {
  const formData = new FormData();
  formData.append('file', file);
  const res = await fetch(`${API_BASE}/policies/upload`, {
    method: 'POST',
    body: formData
  });
  if (!res.ok) throw new Error('Failed to upload document file');
  return res.json();
}

export async function checkLLMHealth(): Promise<LLMHealthStatus> {
  try {
    const res = await fetch(`${API_BASE}/llm/health`);
    if (!res.ok) throw new Error("Offline");
    return res.json();
  } catch {
    return {
      isAvailable: false,
      modelName: "Ollama / LocalAI",
      provider: "Local LLM Runtime",
      responseLatencyMs: 0,
      statusMessage: "Local LLM server is offline or unreachable."
    };
  }
}

export async function fetchLeaves(employeeId?: number): Promise<any[]> {
  const url = employeeId ? `${API_BASE}/leave?employeeId=${employeeId}` : `${API_BASE}/leave`;
  const res = await fetch(url);
  if (!res.ok) return [];
  return res.json();
}

export async function createLeave(data: { employeeId: number; leaveType: string; startDate: string; endDate: string; notes?: string }): Promise<any> {
  const res = await fetch(`${API_BASE}/leave`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) throw new Error('Failed to create leave record');
  return res.json();
}

export async function holdPayroll(division?: string, reason?: string): Promise<any> {
  const res = await fetch(`${API_BASE}/payroll/hold`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ division, reason })
  });
  if (!res.ok) throw new Error('Failed to apply payroll hold');
  return res.json();
}

export interface TicketItem {
  id: number;
  ticketId: string;
  employeeName: string;
  department: string;
  requestType: string;
  priority: string;
  status: string;
  details: string;
  createdAt: string;
}

export async function fetchTickets(status?: string, search?: string): Promise<TicketItem[]> {
  const params = new URLSearchParams();
  if (status && status !== 'ALL') params.append('status', status);
  if (search) params.append('search', search);

  const res = await fetch(`${API_BASE}/ticket?${params.toString()}`);
  if (!res.ok) return [];
  return res.json();
}

export async function createTicket(data: { employeeName: string; department: string; requestType: string; priority?: string; details?: string }): Promise<TicketItem> {
  const res = await fetch(`${API_BASE}/ticket`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) throw new Error('Failed to create ticket');
  return res.json();
}

export async function updateTicketStatus(id: number, status: string): Promise<TicketItem> {
  const res = await fetch(`${API_BASE}/ticket/${id}/status`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status })
  });
  if (!res.ok) throw new Error('Failed to update ticket status');
  return res.json();
}

export async function triageTicketWithAI(id: number): Promise<TicketItem> {
  const res = await fetch(`${API_BASE}/ticket/${id}/triage`, {
    method: 'POST'
  });
  if (!res.ok) throw new Error('Failed to AI triage ticket');
  return res.json();
}

export async function createTicketWithAI(prompt: string): Promise<TicketItem> {
  const res = await fetch(`${API_BASE}/ticket/ai-create`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prompt })
  });
  if (!res.ok) throw new Error('Failed to create AI ticket');
  return res.json();
}

// ── Expense Compliance & Audit ─────────────────────────────────────────────
export async function auditExpensesWithAI(): Promise<{
  totalAudited: number;
  compliantCount: number;
  violationCount: number;
  flaggedClaims: any[];
  summary: string;
}> {
  const res = await fetch(`${API_BASE}/expenses/audit`, {
    method: 'POST'
  });
  if (!res.ok) throw new Error('Failed to run AI expense audit');
  return res.json();
}

export async function updateExpenseStatus(id: number, status: string, reason?: string): Promise<Expense> {
  const res = await fetch(`${API_BASE}/expenses/${id}/status`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status, reason })
  });
  if (!res.ok) throw new Error('Failed to update expense status');
  return res.json();
}

export async function createExpenseClaim(data: { employeeId: number; expenseType: number; amount: number; description: string }): Promise<Expense> {
  const res = await fetch(`${API_BASE}/expenses`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      employeeId: data.employeeId,
      expenseType: data.expenseType,
      amount: data.amount,
      expenseDate: new Date().toISOString(),
      description: data.description
    })
  });
  if (!res.ok) throw new Error('Failed to submit expense claim');
  return res.json();
}

// ── Automation Workflows & Subsystem Health ─────────────────────────────────
export interface WorkflowDefinition {
  id: string;
  title: string;
  subsystem: string;
  description: string;
  defaultPrompt: string;
  icon: string;
  steps: string[];
}

export interface SubsystemStatus {
  name: string;
  type: string;
  status: string;
  target: string;
  metrics: string;
  isHealthy: boolean;
}

export interface AutomationHistoryItem {
  runId: string;
  originalPrompt: string;
  intent: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  actionCount: number;
  auditLogs: {
    action: string;
    target: string;
    result: string;
    currentHash: string;
    timestamp: string;
  }[];
}

export async function fetchWorkflows(): Promise<WorkflowDefinition[]> {
  const res = await fetch(`${API_BASE}/automation/workflows`);
  if (!res.ok) return [];
  return res.json();
}

export async function executeWorkflow(workflowId: string, prompt?: string, userRole = 'Admin'): Promise<AgentResult> {
  const res = await fetch(`${API_BASE}/automation/execute`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ workflowId, prompt, userRole })
  });
  if (!res.ok) throw new Error('Failed to execute workflow');
  return res.json();
}

export async function fetchSubsystems(): Promise<SubsystemStatus[]> {
  const res = await fetch(`${API_BASE}/automation/subsystems`);
  if (!res.ok) return [];
  return res.json();
}

export async function fetchAutomationHistory(): Promise<AutomationHistoryItem[]> {
  const res = await fetch(`${API_BASE}/automation/history`);
  if (!res.ok) return [];
  return res.json();
}

export async function triggerOnboardingWorkflow(prompt: string, userRole = 'Admin'): Promise<AgentResult> {
  return executeAgentPrompt(prompt, userRole);
}

// ── Job Openings & Candidate Portal ───────────────────────────────────────

export interface JobOpening {
  id: number;
  title: string;
  department: string;
  description: string;
  responsibilities?: string;
  requirements: string;
  location: string;
  salaryRange: string;
  status: string;
  createdAt: string;
  applicationsCount?: number;
  applications?: CandidateApplication[];
}

export interface CandidateApplication {
  id: number;
  jobOpeningId: number;
  jobTitle?: string;
  department?: string;
  candidateName: string;
  email: string;
  phone: string;
  experienceYears: number;
  coverNote: string;
  cvText: string;
  cvFileName: string;
  cvPdfData?: string;
  status: string;
  fitScore?: number;
  aiEvaluationJson?: string;
  submittedAt: string;
}

export interface CvAnalysisResult {
  candidateName: string;
  email: string;
  targetPosition: string;
  experienceYears: number;
  extractedSkills: string[];
  matchScore: number;
  recommendation: string;
  fitCategory: string;
  isBestFit: boolean;
  fitSummary: string;
  strengths: string[];
  weaknesses: string[];
  missingSkills: string[];
  recommendedInterviewQuestions: string[];
  proposedRecord?: {
    name: string;
    email: string;
    department: string;
    designation: string;
    suggestedSalary: number;
  };
}

export async function fetchJobOpenings(): Promise<JobOpening[]> {
  const res = await fetch(`${API_BASE}/jobs`);
  if (!res.ok) return [];
  return res.json();
}

export async function fetchJobOpening(id: number): Promise<JobOpening | null> {
  const res = await fetch(`${API_BASE}/jobs/${id}`);
  if (!res.ok) return null;
  return res.json();
}

export async function createJobOpening(data: {
  title: string;
  department?: string;
  description?: string;
  responsibilities?: string;
  requirements?: string;
  location?: string;
  salaryRange?: string;
}): Promise<any> {
  const res = await fetch(`${API_BASE}/jobs`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Failed to create job opening' }));
    throw new Error(err.message || 'Failed to create job opening');
  }
  return res.json();
}

export async function deleteJobOpening(id: number): Promise<any> {
  const res = await fetch(`${API_BASE}/jobs/${id}`, { method: 'DELETE' });
  if (!res.ok) throw new Error('Failed to delete job opening');
  return res.json();
}

export async function submitCandidateApplication(jobId: number, data: {
  candidateName: string;
  email: string;
  phone?: string;
  experienceYears?: number;
  coverNote?: string;
  cvText?: string;
  cvFileName?: string;
  cvPdfData?: string;
}): Promise<any> {
  const res = await fetch(`${API_BASE}/jobs/${jobId}/apply`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Failed to submit application' }));
    throw new Error(err.message || 'Failed to submit application');
  }
  return res.json();
}

export async function fetchCandidateApplications(jobId?: number): Promise<CandidateApplication[]> {
  const url = jobId ? `${API_BASE}/jobs/${jobId}/applications` : `${API_BASE}/jobs/applications`;
  const res = await fetch(url);
  if (!res.ok) return [];
  return res.json();
}

export async function analyzeCandidateCv(data: {
  cvContent?: string;
  pdfBase64?: string;
  fileName?: string;
  jobTitle?: string;
  requiredSkills?: string;
  jobOpeningId?: number;
  candidateId?: number;
}): Promise<CvAnalysisResult> {
  const res = await fetch(`${API_BASE}/cv/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'CV analysis failed' }));
    throw new Error(err.message || 'CV analysis failed');
  }
  return res.json();
}

export async function refreshInterviewQuestions(data: {
  cvContent?: string;
  jobTitle?: string;
  requiredSkills?: string;
  jobOpeningId?: number;
  candidateId?: number;
  existingQuestions?: string[];
}): Promise<string[]> {
  const res = await fetch(`${API_BASE}/cv/refresh-questions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Failed to refresh interview questions' }));
    throw new Error(err.message || 'Failed to refresh interview questions');
  }
  const json = await res.json();
  return json.questions || [];
}

export async function shortlistCandidate(applicationId: number): Promise<{ message: string; applicationId: number; status: string }> {
  const res = await fetch(`${API_BASE}/jobs/applications/${applicationId}/shortlist`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' }
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Failed to shortlist candidate' }));
    throw new Error(err.message || 'Failed to shortlist candidate');
  }
  return res.json();
}

export async function sendInterviewInvitation(applicationId: number, data: {
  interviewDate?: string;
  interviewTime?: string;
  mode?: string;
  locationOrLink?: string;
  notes?: string;
}): Promise<{ message: string; applicationId: number; status: string; interviewDetails: any }> {
  const res = await fetch(`${API_BASE}/jobs/applications/${applicationId}/send-interview-invitation`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Failed to send interview invitation' }));
    throw new Error(err.message || 'Failed to send interview invitation');
  }
  return res.json();
}

export async function rejectCandidate(applicationId: number, stage: 'Screening' | 'Interview' = 'Screening'): Promise<{ message: string; applicationId: number; status: string; stage: string }> {
  const res = await fetch(`${API_BASE}/jobs/applications/${applicationId}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ stage })
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Failed to reject candidate' }));
    throw new Error(err.message || 'Failed to reject candidate');
  }
  return res.json();
}
