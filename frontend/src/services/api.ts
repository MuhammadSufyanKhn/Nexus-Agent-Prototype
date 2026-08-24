export interface ConfirmationDetails {
  proposedAction: string;
  actionSummary: string;
  requiresUserAction: boolean;
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
  employeeId: number;
  employeeName?: string;
  category: string;
  amount: number;
  description?: string;
  status: number;
  statusName?: string;
  submittedAt: string;
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
