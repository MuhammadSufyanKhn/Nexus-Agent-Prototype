import React, { useState, useEffect } from 'react';
import {
  executeAgentPrompt,
  decideApproval,
  fetchDepartments,
  updateExpenseStatus
} from '../services/api';
import { getCommandSuggestions } from '../utils/commandEngine';
import type { CommandSuggestion } from '../utils/commandEngine';
import { CommandSuggestions } from './CommandSuggestions';
import type {
  AgentResult,
  ActionPlan,
  SqlAnalyticsResult,
  ComplianceResult
} from '../services/api';
import {
  Send,
  UserPlus,
  FileCheck,
  BarChart3,
  CheckCircle2,
  AlertTriangle,
  ShieldAlert,
  Sparkles,
  ChevronDown,
  ChevronRight,
  AlertCircle,
  Wifi,
  FileText,
  Building2,
  Users,
  PiggyBank,
  Bot,
  TrendingUp,
  Shield,
  Zap,
  ArrowUpRight,
  Briefcase,
  Ticket,
  Star,
  Receipt,
  ShieldCheck,
  Check,
  XCircle,
  Eye
} from 'lucide-react';
import { DocumentPreviewModal } from './DocumentPreviewModal';

interface AgentConsoleProps {
  userRole: string;
  onApprovalStateChange: () => void;
  onNavigate?: (tab: string, context?: any) => void;
}

export const AgentConsole: React.FC<AgentConsoleProps> = ({
  userRole,
  onApprovalStateChange,
  onNavigate
}) => {
  const [prompt, setPrompt] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<AgentResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [approvalProcessing, setApprovalProcessing] = useState(false);
  const [feedExpanded, setFeedExpanded] = useState(false);
  const [navigationPending, setNavigationPending] = useState<{ tab: string; label: string } | null>(null);
  const [expenseActionId, setExpenseActionId] = useState<number | null>(null);
  const [expenseFeedback, setExpenseFeedback] = useState<string | null>(null);
  const [previewModalDoc, setPreviewModalDoc] = useState<{
    isOpen: boolean;
    documentId?: string | null;
    contentHtml?: string | null;
    documentTitle?: string | null;
    documentType?: string | null;
    employeeName?: string | null;
    department?: string | null;
    createdAt?: string | null;
  }>({ isOpen: false });

  // Autocomplete & Command Engine state
  const [suggestions, setSuggestions] = useState<CommandSuggestion[]>([]);
  const [selectedSuggestionIndex, setSelectedSuggestionIndex] = useState(0);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [departmentsList, setDepartmentsList] = useState<string[]>(['IT', 'HR', 'Marketing', 'Operations', 'R&D']);

  useEffect(() => {
    fetchDepartments().then(depts => {
      if (depts && depts.length > 0) {
        setDepartmentsList(depts.map(d => d.name));
      }
    }).catch(() => {});
  }, []);

  const handlePromptChange = (val: string) => {
    setPrompt(val);
    if (val.trim().length >= 2) {
      const sugs = getCommandSuggestions(val, departmentsList);
      setSuggestions(sugs);
      setSelectedSuggestionIndex(0);
      setShowSuggestions(sugs.length > 0);
    } else {
      setSuggestions([]);
      setShowSuggestions(false);
    }
  };

  const applySuggestion = (sug: CommandSuggestion) => {
    setPrompt(sug.completedText);
    setShowSuggestions(false);
  };

  // Editable workflow preview state
  const [editedParams, setEditedParams] = useState<Record<string, string>>({});

  // Quick Action Modal states
  const [activeModal, setActiveModal] = useState<string | null>(null);
  const [qaName, setQaName] = useState('Ali');
  const [qaDept, setQaDept] = useState('IT');
  const [qaDesig, setQaDesig] = useState('Junior .NET Developer');
  const [qaSalary, setQaSalary] = useState('80000');
  const [qaNewSalary, setQaNewSalary] = useState('90000');
  const [qaSalaryTarget, setQaSalaryTarget] = useState('Ali');
  const [qaPolicyQuery, setQaPolicyQuery] = useState('leave policy');

  // Budget & HR Quick Action states
  const [qaReallocTgt, setQaReallocTgt] = useState('IT');
  const [qaReallocAmount, setQaReallocAmount] = useState('20000');
  const [qaAllocMode, setQaAllocMode] = useState<'ADD' | 'SET'>('ADD');
  const [qaFreezeDept, setQaFreezeDept] = useState('ALL');
  const [qaTransferName, setQaTransferName] = useState('Alex');
  const [qaTransferDept, setQaTransferDept] = useState('Product');
  const [qaTransferRole, setQaTransferRole] = useState('Senior Product Manager');
  const [qaLeaveName, setQaLeaveName] = useState('Marcus');
  const [qaPayrollDept, setQaPayrollDept] = useState('ALL');

  const quickPrompts = [
    {
      label: 'Job Requisition Template',
      icon: FileText,
      type: 'job_template',
      prompt: 'Create a new job opening for .NET Developer in IT department with location Remote / Hybrid, salary $50,000 - $60,000. Role Overview: Lead enterprise architecture, cloud modernization, and system scalability for IT department. Key Technical Requirements: ASP.NET, C#, Entity Framework, Web API development, Database Management, SQL, LINQ. Core Responsibilities: Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles. • Collaborate across multidisciplinary engineering, UX, and AI agent automation pods. • Optimize query execution, conduct peer code reviews, and champion continuous automated testing.'
    },
    {
      label: 'Create Job Opening',
      icon: Briefcase,
      type: 'job_create',
      prompt: 'Create a new job opening for Lead Cloud Architect in IT department with salary $95,000 - $125,000 requiring AWS, Kubernetes, Terraform, Docker, and CI/CD.'
    },
    {
      label: 'Show Job Openings',
      icon: Briefcase,
      type: 'job_read',
      prompt: 'Show all active job openings and count how many candidate CVs have been received for each position.'
    },
    {
      label: 'Screen Candidate CV',
      icon: Sparkles,
      type: 'cv_screen',
      prompt: 'Screen submitted candidate CVs for Senior Full Stack Developer and score match fit.'
    },
    {
      label: 'Onboard Employee',
      icon: UserPlus,
      type: 'onboard',
      prompt: 'Onboard employee Ali in IT as Junior .NET Developer with salary 80000'
    },
    {
      label: 'Transfer / Promote',
      icon: TrendingUp,
      type: 'transfer',
      prompt: 'Move Alex from Engineering to Product as Senior Product Manager under Manager Sarah'
    },
    {
      label: 'Log Sick Day + Slack',
      icon: Zap,
      type: 'leave',
      prompt: "Log Marcus's sick day today and notify his team on Slack."
    },
    {
      label: 'Allocate Budget',
      icon: BarChart3,
      type: 'budget_realloc',
      prompt: 'Allocate 100k budget to IT department for Q3.'
    },
    {
      label: 'Freeze Budgets',
      icon: ShieldAlert,
      type: 'freeze',
      prompt: 'Freeze all department budget allocations for Q3.'
    },
    {
      label: 'Hold Payroll',
      icon: Shield,
      type: 'payroll',
      prompt: 'Place a payroll hold on the Sales division.'
    },
    {
      label: 'Analyze Budget',
      icon: BarChart3,
      type: 'budget',
      prompt: 'Show me departments exceeding their allocated Q3 budget.'
    },
    {
      label: 'Check Policy',
      icon: FileCheck,
      type: 'policy',
      prompt: 'Show me the current leave policy.'
    }
  ];

  // ── Intent → Tab navigation map ──────────────────────────────────────────
  const TAB_LABELS: Record<string, string> = {
    employees: 'Employee Directory', departments: 'Departments & Budgets',
    policies: 'HR Policy Center', expenses: 'Expense Review',
    onboarding: 'Onboarding Hub', audit: 'Activity History',
    approvals: 'HR Approval Center', dashboard: 'Workforce Dashboard', tickets: 'Workplace Service Desk',
    jobs: 'Job Openings & Requisitions',
    cv: 'Candidate CV Screening'
  };

  const getTargetTab = (intent: string): string | null => {
    const map: Record<string, string> = {
      EMPLOYEE_READ: 'employees', EMPLOYEE_CREATE: 'employees', EMPLOYEE_UPDATE: 'employees',
      EMPLOYEE_DELETE: 'employees', EMPLOYEE_TRANSFER: 'employees', EMPLOYEE_PROMOTE: 'employees',
      EMPLOYEE_OFFBOARD: 'employees', EMPLOYEE_ONBOARDING: 'onboarding', ONBOARDING_READ: 'onboarding',
      EMPLOYEE_CANCEL_ONBOARDING: 'onboarding', DEPARTMENT_CREATE: 'departments',
      DEPARTMENT_READ: 'departments', DEPARTMENT_UPDATE: 'departments', DEPARTMENT_DELETE: 'departments',
      BUDGET_ANALYSIS: 'departments', BUDGET_UPDATE: 'departments', BUDGET_REALLOCATE: 'departments',
      BUDGET_FREEZE: 'departments', BUDGET_READ: 'departments', POLICY_READ: 'policies',
      POLICY_CREATE: 'policies', POLICY_UPDATE: 'policies', POLICY_DELETE: 'policies',
      EXPENSE_READ: 'expenses', EXPENSE_CREATE: 'expenses', EXPENSE_COMPLIANCE: 'expenses',
      EXPENSE_FILTER: 'expenses', EXPENSE_COMPLIANCE_SWEEP: 'expenses', EXPENSE_APPROVE: 'expenses',
      EXPENSE_REJECT: 'expenses', EXPENSE_FLAG: 'expenses', EXPENSE_ANALYTICS: 'expenses',
      EXPENSE_CATEGORY_ANALYTICS: 'expenses', EXPENSE_POLICY_VARIANCE: 'expenses',
      APPROVAL_READ: 'approvals', AUDIT_READ: 'audit', DASHBOARD_ANALYTICS: 'dashboard',
      EXECUTE_AUTOMATION: 'audit', PAYROLL_HOLD: 'departments', PAYROLL_BONUS: 'departments',
      LEAVE_CREATE: 'employees', UPDATE_SALARY: 'employees',
      JOB_OPENING_CREATE: 'jobs', JOB_OPENING_READ: 'jobs',
      CV_SCREEN: 'cv', TICKET_READ: 'tickets', TICKET_CREATE: 'tickets', TICKET_TRIAGE: 'tickets',
      SECURITY_TEST: 'audit', SQL_AGENT: 'dashboard'
    };
    return map[intent] ?? null;
  };

  const triggerNavigation = (intent: string) => {
    if (!onNavigate) return;
    const tab = getTargetTab(intent);
    if (!tab) return;
    const label = TAB_LABELS[tab] ?? tab;
    // Present navigation choice without any auto-redirection timer
    setNavigationPending({ tab, label });
  };

  const handleExecute = async (promptToRun?: string) => {
    const targetPrompt = promptToRun || prompt;
    if (!targetPrompt.trim()) return;

    setLoading(true);
    setErrorMsg(null);
    setResult(null);
    setFeedExpanded(false);
    setEditedParams({});
    setActiveModal(null);
    setNavigationPending(null);

    try {
      const res = await executeAgentPrompt(targetPrompt, userRole);
      setResult(res);
      if (res.isSuccess) {
        window.dispatchEvent(new CustomEvent('nexus-data-updated', { detail: { intent: res.intent, data: res.resultData } }));
        window.dispatchEvent(new CustomEvent('budget-updated'));
        // Navigate for read-only successful results
        // Trigger 7-second choice card for navigation
        if (res.intent && !res.requiresApproval) {
          triggerNavigation(res.intent);
        }
      }
      if (!res.isSuccess && res.errorMessage) {
        setErrorMsg(res.errorMessage);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Unable to process your request. Please check service connectivity.');
    } finally {
      setLoading(false);
    }
  };

  const handleQuickActionClick = (q: typeof quickPrompts[0]) => {
    if (q.type === 'job_template') {
      setPrompt(q.prompt);
      return;
    }
    if (q.type === 'budget' || q.type === 'security' || q.type === 'job_create' || q.type === 'job_read' || q.type === 'cv_screen') {
      setPrompt(q.prompt);
      handleExecute(q.prompt);
    } else {
      setActiveModal(q.type);
    }
  };

  const handleApprovalDecision = async (approved: boolean) => {
    if (!result?.actionPlan?.approvalId) return;

    setApprovalProcessing(true);
    try {
      const decisionRes = await decideApproval({
        approvalId: result.actionPlan.approvalId,
        approved,
        approvedBy: `${userRole} User`,
        reason: approved ? 'Changes approved' : 'Changes declined',
        editedParameters: Object.keys(editedParams).length > 0 ? editedParams : undefined
      });

      if (decisionRes) {
        setResult((prev) => {
          if (!prev) return null;
          return {
            ...prev,
            requiresApproval: false,
            actionPlan: prev.actionPlan
              ? { ...prev.actionPlan, status: approved ? 'APPROVED' : 'REJECTED' }
              : undefined
          };
        });
        if (approved) {
          window.dispatchEvent(new CustomEvent('nexus-data-updated'));
          window.dispatchEvent(new CustomEvent('budget-updated'));
          // Trigger 7-second choice card for navigation after approval
          if (result.intent) {
            triggerNavigation(result.intent);
          }
        }
        if (onApprovalStateChange) onApprovalStateChange();
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to record your decision.');
    } finally {
      setApprovalProcessing(false);
    }
  };

  // ── Intent → human label mapping ──────────────────────────────────────────
  const intentLabel = (intent: string) => {
    const map: Record<string, string> = {
      EMPLOYEE_CREATE: 'Create Employee',
      EMPLOYEE_READ: 'Read Employee',
      EMPLOYEE_UPDATE: 'Update Employee',
      EMPLOYEE_DELETE: 'Delete Employee',
      EMPLOYEE_ONBOARDING: 'Employee Onboarding',
      DEPARTMENT_CREATE: 'Create Department',
      DEPARTMENT_READ: 'Read Departments',
      DEPARTMENT_UPDATE: 'Update Department',
      DEPARTMENT_DELETE: 'Delete Department',
      POLICY_CREATE: 'Create Policy',
      POLICY_READ: 'Read Policy',
      POLICY_UPDATE: 'Update Policy',
      POLICY_DELETE: 'Delete Policy',
      BUDGET_ANALYSIS: 'Budget Analysis',
      BUDGET_READ: 'Read Budgets',
      BUDGET_UPDATE: 'Update Budget',
      EXPENSE_COMPLIANCE: 'Expense Compliance',
      EXPENSE_READ: 'Read Expenses',
      EXPENSE_CREATE: 'Submit Expense',
      EXPENSE_FILTER: 'Filter Expenses',
      EXPENSE_COMPLIANCE_SWEEP: 'Compliance Sweep',
      EXPENSE_APPROVE: 'Approve Expense',
      EXPENSE_REJECT: 'Reject Expense',
      EXPENSE_FLAG: 'Flag Expense',
      EXPENSE_ANALYTICS: 'Expense Analytics',
      EXPENSE_CATEGORY_ANALYTICS: 'Category Analytics',
      EXPENSE_POLICY_VARIANCE: 'Policy Variance',
      APPROVAL_READ: 'Read Approvals',
      ONBOARDING_READ: 'Read Onboarding',
      AUDIT_READ: 'Audit Logs',
      DASHBOARD_ANALYTICS: 'Dashboard Analytics',
      SQL_AGENT: 'Data Query',
      GENERAL_CONVERSATION: 'Conversational AI',
      UNKNOWN: 'Unknown Intent'
    };
    return map[intent] || intent;
  };

  // ── Render helpers ────────────────────────────────────────────────────────

  const renderActionPlan = (plan: ActionPlan) => {
    const hasEdits = Object.keys(editedParams).length > 0;

    // Map entity names to friendly labels
    const friendlyEntity = (name: string) => {
      const map: Record<string, string> = {
        EMPLOYEE: 'Employee Profile', DEPARTMENT: 'Department Master', DEPARTMENT_BUDGET: 'Department Budget',
        POLICY: 'HR Policy Record', EXPENSE: 'Expense Report', ONBOARDING: 'Employee Onboarding',
      };
      let clean = (name || '').replace(/\s*\([^)]*\)/g, '').trim();
      return map[clean?.toUpperCase()] ?? clean ?? 'HR Entity';
    };

    // Map field names to friendly labels
    const friendlyField = (field: string) => {
      const map: Record<string, string> = {
        name: 'Full Name', salary: 'Base Salary', designation: 'Job Title', role: 'Leadership Role',
        department: 'Department', startDate: 'Start Date', endDate: 'End Date',
        status: 'Status', email: 'Email Address', phone: 'Phone Number',
        manager: 'Reporting Manager', budget: 'Budget Amount', budgetAmount: 'Budget Amount',
        head: 'Department Head', amount: 'Amount', fromDepartment: 'From Department',
        toDepartment: 'To Department', effectiveDate: 'Effective Date', reason: 'Reason',
        newSalary: 'New Salary', oldSalary: 'Previous Salary', bonus: 'Bonus Amount',
        employeeId: 'Employee ID', id: 'ID', leaveType: 'Leave Type', leaveDate: 'Leave Date',
        appointment_type: 'Appointment Type', new_designation: 'New Job Title'
      };
      return map[field] ?? field.replace(/([A-Z])/g, ' $1').trim();
    };

    return (
      <div className="bg-white rounded-2xl border border-amber-200 shadow-lg overflow-hidden animate-in fade-in duration-200">

        {/* Header */}
        <div className="bg-gradient-to-r from-amber-50 to-orange-50 border-b border-amber-200 px-6 py-4 flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-amber-100 text-amber-700 rounded-xl">
              <ShieldAlert className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2 mb-0.5">
                <span className="text-[11px] font-bold uppercase tracking-wider text-amber-700 bg-amber-100 border border-amber-200 px-2 py-0.5 rounded-full">
                  Awaiting Your Approval
                </span>
              </div>
              <h3 className="text-base font-bold text-slate-900 leading-snug">{plan.title}</h3>
            </div>
          </div>
          {plan.totalFinancialImpact && plan.totalFinancialImpact > 0 && (
            <div className="text-right shrink-0">
              <span className="text-[11px] text-slate-500 font-medium block">Financial Impact</span>
              <span className="text-lg font-extrabold text-slate-900">
                +${plan.totalFinancialImpact.toLocaleString()}
              </span>
            </div>
          )}
        </div>

        <div className="p-6 space-y-5">
          {/* Info Banner */}
          <div className="p-3 bg-blue-50 border border-blue-100 rounded-xl text-xs text-blue-800 flex items-start gap-2.5">
            <Sparkles className="w-4 h-4 text-blue-500 shrink-0 mt-0.5" />
            <div>
              <span className="font-semibold block">Review Proposed Changes</span>
              Nexus AI has prepared the following HR action for your review. You can edit any field below before approving.
            </div>
          </div>

          {/* Policy / Safety Notes */}
          {plan.warnings && plan.warnings.length > 0 && (
            <div className="bg-amber-50 border border-amber-200 rounded-xl p-3.5 text-xs text-amber-900 space-y-1.5">
              <div className="font-bold flex items-center gap-1.5">
                <AlertTriangle className="w-3.5 h-3.5" /> Policy & Compliance Notes:
              </div>
              {plan.warnings.map((w, idx) => (
                <div key={idx} className="ml-5 leading-relaxed">• {w}</div>
              ))}
            </div>
          )}

          {/* Affected Records — HR-friendly cards */}
          {plan.affectedRecords && plan.affectedRecords.length > 0 && (
            <div className="space-y-4">
              {plan.affectedRecords.map((rec, rIdx) => (
                <div key={rIdx} className="border border-slate-200 rounded-xl overflow-hidden">
                  {/* Record Header */}
                  <div className="flex items-center justify-between bg-slate-50 border-b border-slate-200 px-4 py-2.5">
                    <div className="flex items-center gap-2">
                      <Users className="w-3.5 h-3.5 text-slate-500" />
                      <span className="text-xs font-bold text-slate-700">{friendlyEntity(rec.entityName)}</span>
                    </div>
                    {rec.primaryLabel && (
                      <span className="text-xs font-semibold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-full">
                        {rec.primaryLabel}
                      </span>
                    )}
                  </div>

                  {/* Changes Table */}
                  {rec.changes && rec.changes.length > 0 && (
                    <div className="divide-y divide-slate-100">
                      <div className="grid grid-cols-3 bg-slate-50/50 px-4 py-2">
                        <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Field</span>
                        <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Current Value</span>
                        <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Proposed Change</span>
                      </div>
                      {rec.changes.map((c, cIdx) => {
                        const isEdited = editedParams[c.fieldName] !== undefined;
                        const currentValue = editedParams[c.fieldName] ?? c.newValue;
                        return (
                          <div key={cIdx} className="grid grid-cols-3 items-center px-4 py-2.5 gap-2 hover:bg-slate-50/50 transition-colors">
                            <span className="text-xs font-semibold text-slate-700">
                              {friendlyField(c.fieldName)}
                            </span>
                            <span className="text-xs text-slate-500">
                              {c.oldValue || <span className="text-slate-300 italic">—</span>}
                            </span>
                            <div className="relative">
                              <input
                                type="text"
                                value={currentValue}
                                onChange={(e) => setEditedParams({ ...editedParams, [c.fieldName]: e.target.value })}
                                className={`w-full text-xs font-semibold rounded-lg px-2.5 py-1.5 border transition-all outline-none ${
                                  isEdited
                                    ? 'bg-purple-50 border-purple-300 text-purple-900 focus:ring-1 focus:ring-purple-400'
                                    : 'bg-emerald-50 border-emerald-200 text-emerald-900 hover:bg-white focus:bg-white focus:border-blue-400 focus:ring-1 focus:ring-blue-400'
                                }`}
                                title="Click to edit this value"
                              />
                              {isEdited && (
                                <span className="absolute -top-1 -right-1 w-2 h-2 bg-purple-500 rounded-full" />
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Workflow Steps — shown as friendly milestones, NOT technical tool names */}
          {plan.steps && plan.steps.length > 0 && (
            <div className="space-y-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-slate-400">What will happen</h4>
              <div className="space-y-1.5">
                {plan.steps.map((s, sIdx) => (
                  <div key={sIdx} className="flex items-center gap-2.5 p-2.5 bg-slate-50 rounded-lg border border-slate-100 text-xs">
                    <div className="w-5 h-5 rounded-full bg-blue-100 text-blue-700 flex items-center justify-center font-bold text-[10px] shrink-0">
                      {s.stepNumber}
                    </div>
                    <span className="text-slate-700 font-medium">{s.description}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Your Decision Section */}
          <div className="pt-3 border-t border-slate-100 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-700 uppercase tracking-wider">Your Decision</span>
              {hasEdits && (
                <span className="text-[11px] font-semibold text-purple-700 bg-purple-50 border border-purple-200 px-2 py-0.5 rounded-full">
                  Custom modifications detected
                </span>
              )}
            </div>
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    setResult(null);
                    setEditedParams({});
                  }}
                  disabled={approvalProcessing}
                  className="px-4 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-xs font-semibold transition-colors disabled:opacity-50 cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  onClick={() => handleApprovalDecision(false)}
                  disabled={approvalProcessing}
                  className="px-5 py-2.5 bg-white hover:bg-red-50 text-red-600 border border-red-200 rounded-xl text-xs font-semibold transition-colors disabled:opacity-50 cursor-pointer"
                >
                  Decline Request
                </button>
              </div>
              <button
                onClick={() => handleApprovalDecision(true)}
                disabled={approvalProcessing}
                className="px-6 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-bold transition-all shadow-sm hover:shadow-md flex items-center gap-2 disabled:opacity-50 cursor-pointer"
              >
                <CheckCircle2 className="w-4 h-4" />
                {approvalProcessing
                  ? 'Applying Changes...'
                  : hasEdits
                    ? 'Apply Edited Changes'
                    : 'Approve Changes'}
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  };

  const renderSqlAnalytics = (data: SqlAnalyticsResult) => (
    <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3">
        <div className="flex items-center gap-2 text-slate-900 font-bold text-sm">
          <BarChart3 className="w-4 h-4 text-blue-600" />
          <span>Data Analysis &amp; Query Results</span>
        </div>
        <span className="text-[11px] bg-blue-50 text-blue-700 border border-blue-200 font-semibold px-2 py-0.5 rounded">
          Validated Query
        </span>
      </div>

      {data.summary && (
        <div className="p-3 bg-blue-50/50 border border-blue-100 rounded-lg text-xs text-blue-900 font-medium leading-relaxed">
          💡 {data.summary}
        </div>
      )}

      {data.keyInsights && data.keyInsights.length > 0 && (
        <ul className="space-y-1">
          {data.keyInsights.map((insight, i) => (
            <li key={i} className="text-xs text-slate-700 flex items-start gap-2">
              <span className="text-blue-500 mt-0.5">•</span> {insight}
            </li>
          ))}
        </ul>
      )}

      {data.columns && data.columns.length > 0 && (
        <div className="border border-slate-200 rounded-lg overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold">
              <tr>
                {data.columns.map((col: string, idx: number) => (
                  <th key={idx} className="py-2 px-3">{col}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.rows?.map((row: any, idx: number) => (
                <tr key={idx} className="hover:bg-slate-50/60">
                  {data.columns.map((col: string, cIdx: number) => (
                    <td key={cIdx} className="py-2 px-3 text-slate-800">{String(row[col] ?? '')}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );

  const renderComplianceResult = (comp: ComplianceResult) => {
    const isCompliant = comp.status === 'COMPLIANT';
    return (
      <div className={`rounded-xl border p-5 shadow-xs space-y-3 ${isCompliant ? 'bg-emerald-50/50 border-emerald-200' : 'bg-rose-50/50 border-rose-200'
        }`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            {isCompliant
              ? <CheckCircle2 className="w-5 h-5 text-emerald-600" />
              : <AlertTriangle className="w-5 h-5 text-rose-600" />}
            <h4 className={`font-bold text-sm ${isCompliant ? 'text-emerald-900' : 'text-rose-900'}`}>
              Policy Compliance Assessment: {comp.status}
            </h4>
          </div>
          {comp.policySource && (
            <span className="text-[10px] bg-white border border-slate-200 px-2 py-0.5 rounded font-semibold text-slate-600">
              Source: {comp.policySource}
            </span>
          )}
        </div>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs bg-white p-3 rounded-lg border border-slate-200">
          <div><span className="text-slate-400 block text-[10px]">Claimed Amount</span><span className="font-bold text-slate-800">${comp.claimedAmount?.toFixed(2) ?? '0.00'}</span></div>
          <div><span className="text-slate-400 block text-[10px]">Allowed Policy Limit</span><span className="font-bold text-slate-800">${comp.allowedAmount?.toFixed(2) ?? '0.00'}</span></div>
          <div><span className="text-slate-400 block text-[10px]">Variance</span><span className={`font-bold ${(comp.difference ?? 0) > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>${comp.difference?.toFixed(2) ?? '0.00'}</span></div>
          <div><span className="text-slate-400 block text-[10px]">Employee</span><span className="font-bold text-slate-800">{comp.employeeName || '—'}</span></div>
        </div>
        {comp.reason && <p className={`text-xs ${isCompliant ? 'text-emerald-800' : 'text-rose-800'} font-medium`}>{comp.reason}</p>}
      </div>
    );
  };

  // Generic result renderer — for employee, policy, department, budget operations
  const renderGenericResult = (data: any, intent: string) => {
    if (!data) return null;

    // Choose icon & color by intent family
    const isPolicyIntent = intent.startsWith('POLICY');
    const isDeptIntent = intent.startsWith('DEPARTMENT');
    const isBudgetIntent = intent.startsWith('BUDGET');
    const isEmployeeIntent = intent.startsWith('EMPLOYEE');

    const Icon = isPolicyIntent ? FileText : isDeptIntent ? Building2 : isBudgetIntent ? PiggyBank : Users;
    const colorClass = isPolicyIntent ? 'blue' : isDeptIntent ? 'purple' : isBudgetIntent ? 'green' : 'indigo';
    const borderColor = `border-${colorClass}-200`;

    // Extract a human-readable message
    const message = data?.message
      || data?.result
      || data?.summary
      || (isEmployeeIntent && data?.name ? `Employee '${data.name}' operation completed.` : null)
      || 'Operation completed successfully.';

    // Extract list of items if result is array or contains array
    const rawList = Array.isArray(data)
      ? data
      : (Array.isArray(data?.result) ? data.result : data?.policies ?? data?.employees ?? data?.departments ?? data?.budgets ?? null);

    // Keys that should never be dumped as raw key-value strings
    const excludedFieldKeys = [
      'message', 'result', 'summary', 'policies', 'employees', 'departments', 'budgets', 'count',
      'contentHtml', 'content_html', 'html', 'previewUrl', 'downloadUrl', 'documentId', 'documentTitle',
      'fitSummary', 'matchedSkills', 'missingSkills', 'skillBreakdown', 'projectManagementEvidence',
      'recommendedQuestions', 'milestones'
    ];

    // Build primitive key-value fields for single record operations
    const fields = typeof data === 'object' && data !== null && !Array.isArray(data)
      ? Object.entries(data)
        .filter(([k, v]) => !excludedFieldKeys.includes(k) && (typeof v !== 'object' || v === null))
        .slice(0, 10)
      : [];

    return (
      <div className={`bg-white rounded-xl border ${borderColor} p-5 shadow-xs space-y-4`}>
        <div className="flex items-center gap-3 border-b border-slate-100 pb-3">
          <div className={`p-2 rounded-lg bg-${colorClass}-50 border border-${colorClass}-100`}>
            <Icon className={`w-4 h-4 text-${colorClass}-600`} />
          </div>
          <div>
            <div className="text-xs font-bold text-slate-500 uppercase tracking-wider">{intentLabel(intent)}</div>
            <div className="text-sm font-semibold text-slate-900">
              {typeof message === 'string' ? message : 'Operation completed successfully.'}
            </div>
          </div>
          <CheckCircle2 className="w-5 h-5 text-emerald-500 ml-auto shrink-0" />
        </div>

        {/* Interactive PDF Document Widget */}
        {(data?.documentId || data?.downloadUrl) && (
          <div className="p-4 bg-slate-900 text-white rounded-xl border border-slate-800 space-y-3 shadow-md">
            <div className="flex items-center justify-between border-b border-slate-800 pb-2">
              <div className="flex items-center gap-2">
                <FileText className="w-5 h-5 text-indigo-400" />
                <span className="font-bold text-xs tracking-wide text-indigo-200">
                  {data?.documentTitle ?? 'Generated HR Document Packet'}
                </span>
              </div>
              <div className="flex items-center gap-2">
                {intent === 'ONBOARDING_DOCUMENT_PREVIEW' && (
                  <span className="text-[10px] bg-slate-800 text-slate-300 px-2 py-0.5 rounded font-medium border border-slate-700">
                    Preview Only &bull; No Email Sent
                  </span>
                )}
                <span className="text-[10px] bg-indigo-900 text-indigo-300 px-2 py-0.5 rounded font-mono border border-indigo-700">
                  {data?.type ?? 'PDF READY'}
                </span>
              </div>
            </div>
            <p className="text-xs text-slate-300">
              Official HR document packet compiled and signed via Nexus Document Subsystem.
            </p>
            <div className="flex items-center gap-3 pt-1">
              <button
                type="button"
                onClick={() => {
                  setPreviewModalDoc({
                    isOpen: true,
                    documentId: data?.documentId,
                    contentHtml: data?.contentHtml,
                    documentTitle: data?.documentTitle,
                    documentType: data?.type || 'ONBOARDING_PACKAGE',
                    employeeName: data?.employeeName,
                    department: data?.department,
                    createdAt: data?.createdAt
                  });
                }}
                className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors shadow-xs cursor-pointer"
              >
                <Eye className="w-3.5 h-3.5" />
                Preview Document
              </button>
              <a
                href={data.downloadUrl ?? `/api/documents/${data.documentId}/download`}
                download
                className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors shadow-xs"
              >
                Download PDF
              </a>
            </div>
          </div>
        )}

        {/* Rich Candidate Screening Card */}
        {(data?.fitScore !== undefined || data?.skillsFound !== undefined || data?.leadershipRating || data?.salaryBandLevel || data?.recommendedQuestions) && (
          <div className="p-4 bg-slate-900 text-white rounded-xl border border-slate-800 space-y-4 shadow-md">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <div className="flex items-center gap-2.5">
                <Briefcase className="w-5 h-5 text-indigo-400" />
                <div>
                  <h4 className="font-bold text-sm text-white flex items-center gap-2">
                    {data?.candidateName || 'Candidate Profile Evaluation'}
                    {data?.jobTitle && (
                      <span className="text-xs font-normal text-slate-400">for {data.jobTitle}</span>
                    )}
                  </h4>
                  <span className="text-[11px] text-slate-400">Workforce Intelligence &bull; Resume Fit Assessment</span>
                </div>
              </div>
              {data?.fitScore !== undefined && (
                <div className="flex items-center gap-2">
                  <div className={`px-3 py-1 rounded-lg font-mono font-bold text-sm border ${
                    data.fitScore >= 80
                      ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30'
                      : data.fitScore >= 50
                      ? 'bg-amber-500/10 text-amber-400 border-amber-500/30'
                      : 'bg-rose-500/10 text-rose-400 border-rose-500/30'
                  }`}>
                    {data.fitScore}% FIT
                  </div>
                  {data?.categoryFit && (
                    <span className="text-xs px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-semibold border border-slate-700">
                      {data.categoryFit}
                    </span>
                  )}
                </div>
              )}
            </div>

            {/* Candidate Summary Box */}
            {data?.fitSummary && (
              <div className="p-3 bg-slate-800/60 rounded-lg border border-slate-700/60 text-xs text-slate-200 leading-relaxed">
                {data.fitSummary}
              </div>
            )}

            {/* Skill Breakdown */}
            {data?.matchedSkills && data.matchedSkills.length > 0 && (
              <div className="space-y-1.5">
                <span className="text-[11px] uppercase font-bold text-slate-400 tracking-wider">Verified Competencies</span>
                <div className="flex flex-wrap gap-1.5">
                  {data.matchedSkills.map((s: string, idx: number) => (
                    <span key={idx} className="px-2 py-0.5 bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 rounded text-xs font-medium">
                      ✓ {s}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {data?.missingSkills && data.missingSkills.length > 0 && (
              <div className="space-y-1.5">
                <span className="text-[11px] uppercase font-bold text-slate-400 tracking-wider">Identified Qualification Gaps</span>
                <div className="flex flex-wrap gap-1.5">
                  {data.missingSkills.map((s: string, idx: number) => (
                    <span key={idx} className="px-2 py-0.5 bg-rose-500/15 text-rose-300 border border-rose-500/30 rounded text-xs font-medium">
                      ✕ {s}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Technical Skills Analysis Grid */}
            {data?.skillBreakdown && Array.isArray(data.skillBreakdown) && (
              <div className="space-y-2">
                <div className="flex items-center justify-between text-xs text-slate-400">
                  <span className="text-[11px] uppercase font-bold tracking-wider">Technical Competency Audit</span>
                  <span>{data.skillsFound} / {data.totalSkills} skills verified</span>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  {data.skillBreakdown.map((sb: any, idx: number) => (
                    <div key={idx} className="p-2.5 bg-slate-800/70 rounded-lg border border-slate-700/80 text-xs space-y-1">
                      <div className="flex items-center justify-between">
                        <span className="font-semibold text-white">{sb.skill}</span>
                        <span className={`text-[10px] px-1.5 py-0.5 rounded font-bold ${
                          sb.status === 'Verified' ? 'bg-emerald-500/20 text-emerald-300' : 'bg-rose-500/20 text-rose-300'
                        }`}>
                          {sb.status}
                        </span>
                      </div>
                      <p className="text-[11px] text-slate-400">{sb.evidence}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Leadership Analysis Details */}
            {data?.leadershipRating && (
              <div className="p-3 bg-slate-800/80 rounded-lg border border-slate-700 space-y-2">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-[11px] uppercase font-bold text-slate-400 tracking-wider">Leadership & Ownership Rating</span>
                  <span className="font-semibold text-indigo-300 bg-indigo-500/20 px-2 py-0.5 rounded border border-indigo-500/30">
                    {data.leadershipRating}
                  </span>
                </div>
                {data.projectManagementEvidence && Array.isArray(data.projectManagementEvidence) && (
                  <ul className="text-xs text-slate-300 space-y-1 list-disc list-inside">
                    {data.projectManagementEvidence.map((p: string, idx: number) => (
                      <li key={idx}>{p}</li>
                    ))}
                  </ul>
                )}
              </div>
            )}

            {/* Salary Band Assessment */}
            {data?.salaryBandLevel && (
              <div className="p-3 bg-slate-800/80 rounded-lg border border-slate-700 flex items-center justify-between text-xs">
                <div>
                  <span className="text-[10px] uppercase font-bold text-slate-400 tracking-wider block">Policy Band ({data.policyCode || 'POL-HR-001'})</span>
                  <span className="font-bold text-white text-sm">{data.salaryBandLevel}</span>
                </div>
                <div className="text-right">
                  <span className="text-[10px] text-slate-400 block">Recommended Range</span>
                  <span className="font-mono font-bold text-emerald-400 text-sm">
                    ${Number(data.minSalary || 0).toLocaleString()} – ${Number(data.maxSalary || 0).toLocaleString()}
                  </span>
                </div>
              </div>
            )}

            {/* Recommended Interview Questions */}
            {data?.recommendedQuestions && Array.isArray(data.recommendedQuestions) && (
              <div className="space-y-2 pt-1 border-t border-slate-800">
                <span className="text-[11px] uppercase font-bold text-slate-400 tracking-wider block">Suggested Interview Inquiries</span>
                <ol className="space-y-1.5 text-xs text-slate-300 list-decimal list-inside bg-slate-950/40 p-3 rounded-lg border border-slate-800/60">
                  {data.recommendedQuestions.map((q: string, idx: number) => (
                    <li key={idx} className="leading-relaxed pl-1">{q}</li>
                  ))}
                </ol>
              </div>
            )}
          </div>
        )}

        {/* Onboarding Email Resend Confirmation */}
        {data?.dispatchedAt && data?.email && (
          <div className="p-4 bg-slate-900 text-white rounded-xl border border-slate-800 space-y-3 shadow-md">
            <div className="flex items-center justify-between border-b border-slate-800 pb-2">
              <div className="flex items-center gap-2">
                <Send className="w-4 h-4 text-emerald-400" />
                <span className="font-bold text-xs tracking-wide text-emerald-200">
                  Onboarding Welcome Email Resent
                </span>
              </div>
              <span className="text-[10px] bg-emerald-900/60 text-emerald-300 px-2 py-0.5 rounded font-mono border border-emerald-700">
                DELIVERED
              </span>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-xs bg-slate-950/50 p-3 rounded-lg border border-slate-800">
              <div>
                <span className="text-slate-400 block text-[10px]">Employee</span>
                <span className="font-semibold text-slate-200">{data.employeeName}</span>
              </div>
              <div>
                <span className="text-slate-400 block text-[10px]">Recipient Email</span>
                <span className="font-semibold text-indigo-300">{data.email}</span>
              </div>
              <div>
                <span className="text-slate-400 block text-[10px]">Department</span>
                <span className="font-semibold text-slate-200">{data.department || 'General'}</span>
              </div>
              <div>
                <span className="text-slate-400 block text-[10px]">Designation</span>
                <span className="font-semibold text-slate-200">{data.designation || 'Specialist'}</span>
              </div>
            </div>
          </div>
        )}

        {/* Onboarding Milestone Timeline */}
        {data?.milestones && Array.isArray(data.milestones) && (
          <div className="p-4 bg-slate-900 text-white rounded-xl border border-slate-800 space-y-3 shadow-md">
            <div className="flex items-center justify-between border-b border-slate-800 pb-2">
              <span className="font-bold text-xs tracking-wide text-indigo-200">
                Active Onboarding Milestones ({data.milestones.length})
              </span>
              <span className="text-[10px] bg-indigo-900 text-indigo-300 px-2 py-0.5 rounded font-mono border border-indigo-700">
                TIMELINE
              </span>
            </div>
            <div className="space-y-2">
              {data.milestones.map((m: any, idx: number) => (
                <div key={idx} className="flex items-center justify-between p-2.5 bg-slate-800/70 rounded-lg border border-slate-700/60 text-xs">
                  <div className="flex items-center gap-2">
                    <span className="w-5 h-5 rounded-full bg-slate-700 text-slate-300 flex items-center justify-center font-bold text-[10px]">
                      {idx + 1}
                    </span>
                    <div>
                      <span className="font-semibold text-white block">{m.taskName || m.TaskName}</span>
                      <span className="text-[10px] text-slate-400">{m.employeeName || 'New Hire'}</span>
                    </div>
                  </div>
                  <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                    (m.status || '').toLowerCase() === 'completed'
                      ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'
                      : 'bg-amber-500/20 text-amber-300 border border-amber-500/30'
                  }`}>
                    {m.status || 'Pending'}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Array results list (e.g. policy list, employee list) */}
        {rawList && rawList.length > 0 && (
          <div className="space-y-2.5">
            {rawList.slice(0, 15).map((item: any, idx: number) => {
              if (!item || typeof item !== 'object') return null;
              const nameLabel = item.title ?? item.name ?? item.code ?? item.departmentName ?? `Record #${item.id || idx + 1}`;
              const descLabel = item.contentSummary ?? item.designation ?? item.category ?? item.description ?? item.quarter ?? '';
              const valLabel = item.salary ? `$${Number(item.salary).toLocaleString()}` : item.code ?? item.statusName ?? item.status ?? '';

              return (
                <div key={idx} className="p-3.5 bg-slate-50 rounded-xl border border-slate-200 text-xs space-y-2">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="flex items-center gap-2 mb-0.5">
                        {item.code && (
                          <span className="text-[10px] font-bold text-blue-700 bg-blue-50 border border-blue-200 px-1.5 py-0.5 rounded">
                            {item.code}
                          </span>
                        )}
                        <span className="font-bold text-slate-900 text-sm">{nameLabel}</span>
                      </div>
                      {descLabel && <p className="text-xs text-slate-600 leading-relaxed font-normal">{descLabel}</p>}
                    </div>
                    {valLabel && !item.code && (
                      <span className="font-extrabold text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-lg border border-emerald-200 text-xs shrink-0">
                        {valLabel}
                      </span>
                    )}
                  </div>

                  {/* Policy PDF / Document viewer button */}
                  {item.documentPath && (
                    <div className="pt-2 border-t border-slate-200/60 flex items-center justify-between">
                      <span className="text-[11px] text-slate-500 font-medium">Official Document Available</span>
                      <a
                        href={item.documentPath.startsWith('http') ? item.documentPath : `http://localhost:5160/${item.documentPath.replace(/^\//, '')}`}
                        target="_blank"
                        rel="noreferrer"
                        className="px-3 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-bold transition-colors flex items-center gap-1.5 shadow-2xs"
                      >
                        <FileCheck className="w-3.5 h-3.5" /> View Official PDF Document
                      </a>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        {/* Custom Dedicated Result Cards */}
        {intent === 'LEAVE_CREATE' && (
          <div className="p-4 bg-emerald-50/60 rounded-xl border border-emerald-200 text-xs space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-bold text-emerald-900 text-sm">Official Leave Record Registered</span>
              <span className="bg-emerald-600 text-white font-bold text-[10px] px-2.5 py-0.5 rounded-full uppercase tracking-wider">
                Approved &amp; Synced
              </span>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-1">
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Employee</span>
                <span className="font-bold text-slate-800 text-xs">{data.employeeName || data.name || 'Ali'}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Department</span>
                <span className="font-bold text-slate-800 text-xs">{data.department || 'IT'}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Leave Date</span>
                <span className="font-bold text-slate-800 text-xs">{data.leaveDate || data.startDate || new Date().toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' })}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Team Alert</span>
                <span className="font-bold text-emerald-700 text-xs flex items-center gap-1">
                  <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" /> Dispatched via Gmail
                </span>
              </div>
            </div>
          </div>
        )}

        {intent === 'DEPARTMENT_CREATE' && (
          <div className="p-4 bg-blue-50/60 rounded-xl border border-blue-200 text-xs space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-bold text-blue-900 text-sm">New Corporate Department Established</span>
              <span className="bg-blue-600 text-white font-bold text-[10px] px-2.5 py-0.5 rounded-full uppercase tracking-wider">
                Active in Master
              </span>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 pt-1">
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Department Name</span>
                <span className="font-bold text-slate-800 text-xs">{data.name || data.department || 'Finance'}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Department Head</span>
                <span className="font-bold text-slate-800 text-xs">{data.head || 'Assigned Lead'}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Initial Budget</span>
                <span className="font-bold text-emerald-700 text-xs">
                  {data.budgetAmount ? `$${Number(data.budgetAmount).toLocaleString()}` : (data.amount ? `$${Number(data.amount).toLocaleString()}` : '$500,000')}
                </span>
              </div>
            </div>
          </div>
        )}

        {data?.averageSalary !== undefined && (
          <div className="p-4 bg-indigo-50/60 rounded-xl border border-indigo-200 text-xs space-y-3">
            <div className="flex items-center justify-between">
              <span className="font-bold text-indigo-900 text-sm">Department Compensation Benchmark</span>
              <span className="bg-indigo-600 text-white font-bold text-[10px] px-2.5 py-0.5 rounded-full uppercase tracking-wider">
                Verified Metric
              </span>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-1">
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Department</span>
                <span className="font-bold text-slate-800 text-xs">{data.department ?? 'IT'}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Average Salary</span>
                <span className="font-bold text-emerald-700 text-sm">${Number(data.averageSalary).toLocaleString()}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Salary Range</span>
                <span className="font-bold text-slate-800 text-xs">${Number(data.minSalary ?? 0).toLocaleString()} – ${Number(data.maxSalary ?? 0).toLocaleString()}</span>
              </div>
              <div>
                <span className="text-[10px] text-slate-400 uppercase font-semibold block">Active Headcount</span>
                <span className="font-bold text-slate-800 text-xs">{data.employeeCount ?? 1} employees</span>
              </div>
            </div>
          </div>
        )}

        {/* Key-value fields for single record operations */}
        {fields.length > 0 && (!rawList || rawList.length === 0) && !data?.documentId && !data?.downloadUrl && data?.fitScore === undefined && intent !== 'LEAVE_CREATE' && intent !== 'DEPARTMENT_CREATE' && data?.averageSalary === undefined && (
          <div className="grid grid-cols-2 gap-2">
            {fields.map(([key, val]) => (
              <div key={key} className="bg-slate-50 rounded-lg p-2.5 border border-slate-100">
                <span className="text-[10px] text-slate-400 uppercase tracking-wide block font-semibold">
                  {key.replace(/([A-Z])/g, ' $1').trim()}
                </span>
                <span className="text-xs font-bold text-slate-800">
                  {typeof val === 'number' && val > 1000 ? `$${val.toLocaleString()}` : String(val ?? '—')}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    );
  };

  // Execution feed timeline
  const renderFeed = (feed: any[]) => {
    if (!feed || feed.length === 0) return null;
    return (
      <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
        <button
          onClick={() => setFeedExpanded(v => !v)}
          className="w-full flex items-center justify-between px-4 py-3 text-xs font-bold text-slate-700 hover:bg-slate-50 transition-colors"
        >
          <span>Action Processing Progress ({feed.length} steps)</span>
          {feedExpanded ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5" />}
        </button>
        {feedExpanded && (
          <div className="border-t border-slate-100 divide-y divide-slate-50">
            {feed.map((evt: any, idx: number) => {
              const isError = evt.eventType?.includes('FAILED');
              const isSuccess = evt.eventType?.includes('COMPLETED');
              return (
                <div key={idx} className={`flex items-start gap-3 px-4 py-2.5 ${isError ? 'bg-rose-50/40' : ''}`}>
                  <span className={`mt-0.5 w-2 h-2 rounded-full shrink-0 ${isError ? 'bg-rose-500' : isSuccess ? 'bg-emerald-500' : 'bg-blue-400'}`} />
                  <div className="flex-1 min-w-0">
                    <div className={`text-[11px] font-bold ${isError ? 'text-rose-700' : 'text-slate-600'}`}>
                      {evt.eventType?.replace(/_/g, ' ')}
                    </div>
                    <div className="text-xs text-slate-700 truncate">{evt.message}</div>
                  </div>
                  <span className="text-[10px] text-slate-400 shrink-0">
                    {new Date(evt.timestamp).toLocaleTimeString()}
                  </span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  };

  const renderConversationalResult = (message: string) => {
    return (
      <div className="bg-gradient-to-r from-blue-50/80 to-indigo-50/80 rounded-xl border border-blue-200/80 p-5 shadow-xs flex items-start gap-4">
        <div className="p-2.5 rounded-xl bg-blue-600 text-white shadow-sm shrink-0">
          <Bot className="w-5 h-5" />
        </div>
        <div className="space-y-1.5 flex-1">
          <div className="text-xs font-bold text-blue-900 uppercase tracking-wider flex items-center gap-2">
            <span>Nexus AI Assistant</span>
            <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-[10px] font-semibold">Conversational</span>
          </div>
          <div className="text-sm font-medium text-slate-800 leading-relaxed whitespace-pre-line">
            {message}
          </div>
        </div>
      </div>
    );
  };

  const renderFilteredEmployees = (data: any) => {
    const emps = data?.employees || [];
    const dept = data?.department || 'Requested Department';
    return (
      <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-blue-50 text-blue-600 rounded-xl">
              <Users className="w-5 h-5" />
            </div>
            <div>
              <h4 className="text-sm font-bold text-slate-900">Active Staff Directory — {dept}</h4>
              <p className="text-xs text-slate-500">{data.summary || `Found ${emps.length} active employee(s)`}</p>
            </div>
          </div>
          <span className="px-3 py-1 bg-emerald-50 text-emerald-700 border border-emerald-200 rounded-full text-xs font-bold">
            {emps.length} Active Records
          </span>
        </div>

        {emps.length > 0 ? (
          <div className="overflow-hidden border border-slate-200 rounded-xl">
            <table className="min-w-full divide-y divide-slate-200 text-xs">
              <thead className="bg-slate-50 font-bold text-slate-600 uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="px-4 py-3 text-left">Employee Name</th>
                  <th className="px-4 py-3 text-left">Designation</th>
                  <th className="px-4 py-3 text-left">Department</th>
                  <th className="px-4 py-3 text-left">Reporting Manager</th>
                  <th className="px-4 py-3 text-left">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {emps.map((emp: any, idx: number) => (
                  <tr key={idx} className="hover:bg-slate-50 transition-colors">
                    <td className="px-4 py-3 font-bold text-slate-900 flex items-center gap-2">
                      <div className="w-6 h-6 rounded-full bg-blue-100 text-blue-700 flex items-center justify-center font-bold text-[10px]">
                        {emp.name ? emp.name.charAt(0) : 'E'}
                      </div>
                      <span>{emp.name}</span>
                    </td>
                    <td className="px-4 py-3 font-semibold text-slate-700">{emp.designation || 'Staff'}</td>
                    <td className="px-4 py-3 text-slate-600">
                      <span className="px-2 py-0.5 rounded-md bg-slate-100 font-medium text-[11px] text-slate-700">
                        {emp.department || dept}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{emp.manager || 'Executive'}</td>
                    <td className="px-4 py-3">
                      <span className="inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-full font-bold uppercase tracking-wider bg-emerald-100 text-emerald-800 border border-emerald-300">
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                        {emp.status || 'Active'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="p-4 bg-slate-50 border border-slate-200 rounded-xl text-xs text-slate-600 text-center">
            No active employee records found matching criteria.
          </div>
        )}
      </div>
    );
  };

  const renderJobOpeningResult = (data: any) => {
    const title = data?.title || 'Web Developer';
    const dept = data?.department || 'IT';
    const reqs = data?.requirements || 'Technical Skills';
    const isStandardPort = window.location.port === '3000' || window.location.port === '5173';
    const link = isStandardPort 
      ? `http://localhost:3001/?jobId=${data?.jobOpeningId || 1}`
      : `${window.location.origin}/?portal=candidate&jobId=${data?.jobOpeningId || 1}`;

    return (
      <div className="bg-white rounded-2xl border border-indigo-200 p-6 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-xl">
              <Briefcase className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-bold uppercase tracking-wider text-indigo-700 bg-indigo-50 border border-indigo-200 px-2 py-0.5 rounded-full">
                  Job Opening Created
                </span>
                <span className="text-[10px] font-bold uppercase text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded-full">
                  Live in Job Opening Tab
                </span>
              </div>
              <h4 className="text-base font-bold text-slate-900 mt-1">{title} ({dept})</h4>
            </div>
          </div>
          <button
            onClick={() => onNavigate?.('jobs')}
            className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer shadow-xs"
          >
            <span>View in Job Opening Tab</span>
            <ArrowUpRight className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-2 text-xs">
          <div>
            <span className="text-slate-400 font-medium">Requisition Title:</span>{' '}
            <span className="font-bold text-slate-800">{title}</span>
          </div>
          <div>
            <span className="text-slate-400 font-medium">Target Department:</span>{' '}
            <span className="font-bold text-slate-800">{dept}</span>
          </div>
          <div>
            <span className="text-slate-400 font-medium">Technical Requirements:</span>{' '}
            <span className="font-semibold text-slate-700">{reqs}</span>
          </div>
        </div>

        <div className="p-3 bg-blue-50/70 border border-blue-200 rounded-xl flex items-center justify-between gap-3 text-xs">
          <div className="flex items-center gap-2 text-blue-900 truncate">
            <span className="font-semibold">Candidate Portal Link:</span>
            <span className="font-mono text-blue-700 text-[11px] truncate select-all">{link}</span>
          </div>
          <button
            onClick={() => {
              navigator.clipboard.writeText(link);
              alert('Candidate Application Link copied to clipboard!');
            }}
            className="px-3 py-1 bg-white hover:bg-blue-100 text-blue-700 border border-blue-300 rounded-lg text-[11px] font-bold shrink-0 transition-colors cursor-pointer"
          >
            Copy Link
          </button>
        </div>

        <p className="text-xs text-slate-500 italic">
          The new job opening has been created in the Job Opening tab. Candidates can submit their resumes via the public application link.
        </p>
      </div>
    );
  };

  const renderTicketResult = (data: any, intent: string) => {
    const isUpdate = intent === 'TICKET_UPDATE' || data?.status === 'Resolved';
    const ticketId = data?.ticketId || 'TCK-IT-001';
    const summary = data?.summary || data?.message || `Ticket ${ticketId} processed.`;
    const employee = data?.employee || data?.employeeName || 'Sarah';
    const dept = data?.department || 'DevOps';
    const details = data?.details || 'MacBook Pro M3 and AWS VPN access';

    return (
      <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-3">
            <div className={`p-2.5 rounded-xl ${isUpdate ? 'bg-emerald-50 text-emerald-600' : 'bg-blue-50 text-blue-600'}`}>
              <Ticket className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-mono font-bold uppercase text-slate-700 bg-slate-100 border border-slate-200 px-2 py-0.5 rounded">
                  {ticketId}
                </span>
                <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded-full border ${
                  isUpdate ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-blue-50 text-blue-700 border-blue-200'
                }`}>
                  {isUpdate ? 'Resolved' : 'Open Ticket'}
                </span>
              </div>
              <h4 className="text-sm font-bold text-slate-900 mt-1">{summary}</h4>
            </div>
          </div>
          <button
            onClick={() => onNavigate?.('tickets')}
            className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer shadow-xs"
          >
            <span>Service Desk</span>
            <ArrowUpRight className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 bg-slate-50 border border-slate-200 rounded-xl p-3.5 text-xs">
          <div>
            <span className="text-slate-400 block text-[10px]">Employee</span>
            <span className="font-bold text-slate-800">{employee}</span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Department</span>
            <span className="font-bold text-slate-800">{dept}</span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Provisioning / Details</span>
            <span className="font-semibold text-slate-700">{details}</span>
          </div>
        </div>
      </div>
    );
  };

  const renderCvScreenResult = (data: any) => {
    const name = data?.candidateName || 'Candidate';
    const score = data?.matchScore || 85;
    const isBestFit = data?.isBestFit === true || score >= 80;
    const questions = data?.recommendedInterviewQuestions || [];

    return (
      <div className="bg-white rounded-2xl border border-indigo-200 p-6 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-xl">
              <Sparkles className="w-5 h-5 text-indigo-600" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className={`text-[10px] font-bold uppercase px-2.5 py-0.5 rounded-full border flex items-center gap-1 ${
                  isBestFit ? 'bg-emerald-50 text-emerald-700 border-emerald-300' : 'bg-blue-50 text-blue-700 border-blue-300'
                }`}>
                  {isBestFit && <Star className="w-3 h-3 fill-current text-emerald-600" />}
                  {isBestFit ? 'Best Fit For This Position' : 'Strong Match'}
                </span>
              </div>
              <h4 className="text-base font-bold text-slate-900 mt-1">{name} — Fit Score: {score}%</h4>
            </div>
          </div>
          <button
            onClick={() => onNavigate?.('cv')}
            className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer shadow-xs"
          >
            <span>Open in CV Tab</span>
            <ArrowUpRight className="w-3.5 h-3.5" />
          </button>
        </div>

        {data?.fitSummary && (
          <p className="text-xs text-slate-700 leading-relaxed bg-slate-50 p-3 rounded-xl border border-slate-200">
            {data.fitSummary}
          </p>
        )}

        {questions.length > 0 && (
          <div className="space-y-1.5 bg-indigo-50/50 border border-indigo-100 rounded-xl p-3.5 text-xs">
            <span className="font-bold text-indigo-900 block text-[11px] uppercase tracking-wider">
              Recommended Interview Questions:
            </span>
            <ol className="list-decimal pl-4 space-y-1 text-slate-700">
              {questions.map((q: string, idx: number) => (
                <li key={idx}>{q}</li>
              ))}
            </ol>
          </div>
        )}
      </div>
    );
  };

  const renderExpenseResult = (data: any, intent: string, _res: AgentResult) => {
    if (!data) return null;

    const handleQuickAction = async (expenseId: number, status: string, reason?: string) => {
      setExpenseActionId(expenseId);
      try {
        const updated = await updateExpenseStatus(expenseId, status, reason);
        setExpenseFeedback(`Claim ${updated.claimNumber || '#' + expenseId} marked as ${status} successfully.`);
        setTimeout(() => setExpenseFeedback(null), 4000);
        window.dispatchEvent(new CustomEvent('nexus-data-updated'));

        // Update local result state directly WITHOUT re-executing prompt!
        setResult(prev => {
          if (!prev || !prev.resultData) return prev;
          const currentData = { ...(prev.resultData as any) };

          // If single claim result (e.g. EXPENSE_CREATE, EXPENSE_APPROVE, EXPENSE_REJECT)
          if (currentData.claim) {
            currentData.claim = {
              ...currentData.claim,
              status: updated.status,
              statusName: updated.statusName,
              complianceStatus: updated.complianceStatus
            };
          }

          // If claims array (e.g. EXPENSE_READ, EXPENSE_FILTER, EXPENSE_POLICY_VARIANCE)
          if (Array.isArray(currentData.claims)) {
            currentData.claims = currentData.claims.map((c: any) => {
              const cId = c.id || (c.claimNumber ? parseInt(c.claimNumber.replace(/\D/g, '')) : 0);
              if (cId === expenseId || c.claimNumber === updated.claimNumber) {
                return {
                  ...c,
                  status: updated.status,
                  statusName: updated.statusName,
                  complianceStatus: updated.complianceStatus
                };
              }
              return c;
            });
          }

          // If flaggedClaims array (e.g. EXPENSE_COMPLIANCE_SWEEP)
          if (Array.isArray(currentData.flaggedClaims)) {
            currentData.flaggedClaims = currentData.flaggedClaims.map((c: any) => {
              const cId = c.id || (c.claimNumber ? parseInt(c.claimNumber.replace(/\D/g, '')) : 0);
              if (cId === expenseId || c.claimNumber === updated.claimNumber) {
                return {
                  ...c,
                  status: updated.status,
                  statusName: updated.statusName,
                  complianceStatus: updated.complianceStatus
                };
              }
              return c;
            });
          }

          return { ...prev, resultData: currentData };
        });
      } catch (err: any) {
        setExpenseFeedback(`Action failed: ${err.message || 'Server error'}`);
        setTimeout(() => setExpenseFeedback(null), 4000);
      } finally {
        setExpenseActionId(null);
      }
    };

    // 1. EXPENSE_COMPLIANCE_SWEEP
    if (intent === 'EXPENSE_COMPLIANCE_SWEEP') {
      const reviewed = data.claimsReviewed ?? 0;
      const compliant = data.compliantClaims ?? 0;
      const flagged = data.flaggedClaimsCount ?? data.flaggedClaims?.length ?? 0;
      const totalVar = Number(data.totalPolicyVariance ?? 0);
      const flaggedList: any[] = data.flaggedClaims ?? [];

      return (
        <div className="bg-white rounded-2xl border border-blue-200 p-6 shadow-xs space-y-5">
          {expenseFeedback && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-xs font-bold text-emerald-800 flex items-center justify-between animate-in fade-in">
              <span className="flex items-center gap-1.5">
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                {expenseFeedback}
              </span>
              <button onClick={() => setExpenseFeedback(null)} className="text-emerald-700 hover:text-emerald-900 cursor-pointer">
                &times;
              </button>
            </div>
          )}

          <div className="flex items-center justify-between border-b border-slate-100 pb-4">
            <div className="flex items-center gap-3">
              <div className="p-3 bg-blue-50 text-blue-600 rounded-xl border border-blue-100">
                <ShieldCheck className="w-6 h-6 text-blue-600" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-bold text-slate-900">AI Policy Compliance Sweep</h3>
                  <span className="text-[10px] bg-blue-50 text-blue-700 border border-blue-200 font-mono font-bold px-2 py-0.5 rounded">
                    POL-FIN-002 ENFORCED
                  </span>
                </div>
                <p className="text-xs text-slate-500 mt-0.5">
                  Automated audit sweep executed across all submitted employee expense claims.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition shadow-xs cursor-pointer"
            >
              <span>Open in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <div className="bg-slate-50 border border-slate-200 rounded-xl p-3.5">
              <span className="text-[10px] uppercase font-bold text-slate-400 tracking-wider block">Claims Reviewed</span>
              <span className="text-xl font-bold text-slate-900 mt-0.5 block">{reviewed}</span>
              <span className="text-[10px] text-slate-500 font-medium">100% evaluated</span>
            </div>

            <div className="bg-emerald-50/60 border border-emerald-200 rounded-xl p-3.5">
              <span className="text-[10px] uppercase font-bold text-emerald-600 tracking-wider block">Compliant Claims</span>
              <span className="text-xl font-bold text-emerald-700 mt-0.5 block">{compliant}</span>
              <span className="text-[10px] text-emerald-600 font-medium">Within category caps</span>
            </div>

            <div className="bg-rose-50/60 border border-rose-200 rounded-xl p-3.5">
              <span className="text-[10px] uppercase font-bold text-rose-600 tracking-wider block">Policy Violations</span>
              <span className="text-xl font-bold text-rose-700 mt-0.5 block">{flagged}</span>
              <span className="text-[10px] text-rose-600 font-medium">Flagged for review</span>
            </div>

            <div className="bg-amber-50/60 border border-amber-200 rounded-xl p-3.5">
              <span className="text-[10px] uppercase font-bold text-amber-600 tracking-wider block">Total Policy Variance</span>
              <span className="text-xl font-bold text-amber-700 mt-0.5 block">+${totalVar.toFixed(2)}</span>
              <span className="text-[10px] text-amber-600 font-medium">Excess over policy cap</span>
            </div>
          </div>

          {flaggedList.length > 0 && (
            <div className="space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold uppercase tracking-wider text-rose-800 flex items-center gap-1.5">
                  <AlertTriangle className="w-3.5 h-3.5 text-rose-600" />
                  Flagged Non-Compliant Claims ({flaggedList.length}):
                </span>
                <span className="text-[10px] text-slate-400 font-medium">
                  POL-FIN-002 thresholds: Meal $50 • Travel $250 • Equipment $500
                </span>
              </div>

              <div className="border border-slate-200 rounded-xl overflow-hidden shadow-2xs">
                <table className="w-full text-left text-xs">
                  <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider text-[10px]">
                    <tr>
                      <th className="py-2.5 px-3">Claim #</th>
                      <th className="py-2.5 px-3">Employee</th>
                      <th className="py-2.5 px-3">Category</th>
                      <th className="py-2.5 px-3">Claimed</th>
                      <th className="py-2.5 px-3">Policy Cap</th>
                      <th className="py-2.5 px-3">Variance</th>
                      <th className="py-2.5 px-3">Flag Reason</th>
                      <th className="py-2.5 px-3 text-right">Quick Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {flaggedList.map((f: any, idx: number) => {
                      const cNo = f.claimNumber || `EXP-${idx + 1}`;
                      const emp = f.employeeName || f.employee || 'Employee';
                      const cat = f.category || 'General';
                      const amt = Number(f.amount || 0);
                      const cap = Number(f.policyLimit || 0);
                      const v = Number(f.variance || (amt - cap));
                      const reason = f.flagReason || f.reason || 'Exceeds policy limit';
                      const claimId = f.id || (f.claimNumber ? parseInt(f.claimNumber.replace(/\D/g, '')) : idx);

                      return (
                        <tr key={idx} className="hover:bg-slate-50/70 transition">
                          <td className="py-2.5 px-3 font-mono font-bold text-blue-700">{cNo}</td>
                          <td className="py-2.5 px-3 font-bold text-slate-900">{emp}</td>
                          <td className="py-2.5 px-3 text-slate-600">{cat}</td>
                          <td className="py-2.5 px-3 font-bold text-slate-900">${amt.toFixed(2)}</td>
                          <td className="py-2.5 px-3 text-slate-500">${cap.toFixed(2)}</td>
                          <td className="py-2.5 px-3 font-bold text-rose-600">
                            +{v.toFixed(2)}
                          </td>
                          <td className="py-2.5 px-3 text-slate-500 text-[11px] max-w-xs truncate" title={reason}>
                            {reason}
                          </td>
                          <td className="py-2.5 px-3 text-right">
                            <div className="flex items-center justify-end gap-1">
                              {expenseActionId === claimId ? (
                                <span className="text-[10px] text-slate-400 font-bold">Saving...</span>
                              ) : (
                                <>
                                  <button
                                    onClick={() => handleQuickAction(claimId, 'Approved', 'Approved by Manager')}
                                    className="px-2 py-0.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-200 rounded text-[10px] font-bold transition cursor-pointer"
                                    title="Approve Claim"
                                  >
                                    Approve
                                  </button>
                                  <button
                                    onClick={() => handleQuickAction(claimId, 'Rejected', 'Policy violation')}
                                    className="px-2 py-0.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded text-[10px] font-bold transition cursor-pointer"
                                    title="Reject Claim"
                                  >
                                    Reject
                                  </button>
                                </>
                              )}
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      );
    }

    // 2. EXPENSE_FILTER
    if (intent === 'EXPENSE_FILTER') {
      const claims: any[] = data.claims ?? (Array.isArray(data) ? data : []);
      const count = data.count ?? claims.length;

      return (
        <div className="bg-white rounded-2xl border border-rose-200 p-6 shadow-xs space-y-4">
          {expenseFeedback && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-xs font-bold text-emerald-800 flex items-center justify-between animate-in fade-in">
              <span className="flex items-center gap-1.5">
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                {expenseFeedback}
              </span>
              <button onClick={() => setExpenseFeedback(null)} className="text-emerald-700 hover:text-emerald-900 cursor-pointer">
                &times;
              </button>
            </div>
          )}

          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-rose-50 text-rose-600 rounded-xl border border-rose-100">
                <AlertTriangle className="w-5 h-5 text-rose-600" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-bold text-slate-900">Filtered Policy Violations (Flagged)</h3>
                  <span className="text-[10px] bg-rose-50 text-rose-700 border border-rose-200 font-bold px-2 py-0.5 rounded-full">
                    {count} Violations
                  </span>
                </div>
                <p className="text-xs text-slate-500 mt-0.5">
                  Filtered by non-compliant status under Corporate Expense Policy POL-FIN-002.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>Open in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="border border-slate-200 rounded-xl overflow-hidden shadow-2xs">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="py-2.5 px-3">Claim #</th>
                  <th className="py-2.5 px-3">Employee</th>
                  <th className="py-2.5 px-3">Category</th>
                  <th className="py-2.5 px-3">Claimed</th>
                  <th className="py-2.5 px-3">Policy Cap</th>
                  <th className="py-2.5 px-3">Variance</th>
                  <th className="py-2.5 px-3">Status</th>
                  <th className="py-2.5 px-3 text-right">Quick Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {claims.map((c: any, idx: number) => {
                  const amt = Number(c.amount || 0);
                  const cap = Number(c.policyLimit || 50);
                  const v = Number(c.variance ?? (amt - cap));
                  const claimId = c.id || (c.claimNumber ? parseInt(c.claimNumber.replace(/\D/g, '')) : idx);

                  return (
                    <tr key={idx} className="hover:bg-slate-50/70 transition">
                      <td className="py-2.5 px-3 font-mono font-bold text-blue-700">{c.claimNumber}</td>
                      <td className="py-2.5 px-3 font-bold text-slate-900">{c.employeeName}</td>
                      <td className="py-2.5 px-3 text-slate-600">{c.category}</td>
                      <td className="py-2.5 px-3 font-bold text-slate-900">${amt.toFixed(2)}</td>
                      <td className="py-2.5 px-3 text-slate-500">${cap.toFixed(2)}</td>
                      <td className="py-2.5 px-3 font-bold text-rose-600">+${v.toFixed(2)}</td>
                      <td className="py-2.5 px-3">
                        <span className="text-[10px] font-bold bg-rose-50 text-rose-700 border border-rose-200 px-2 py-0.5 rounded-full">
                          FLAGGED
                        </span>
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <div className="flex items-center justify-end gap-1">
                          {expenseActionId === claimId ? (
                            <span className="text-[10px] text-slate-400 font-bold">Saving...</span>
                          ) : (
                            <>
                              <button
                                onClick={() => handleQuickAction(claimId, 'Approved', 'Approved by Manager')}
                                className="px-2 py-0.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-200 rounded text-[10px] font-bold transition cursor-pointer"
                                title="Approve Claim"
                              >
                                Approve
                              </button>
                              <button
                                onClick={() => handleQuickAction(claimId, 'Rejected', 'Policy violation')}
                                className="px-2 py-0.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded text-[10px] font-bold transition cursor-pointer"
                                title="Reject Claim"
                              >
                                Reject
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      );
    }

    // 3. EXPENSE_CREATE
    if (intent === 'EXPENSE_CREATE') {
      const claim = data.claim;
      if (!claim) {
        return (
          <div className="bg-white rounded-2xl border border-amber-200 p-5 text-xs text-amber-800 font-medium">
            {data.summary || data.error || 'Expense claim creation encountered an issue.'}
          </div>
        );
      }

      const amt = Number(claim.amount || 0);
      const cap = Number(claim.policyLimit || 50);
      const v = Number(claim.variance ?? (amt - cap));
      const statusText = (claim.statusName || (claim.status === 2 ? 'Approved' : (claim.status === 3 ? 'Rejected' : claim.complianceStatus || 'Pending'))).toString();
      const isApproved = statusText.toLowerCase().includes('approved') || claim.status === 2;
      const isRejected = statusText.toLowerCase().includes('rejected') || claim.status === 3;
      const isViol = !isApproved && !isRejected && (v > 0 || statusText.toLowerCase().includes('flag') || statusText.toLowerCase().includes('noncompliant'));
      const claimId = claim.id || (claim.claimNumber ? parseInt(claim.claimNumber.replace(/\D/g, '')) : 0);

      return (
        <div className="bg-white rounded-2xl border border-emerald-200 p-6 shadow-xs space-y-5">
          {expenseFeedback && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-xs font-bold text-emerald-800 flex items-center justify-between animate-in fade-in">
              <span className="flex items-center gap-1.5">
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                {expenseFeedback}
              </span>
              <button onClick={() => setExpenseFeedback(null)} className="text-emerald-700 hover:text-emerald-900 cursor-pointer">
                &times;
              </button>
            </div>
          )}

          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-emerald-50 text-emerald-600 rounded-xl border border-emerald-100">
                <CheckCircle2 className="w-5 h-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-bold text-slate-900">Expense Claim Created Successfully</h3>
                  <span className="text-[10px] bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-2 py-0.5 rounded-full">
                    SAVED TO DATABASE
                  </span>
                </div>
                <p className="text-xs text-slate-500 mt-0.5">
                  Record stored in database with automatic POL-FIN-002 limit assessment.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>View in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-slate-50 border border-slate-200 rounded-xl p-4 text-xs">
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Claim Number</span>
              <span className="font-mono font-bold text-blue-700 text-sm">{claim.claimNumber}</span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Employee</span>
              <span className="font-bold text-slate-900 text-sm">{claim.employeeName}</span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Category</span>
              <span className="font-semibold text-slate-700 text-xs">{claim.category} ({claim.description})</span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Claimed Amount</span>
              <span className="font-bold text-slate-900 text-sm">${amt.toFixed(2)}</span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Policy Cap</span>
              <span className="font-semibold text-slate-600 text-xs">${cap.toFixed(2)} (POL-FIN-002)</span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Policy Variance</span>
              <span className={`font-bold text-xs ${isViol ? 'text-rose-600' : 'text-emerald-600'}`}>
                {isViol ? `+$${v.toFixed(2)} (Exceeds Cap)` : `-$${Math.abs(v).toFixed(2)} (Within Cap)`}
              </span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Compliance Status</span>
              <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full inline-block mt-0.5 ${
                isApproved ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' :
                isRejected ? 'bg-rose-50 text-rose-700 border border-rose-200' :
                isViol ? 'bg-amber-50 text-amber-700 border border-amber-200' : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
              }`}>
                {isApproved ? 'APPROVED' : isRejected ? 'REJECTED' : isViol ? 'FLAGGED FOR REVIEW' : 'COMPLIANT'}
              </span>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Workflow Status</span>
              <span className="font-bold text-slate-700 text-xs">
                {isApproved ? 'Approved by Manager' : isRejected ? 'Rejected' : 'Pending Review'}
              </span>
            </div>
          </div>

          <div className="pt-2 flex items-center justify-end gap-2 border-t border-slate-100">
            {expenseActionId === claimId ? (
              <span className="text-xs text-slate-500 font-semibold">Updating database...</span>
            ) : isApproved ? (
              <span className="text-xs font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-3 py-1.5 rounded-lg flex items-center gap-1.5">
                <Check className="w-3.5 h-3.5" /> Approved by Manager
              </span>
            ) : isRejected ? (
              <span className="text-xs font-bold text-rose-700 bg-rose-50 border border-rose-200 px-3 py-1.5 rounded-lg flex items-center gap-1.5">
                <XCircle className="w-3.5 h-3.5" /> Rejected by Manager
              </span>
            ) : (
              <>
                <button
                  onClick={() => handleQuickAction(claimId, 'Approved', 'Approved by Manager')}
                  className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition flex items-center gap-1 cursor-pointer"
                >
                  <Check className="w-3.5 h-3.5" />
                  <span>Approve Claim</span>
                </button>
                <button
                  onClick={() => handleQuickAction(claimId, 'Rejected', 'Exceeds meal policy cap')}
                  className="px-3 py-1.5 bg-white hover:bg-rose-50 text-rose-600 border border-rose-200 rounded-lg text-xs font-bold transition flex items-center gap-1 cursor-pointer"
                >
                  <XCircle className="w-3.5 h-3.5" />
                  <span>Reject Claim</span>
                </button>
              </>
            )}
          </div>
        </div>
      );
    }

    // 4. EXPENSE_READ (Over limit query)
    if (intent === 'EXPENSE_READ') {
      const claims: any[] = data.claims ?? [];
      return (
        <div className="bg-white rounded-2xl border border-amber-200 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-amber-50 text-amber-600 rounded-xl border border-amber-100">
                <Receipt className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-bold text-slate-900">Expense Claims Exceeding Policy Threshold</h3>
                  <span className="text-[10px] bg-amber-50 text-amber-700 border border-amber-200 font-bold px-2 py-0.5 rounded-full">
                    {claims.length} Claims Found
                  </span>
                </div>
                <p className="text-xs text-slate-500 mt-0.5">
                  Filtered against daily meal limit cap ($50.00) under POL-FIN-002.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>Open in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="border border-slate-200 rounded-xl overflow-hidden shadow-2xs">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="py-2.5 px-3">Claim #</th>
                  <th className="py-2.5 px-3">Employee</th>
                  <th className="py-2.5 px-3">Claimed</th>
                  <th className="py-2.5 px-3">Policy Cap</th>
                  <th className="py-2.5 px-3">Variance</th>
                  <th className="py-2.5 px-3">Status</th>
                  <th className="py-2.5 px-3 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {claims.map((c: any, idx: number) => {
                  const amt = Number(c.amount || 0);
                  const cap = Number(data.policyLimit || 50);
                  const v = Number(c.variance ?? (amt - cap));
                  const statusText = (c.statusName || (c.status === 2 ? 'Approved' : (c.status === 3 ? 'Rejected' : c.complianceStatus || c.status || 'Pending'))).toString();
                  const isApproved = statusText.toLowerCase().includes('approved') || c.status === 2;
                  const isRejected = statusText.toLowerCase().includes('rejected') || c.status === 3;
                  const isFlagged = !isApproved && !isRejected && (statusText.toLowerCase().includes('flag') || statusText.toLowerCase().includes('noncompliant'));
                  const claimId = c.id || (c.claimNumber ? parseInt(c.claimNumber.replace(/\D/g, '')) : idx);

                  return (
                    <tr key={idx} className="hover:bg-slate-50/70 transition">
                      <td className="py-2.5 px-3 font-mono font-bold text-blue-700">{c.claimNumber}</td>
                      <td className="py-2.5 px-3 font-bold text-slate-900">{c.employee || c.employeeName}</td>
                      <td className="py-2.5 px-3 font-bold text-slate-900">${amt.toFixed(2)}</td>
                      <td className="py-2.5 px-3 text-slate-500">${cap.toFixed(2)}</td>
                      <td className="py-2.5 px-3 font-bold text-rose-600">+{v.toFixed(2)}</td>
                      <td className="py-2.5 px-3">
                        <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                          isApproved
                            ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                            : isRejected
                            ? 'bg-rose-50 text-rose-700 border border-rose-200'
                            : isFlagged
                            ? 'bg-amber-50 text-amber-700 border border-amber-200'
                            : 'bg-blue-50 text-blue-700 border border-blue-200'
                        }`}>
                          {isApproved ? 'APPROVED' : isRejected ? 'REJECTED' : isFlagged ? 'FLAGGED' : 'PENDING'}
                        </span>
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <div className="flex items-center justify-end gap-1">
                          {expenseActionId === claimId ? (
                            <span className="text-[10px] text-slate-400 font-bold">Saving...</span>
                          ) : isApproved ? (
                            <span className="text-[10px] font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                              Approved
                            </span>
                          ) : isRejected ? (
                            <span className="text-[10px] font-bold text-rose-600 bg-rose-50 px-2 py-0.5 rounded border border-rose-200">
                              Rejected
                            </span>
                          ) : (
                            <>
                              <button
                                onClick={() => handleQuickAction(claimId, 'Approved', 'Approved by Manager')}
                                className="px-2 py-0.5 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-200 rounded text-[10px] font-bold transition cursor-pointer"
                                title="Approve Claim"
                              >
                                Approve
                              </button>
                              <button
                                onClick={() => handleQuickAction(claimId, 'Rejected', 'Exceeds meal policy limit')}
                                className="px-2 py-0.5 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded text-[10px] font-bold transition cursor-pointer"
                                title="Reject Claim"
                              >
                                Reject
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      );
    }

    // 5. EXPENSE_APPROVE
    if (intent === 'EXPENSE_APPROVE') {
      const claim = data.claim;
      return (
        <div className="bg-white rounded-2xl border border-emerald-200 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-emerald-50 text-emerald-600 rounded-xl border border-emerald-100">
                <CheckCircle2 className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">Expense Claim Approved</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Reimbursement authorized and state committed to SQL database.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>View in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          {claim && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-slate-50 border border-slate-200 rounded-xl p-4 text-xs">
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Claim #</span>
                <span className="font-mono font-bold text-blue-700 text-sm">{claim.claimNumber}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Employee</span>
                <span className="font-bold text-slate-900 text-sm">{claim.employeeName}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Amount Approved</span>
                <span className="font-bold text-emerald-700 text-sm">${Number(claim.amount || 0).toFixed(2)}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Reviewed By</span>
                <span className="font-bold text-slate-800 text-xs">{claim.reviewedBy || 'Admin'}</span>
              </div>
            </div>
          )}
        </div>
      );
    }

    // 6. EXPENSE_REJECT
    if (intent === 'EXPENSE_REJECT') {
      const claim = data.claim;
      return (
        <div className="bg-white rounded-2xl border border-rose-200 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-rose-50 text-rose-600 rounded-xl border border-rose-100">
                <AlertTriangle className="w-5 h-5 text-rose-600" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">Expense Claim Rejected</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Claim declined due to corporate policy non-compliance.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>View in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          {claim && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-slate-50 border border-slate-200 rounded-xl p-4 text-xs">
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Claim #</span>
                <span className="font-mono font-bold text-blue-700 text-sm">{claim.claimNumber}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Category</span>
                <span className="font-bold text-slate-900 text-sm">{claim.category}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Amount</span>
                <span className="font-bold text-rose-700 text-sm">${Number(claim.amount || 0).toFixed(2)}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Rejection Reason</span>
                <span className="font-semibold text-rose-600 text-xs">{claim.flagReason || 'Policy violation'}</span>
              </div>
            </div>
          )}
        </div>
      );
    }

    // 7. EXPENSE_FLAG
    if (intent === 'EXPENSE_FLAG') {
      const claim = data.claim;
      return (
        <div className="bg-white rounded-2xl border border-amber-200 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-amber-50 text-amber-600 rounded-xl border border-amber-100">
                <AlertTriangle className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">Expense Claim Flagged for Review</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Escalated to management for corporate policy compliance assessment.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-amber-600 hover:bg-amber-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>Review in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          {claim && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-slate-50 border border-slate-200 rounded-xl p-4 text-xs">
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Claim #</span>
                <span className="font-mono font-bold text-blue-700 text-sm">{claim.claimNumber}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Employee</span>
                <span className="font-bold text-slate-900 text-sm">{claim.employeeName}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Amount</span>
                <span className="font-bold text-slate-900 text-sm">${Number(claim.amount || 0).toFixed(2)}</span>
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold text-slate-400 block">Flag Reason</span>
                <span className="font-semibold text-amber-700 text-xs">{claim.flagReason || 'Manager Policy Review Required'}</span>
              </div>
            </div>
          )}
        </div>
      );
    }

    // 8. EXPENSE_ANALYTICS (Monthly Summary)
    if (intent === 'EXPENSE_ANALYTICS') {
      const month = data.month || 'Current Month';
      const submitted = Number(data.totalSubmitted || 0);
      const count = Number(data.totalCount || 0);
      const pending = Number(data.pendingAmount || 0);
      const approved = Number(data.approvedAmount || 0);
      const flagged = Number(data.flaggedAmount || 0);

      return (
        <div className="bg-white rounded-2xl border border-indigo-200 p-6 shadow-xs space-y-5">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-indigo-50 text-indigo-600 rounded-xl border border-indigo-100">
                <BarChart3 className="w-5 h-5 text-indigo-600" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">Expense Reimbursement Summary — {month}</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Monthly financial totals aggregated from SQL Server database.
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>View All Claims</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
            <div className="bg-slate-50 border border-slate-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Total Submitted</span>
              <span className="text-lg font-bold text-slate-900 mt-0.5 block">${submitted.toFixed(2)}</span>
              <span className="text-[10px] text-slate-500">{count} Total Claims</span>
            </div>
            <div className="bg-blue-50/60 border border-blue-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-blue-600 block">Pending</span>
              <span className="text-lg font-bold text-blue-700 mt-0.5 block">${pending.toFixed(2)}</span>
              <span className="text-[10px] text-blue-600">Awaiting review</span>
            </div>
            <div className="bg-emerald-50/60 border border-emerald-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-emerald-600 block">Approved</span>
              <span className="text-lg font-bold text-emerald-700 mt-0.5 block">${approved.toFixed(2)}</span>
              <span className="text-[10px] text-emerald-600">Authorized</span>
            </div>
            <div className="bg-rose-50/60 border border-rose-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-rose-600 block">Flagged Violations</span>
              <span className="text-lg font-bold text-rose-700 mt-0.5 block">${flagged.toFixed(2)}</span>
              <span className="text-[10px] text-rose-600">Over policy caps</span>
            </div>
            <div className="bg-purple-50/60 border border-purple-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-purple-600 block">Policy Rules</span>
              <span className="text-xs font-bold text-purple-900 mt-1 block">POL-FIN-002</span>
              <span className="text-[10px] text-purple-600">Meal $50 • Travel $250</span>
            </div>
          </div>
        </div>
      );
    }

    // 9. EXPENSE_CATEGORY_ANALYTICS
    if (intent === 'EXPENSE_CATEGORY_ANALYTICS') {
      const categories: any[] = data.categories ?? [];
      const totalClaims = data.totalClaims ?? categories.reduce((sum, c) => sum + (c.claims || 0), 0);
      const totalAmt = Number(data.totalAmount || categories.reduce((sum, c) => sum + (c.totalAmount || 0), 0));

      return (
        <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-blue-50 text-blue-600 rounded-xl border border-blue-100">
                <BarChart3 className="w-5 h-5 text-blue-600" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">Expense Claims Breakdown by Category</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Total Claims: {totalClaims} • Total Amount: ${totalAmt.toFixed(2)}
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>Open in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="border border-slate-200 rounded-xl overflow-hidden shadow-2xs">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="py-2.5 px-3">Category</th>
                  <th className="py-2.5 px-3">Policy Cap</th>
                  <th className="py-2.5 px-3">Claims Count</th>
                  <th className="py-2.5 px-3">Total Claimed</th>
                  <th className="py-2.5 px-3">Compliant</th>
                  <th className="py-2.5 px-3">Flagged Violations</th>
                  <th className="py-2.5 px-3 text-right">Approved</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {categories.map((c: any, idx: number) => {
                  const cap = c.category === 'Meal' ? '$50.00' : (c.category === 'Travel' ? '$250.00' : '$500.00');
                  return (
                    <tr key={idx} className="hover:bg-slate-50/70 transition">
                      <td className="py-2.5 px-3 font-bold text-slate-900 flex items-center gap-2">
                        <span className="w-2 h-2 rounded-full bg-blue-600" />
                        {c.category}
                      </td>
                      <td className="py-2.5 px-3 font-mono text-slate-600">{cap}</td>
                      <td className="py-2.5 px-3 font-semibold text-slate-800">{c.claims}</td>
                      <td className="py-2.5 px-3 font-bold text-slate-900">${Number(c.totalAmount || 0).toFixed(2)}</td>
                      <td className="py-2.5 px-3 font-semibold text-emerald-700">{c.compliant}</td>
                      <td className="py-2.5 px-3 font-bold text-rose-600">{c.flagged}</td>
                      <td className="py-2.5 px-3 text-right font-semibold text-blue-700">{c.approved}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      );
    }

    // 10. EXPENSE_POLICY_VARIANCE
    if (intent === 'EXPENSE_POLICY_VARIANCE') {
      const claims: any[] = data.claims ?? [];
      const totalVar = Number(data.totalVariance || 0);
      const cap = Number(data.policyLimit || 250);

      return (
        <div className="bg-white rounded-2xl border border-amber-200 p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-amber-50 text-amber-600 rounded-xl border border-amber-100">
                <AlertTriangle className="w-5 h-5 text-amber-600" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">Flagged Travel Policy Variance Analysis</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Calculated against POL-FIN-002 Travel Cap of ${cap.toFixed(2)}. Total Excess: +${totalVar.toFixed(2)}
                </p>
              </div>
            </div>
            <button
              onClick={() => onNavigate?.('expenses')}
              className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-xl text-xs font-semibold flex items-center gap-1.5 transition cursor-pointer shadow-xs"
            >
              <span>Open in Expenses</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div className="bg-slate-50 border border-slate-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-slate-400 block">Flagged Travel Claims</span>
              <span className="text-xl font-bold text-slate-900 mt-0.5 block">{data.flaggedCount ?? claims.length}</span>
            </div>
            <div className="bg-blue-50/60 border border-blue-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-blue-600 block">Policy Travel Cap</span>
              <span className="text-xl font-bold text-blue-700 mt-0.5 block">${cap.toFixed(2)}</span>
            </div>
            <div className="bg-rose-50/60 border border-rose-200 rounded-xl p-3">
              <span className="text-[10px] uppercase font-bold text-rose-600 block">Total Travel Excess Variance</span>
              <span className="text-xl font-bold text-rose-700 mt-0.5 block">+${totalVar.toFixed(2)}</span>
            </div>
          </div>

          {claims.length > 0 && (
            <div className="border border-slate-200 rounded-xl overflow-hidden shadow-2xs">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider text-[10px]">
                  <tr>
                    <th className="py-2.5 px-3">Claim #</th>
                    <th className="py-2.5 px-3">Employee</th>
                    <th className="py-2.5 px-3">Claimed</th>
                    <th className="py-2.5 px-3">Travel Cap</th>
                    <th className="py-2.5 px-3 font-bold text-rose-700">Variance (+Excess)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {claims.map((c: any, idx: number) => {
                    const amt = Number(c.claimedAmount || c.amount || 0);
                    const v = Number(c.variance || (amt - cap));
                    return (
                      <tr key={idx} className="hover:bg-slate-50/70 transition">
                        <td className="py-2.5 px-3 font-mono font-bold text-blue-700">{c.claimNumber}</td>
                        <td className="py-2.5 px-3 font-bold text-slate-900">{c.employee || c.employeeName}</td>
                        <td className="py-2.5 px-3 font-bold text-slate-900">${amt.toFixed(2)}</td>
                        <td className="py-2.5 px-3 text-slate-500">${cap.toFixed(2)}</td>
                        <td className="py-2.5 px-3 font-bold text-rose-600">+${v.toFixed(2)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      );
    }

    return null;
  };

  // Choose which result renderer to use based on intent
  const renderResultData = (res: AgentResult) => {
    if (!res.resultData) return null;
    const intent = res.intent ?? '';
    const data = res.resultData as any;

    if (intent === 'GENERAL_CONVERSATION' || data?.isConversational) {
      return renderConversationalResult(data?.message ?? 'Hello! How can I assist you today?');
    }

    // Expense operations
    if (intent.startsWith('EXPENSE_') || intent === 'EXPENSE_COMPLIANCE') {
      const expNode = renderExpenseResult(data, intent, res);
      if (expNode) return expNode;
    }

    // Job Opening Creation
    if (intent === 'JOB_OPENING_CREATE') {
      return renderJobOpeningResult(data);
    }

    // Ticket Create / Update / Read
    if (intent.startsWith('TICKET') || intent === 'TICKET_CREATE' || intent === 'TICKET_UPDATE' || intent === 'TICKET_READ' || intent === 'TICKET_TRIAGE') {
      return renderTicketResult(data, intent);
    }

    // CV Fit Screening
    if (intent === 'CV_SCREEN') {
      return renderCvScreenResult(data);
    }

    // Filtered Employee List (EMPLOYEE_READ)
    if (intent === 'EMPLOYEE_READ' || (data?.employees && Array.isArray(data.employees))) {
      return renderFilteredEmployees(data);
    }

    // SQL / budget analytics
    if (intent === 'BUDGET_ANALYSIS' || intent === 'SQL_AGENT' ||
      intent === 'DASHBOARD_ANALYTICS' || intent === 'APPROVAL_READ' ||
      intent === 'ONBOARDING_READ' || intent === 'AUDIT_READ') {
      if (data?.columns) return renderSqlAnalytics(data as SqlAnalyticsResult);
      return renderGenericResult(data, intent);
    }

    if (intent === 'EXPENSE_COMPLIANCE') {
      return renderComplianceResult(res.resultData as ComplianceResult);
    }

    return renderGenericResult(res.resultData, intent);
  };

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 text-white rounded-2xl p-6 shadow-lg relative overflow-hidden">
        <div className="relative z-10 max-w-2xl space-y-2">
          <div className="flex items-center gap-2 text-blue-400 text-xs font-semibold uppercase tracking-wider">
            <Sparkles className="w-4 h-4" />
            <span>Autonomous Workforce Intelligence</span>
          </div>
          <h2 className="text-2xl font-bold tracking-tight">Nexus AI Assistant</h2>
          <p className="text-xs text-slate-300 leading-relaxed">
            Ask Nexus to analyze workforce data, evaluate HR policy compliance, manage employee records, or execute multi-system automation.
          </p>
        </div>
      </div>

      {/* Input Box */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-md p-4 space-y-4">
        <div className="relative">
          <textarea
            value={prompt}
            onChange={(e) => handlePromptChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Tab') {
                if (showSuggestions && suggestions.length > 0) {
                  e.preventDefault();
                  const selected = suggestions[selectedSuggestionIndex] || suggestions[0];
                  applySuggestion(selected);
                  return;
                }
              } else if (e.key === 'ArrowDown') {
                if (showSuggestions && suggestions.length > 0) {
                  e.preventDefault();
                  setSelectedSuggestionIndex((prev) => (prev + 1) % suggestions.length);
                  return;
                }
              } else if (e.key === 'ArrowUp') {
                if (showSuggestions && suggestions.length > 0) {
                  e.preventDefault();
                  setSelectedSuggestionIndex((prev) => (prev - 1 + suggestions.length) % suggestions.length);
                  return;
                }
              } else if (e.key === 'Escape') {
                setShowSuggestions(false);
                return;
              } else if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                setShowSuggestions(false);
                handleExecute();
              }
            }}
            placeholder="What would you like Nexus to do? e.g. 'alloc', 'onboard Ali', 'show leave policy', 'freeze IT budget'..."
            rows={3}
            className="w-full p-4 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 transition-all resize-none font-medium"
          />

          <CommandSuggestions
            suggestions={showSuggestions ? suggestions : []}
            selectedIndex={selectedSuggestionIndex}
            onSelectSuggestion={applySuggestion}
            onClose={() => setShowSuggestions(false)}
          />
          <button
            onClick={() => handleExecute()}
            disabled={loading || !prompt.trim()}
            className="absolute right-3 bottom-4 px-5 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white font-bold text-xs rounded-lg shadow-sm transition-all flex items-center gap-2"
          >
            {loading ? (
              <>
                <div className="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                <span>Processing...</span>
              </>
            ) : (
              <>
                <span>Execute Request</span>
                <Send className="w-3.5 h-3.5" />
              </>
            )}
          </button>
        </div>

        {/* Quick Action Badges */}
        <div className="space-y-2 pt-1 border-t border-slate-100">
          <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider block">Recommended HR Quick Actions:</span>
          <div className="flex flex-wrap gap-2">
            {quickPrompts.map((q, idx) => {
              const Icon = q.icon;
              return (
                <button
                  key={idx}
                  onClick={() => handleQuickActionClick(q)}
                  className="flex items-center gap-2 px-3 py-1.5 bg-slate-100 hover:bg-blue-50 hover:text-blue-700 hover:border-blue-200 text-slate-700 border border-slate-200 rounded-lg text-xs font-semibold transition-all"
                >
                  <Icon className="w-3.5 h-3.5 text-blue-600" />
                  <span>{q.label}</span>
                </button>
              );
            })}
          </div>
        </div>
      </div>

      {/* LLM Error Banner — shown discreetly, no technical terms */}
      {result?.llmError && (
        <div className="p-3 bg-orange-50 border border-orange-200 rounded-xl text-xs text-orange-800 flex items-start gap-2.5">
          <Wifi className="w-3.5 h-3.5 text-orange-500 shrink-0 mt-0.5" />
          <span>Response generated using offline analysis. Results may be limited.</span>
        </div>
      )}

      {/* Clarification / Error Alert */}
      {errorMsg && (
        <div className="p-4 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 font-semibold flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{errorMsg}</span>
          </div>
          <button onClick={() => setErrorMsg(null)} className="text-rose-600 font-bold hover:underline ml-4 shrink-0">Dismiss</button>
        </div>
      )}

      {/* ── Navigation Suggestion Card (User-Controlled, No Time Limit) ── */}
      {navigationPending && (
        <div className="bg-gradient-to-r from-blue-900 via-indigo-900 to-slate-900 text-white rounded-2xl p-4 shadow-xl border border-blue-500/30 space-y-2 animate-in fade-in duration-300">
          <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="p-2.5 bg-blue-500/20 rounded-xl shrink-0 border border-blue-400/30">
                <Sparkles className="w-5 h-5 text-blue-300" />
              </div>
              <div>
                <div className="text-xs font-semibold text-blue-200 flex items-center gap-2">
                  <span>Quick Navigation</span>
                </div>
                <h4 className="text-sm font-bold text-white mt-0.5">
                  Would you like to open {navigationPending.label}?
                </h4>
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0 self-end sm:self-auto">
              <button
                onClick={() => { setNavigationPending(null); }}
                className="px-3.5 py-1.5 bg-white/10 hover:bg-white/20 text-slate-200 border border-white/20 rounded-xl text-xs font-semibold transition-colors cursor-pointer"
              >
                Stay on Assistant
              </button>
              <button
                onClick={() => {
                  if (onNavigate) onNavigate(navigationPending.tab);
                  setNavigationPending(null);
                }}
                className="px-4 py-1.5 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-xs font-bold transition-all shadow-md flex items-center gap-1.5 cursor-pointer"
              >
                <span>Go to {navigationPending.label}</span>
                <ChevronRight className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Execution Results Section */}
      {result && (
        <div className="space-y-4 animate-in fade-in duration-300">

          {/* ── State Machine Status Header ── */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-xs">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                {result.isSuccess || result.requiresApproval
                  ? <CheckCircle2 className="w-5 h-5 text-emerald-600" />
                  : <AlertTriangle className="w-5 h-5 text-amber-500" />}
                <div>
                  {/* Workflow State Badge */}
                  {result.state && (
                    <span className={`inline-flex items-center gap-1 text-[10px] px-2.5 py-0.5 rounded-full font-bold uppercase tracking-wider mb-1 ${result.state === 'CONFIRMATION_REQUIRED' ? 'bg-amber-100 text-amber-800 border border-amber-300' :
                        result.state === 'CLARIFICATION_REQUIRED' ? 'bg-orange-100 text-orange-800 border border-orange-300' :
                          result.state === 'READY_TO_EXECUTE' ? 'bg-emerald-100 text-emerald-800 border border-emerald-300' :
                            'bg-blue-100 text-blue-800 border border-blue-300'
                      }`}>
                      {result.state === 'CONFIRMATION_REQUIRED' && <ShieldAlert className="w-2.5 h-2.5" />}
                      {result.state === 'CLARIFICATION_REQUIRED' && <AlertCircle className="w-2.5 h-2.5" />}
                      {result.state === 'READY_TO_EXECUTE' && <CheckCircle2 className="w-2.5 h-2.5" />}
                      {result.state === 'ANSWER_DIRECT' && <Bot className="w-2.5 h-2.5" />}
                      {result.state === 'CONFIRMATION_REQUIRED' ? 'Action Review Required' :
                       result.state === 'CLARIFICATION_REQUIRED' ? 'Additional Information Needed' :
                       result.state === 'READY_TO_EXECUTE' ? 'Action Completed' : 'Nexus HR Assistant'}
                    </span>
                  )}
                  <div className="text-xs font-bold text-slate-900">
                    {result.requiresApproval
                      ? 'Awaiting HR Manager Approval'
                      : result.isSuccess
                        ? 'Request Completed Successfully'
                        : 'Information Required'}
                  </div>
                </div>
              </div>
            </div>

            {/* User Message */}
            {result.userMessage && result.state !== 'ANSWER_DIRECT' && !result.intent?.startsWith('EXPENSE_') && (
              <div className="mt-3 pt-3 border-t border-slate-100 text-xs text-slate-700 font-medium leading-relaxed whitespace-pre-line">
                {result.userMessage}
              </div>
            )}
          </div>

          {/* ── ConfirmationDetails Banner (spec output field) ── */}
          {result.confirmationDetails && result.confirmationDetails.requiresUserAction && !result.requiresApproval && (
            <div className="bg-amber-50/80 border border-amber-200 rounded-xl p-4 space-y-2 shadow-xs">
              <div className="flex items-center gap-2 text-amber-800 font-bold text-xs">
                <ShieldAlert className="w-4 h-4 text-amber-600" />
                <span>Proposed Action</span>
              </div>
              <div className="text-xs font-semibold text-slate-800 bg-white rounded-lg border border-amber-100 px-3 py-2">
                {result.confirmationDetails.proposedAction}
              </div>
              <div className="grid grid-cols-1 gap-1">
                {result.confirmationDetails.actionSummary.split('|').map((part, i) => {
                  const [key, val] = part.trim().split(':');
                  return (
                    <div key={i} className="flex items-center justify-between text-[11px] px-2">
                      <span className="text-slate-500 font-semibold">{key?.trim()}</span>
                      <span className="text-slate-800 font-bold">{val?.trim() ?? '—'}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          )}


          {/* Action Plan (approval required) */}
          {result.requiresApproval && result.actionPlan && renderActionPlan(result.actionPlan)}

          {/* Approved plan confirmation */}
          {result.actionPlan && !result.requiresApproval && result.actionPlan.status === 'APPROVED' && (
            <div className="p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-xs text-emerald-800 font-semibold flex items-center gap-2">
              <CheckCircle2 className="w-4 h-4 text-emerald-600" />
              Plan approved and executed successfully.
            </div>
          )}

          {/* Result Data */}
          {result.resultData && renderResultData(result)}

          {/* Context-Aware Recommended Next Actions */}
          {result.choices && result.choices.length > 0 && (
            <div className="bg-gradient-to-br from-slate-50 to-blue-50/50 rounded-2xl border border-blue-100 p-4 shadow-xs space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-blue-600" />
                  <span className="text-xs font-bold text-slate-800 uppercase tracking-wider">Recommended Next Actions:</span>
                </div>
                <span className="text-[10px] text-slate-500 font-medium">Click to execute or view section</span>
              </div>
              <div className="flex flex-wrap gap-2.5">
                {result.choices.map((choice) => (
                  <button
                    key={choice.id}
                    onClick={() => {
                      if (choice.actionType === 'NAVIGATE' && choice.targetTab) {
                        if (onNavigate) onNavigate(choice.targetTab, choice.context);
                      } else if (choice.actionType === 'EXECUTE_PROMPT' && choice.promptToExecute) {
                        setPrompt(choice.promptToExecute);
                        handleExecute(choice.promptToExecute);
                      } else if (choice.actionType === 'OPEN_URL' && choice.url) {
                        window.open(choice.url, '_blank');
                      }
                    }}
                    className="flex items-center gap-2 px-3.5 py-2 bg-white hover:bg-blue-600 hover:text-white text-slate-700 border border-slate-200 hover:border-blue-600 rounded-xl text-xs font-semibold shadow-2xs hover:shadow-md transition-all group cursor-pointer"
                  >
                    {choice.actionType === 'NAVIGATE' && <ArrowUpRight className="w-3.5 h-3.5 text-blue-500 group-hover:text-white" />}
                    {choice.actionType === 'EXECUTE_PROMPT' && <Send className="w-3.5 h-3.5 text-indigo-500 group-hover:text-white" />}
                    {choice.actionType === 'OPEN_URL' && <FileText className="w-3.5 h-3.5 text-emerald-500 group-hover:text-white" />}
                    <span>{choice.label}</span>
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Execution Feed Timeline */}
          {result.executionFeed && result.executionFeed.length > 0 && renderFeed(result.executionFeed)}
        </div>
      )}

      {/* Quick Action Interactive Dialog Modals */}
      {activeModal === 'onboard' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-blue-600 font-bold text-sm">
                <UserPlus className="w-5 h-5" />
                <span>Quick Action: Onboard Employee</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Employee Name</label>
                <input type="text" value={qaName} onChange={(e) => setQaName(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. Ali" />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-bold text-slate-700 block mb-1">Department</label>
                  <input type="text" value={qaDept} onChange={(e) => setQaDept(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. IT" />
                </div>
                <div>
                  <label className="font-bold text-slate-700 block mb-1">Salary ($)</label>
                  <input type="text" value={qaSalary} onChange={(e) => setQaSalary(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. 80000" />
                </div>
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">Designation</label>
                <input type="text" value={qaDesig} onChange={(e) => setQaDesig(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. Junior .NET Developer" />
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = `Onboard employee ${qaName} in ${qaDept} as ${qaDesig} with salary ${qaSalary}`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-bold shadow-xs"
              >
                Launch Onboarding Workflow
              </button>
            </div>
          </div>
        </div>
      )}

      {activeModal === 'policy' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-blue-600 font-bold text-sm">
                <FileCheck className="w-5 h-5" />
                <span>Quick Action: Check Policy</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Policy Category</label>
                <select
                  value={qaPolicyQuery}
                  onChange={(e) => setQaPolicyQuery(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500 bg-white"
                >
                  <option value="leave policy">🏖️ Leave Policy</option>
                  <option value="expense policy">🧾 Expense & Reimbursement Policy</option>
                  <option value="compensation policy">💰 Compensation & Salary Policy</option>
                  <option value="code of conduct policy">📋 Code of Conduct</option>
                  <option value="remote work policy">🏠 Remote Work Policy</option>
                  <option value="overtime policy">⏰ Overtime Policy</option>
                  <option value="recruitment policy">👥 Recruitment Policy</option>
                  <option value="data security policy">🔒 Data Security Policy</option>
                  <option value="performance review policy">⭐ Performance Review Policy</option>
                  <option value="travel policy">✈️ Travel Policy</option>
                </select>
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">Or enter a custom policy keyword</label>
                <input
                  type="text"
                  value={qaPolicyQuery}
                  onChange={(e) => setQaPolicyQuery(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500"
                  placeholder="e.g. maternity leave, POL-HR-001..."
                />
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = `show policy ${qaPolicyQuery}`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-bold shadow-xs"
              >
                Evaluate Policy Compliance
              </button>
            </div>
          </div>
        </div>
      )}

      {activeModal === 'salary' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-emerald-600 font-bold text-sm">
                <TrendingUp className="w-5 h-5" />
                <span>Quick Action: Update Salary</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="p-3 bg-amber-50/70 border border-amber-200 rounded-lg text-xs text-amber-800 font-medium">
              ⚠️ Salary updates are financial mutations — Nexus will show a <strong>CONFIRMATION_REQUIRED</strong> plan before any data is modified.
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Employee Name</label>
                <input type="text" value={qaSalaryTarget} onChange={(e) => setQaSalaryTarget(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. Muhammad, Ali..." />
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">New Monthly Salary ($)</label>
                <input type="text" value={qaNewSalary} onChange={(e) => setQaNewSalary(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. 90000, 150k..." />
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = `Bump up ${qaSalaryTarget}'s monthly compensation to ${qaNewSalary} starting next month`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold shadow-xs flex items-center gap-1.5"
              >
                <TrendingUp className="w-3.5 h-3.5" />
                Draft Salary Update Plan
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Quick Action Dialog: Allocate / Reallocate Budget */}
      {activeModal === 'budget_realloc' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-indigo-600 font-bold text-sm">
                <BarChart3 className="w-5 h-5" />
                <span>Quick Action: Allocate Department Budget</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Target Department</label>
                <select
                  value={qaReallocTgt}
                  onChange={(e) => setQaReallocTgt(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-indigo-500 bg-white"
                >
                  {departmentsList.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">Amount</label>
                <input type="text" value={qaReallocAmount} onChange={(e) => setQaReallocAmount(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-indigo-500" placeholder="e.g. 200k, 100000, 50%..." />
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">Allocation Mode</label>
                <div className="flex rounded-lg border border-slate-200 overflow-hidden">
                  <button
                    onClick={() => setQaAllocMode('ADD')}
                    className={`flex-1 py-2 text-xs font-bold transition-all ${
                      qaAllocMode === 'ADD'
                        ? 'bg-indigo-600 text-white'
                        : 'bg-slate-50 text-slate-600 hover:bg-slate-100'
                    }`}
                  >
                    ➕ Add to Existing
                  </button>
                  <button
                    onClick={() => setQaAllocMode('SET')}
                    className={`flex-1 py-2 text-xs font-bold transition-all border-l border-slate-200 ${
                      qaAllocMode === 'SET'
                        ? 'bg-indigo-600 text-white'
                        : 'bg-slate-50 text-slate-600 hover:bg-slate-100'
                    }`}
                  >
                    🎯 Set as New Total
                  </button>
                </div>
                <p className="text-[10px] text-slate-400 mt-1">
                  {qaAllocMode === 'ADD'
                    ? `Will ADD ${qaReallocAmount} on top of existing budget (e.g. 20k existing + ${qaReallocAmount} = new total)`
                    : `Will SET budget to exactly ${qaReallocAmount} regardless of current balance`}
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const verb = qaAllocMode === 'ADD' ? `Increase ${qaReallocTgt} budget by` : `Set ${qaReallocTgt} budget to`;
                  const cmd = `${verb} ${qaReallocAmount} for Q3.`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-bold shadow-xs flex items-center gap-1.5"
              >
                <BarChart3 className="w-3.5 h-3.5" />
                Allocate Budget
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Quick Action Dialog: Freeze Budgets */}
      {activeModal === 'freeze' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-rose-600 font-bold text-sm">
                <ShieldAlert className="w-5 h-5" />
                <span>Quick Action: Freeze Department Budgets</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="p-3 bg-rose-50 border border-rose-200 rounded-lg text-xs text-rose-800 font-medium">
              🔒 Freezing budget allocations locks spending and requires executive confirmation.
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Target Department</label>
                <select
                  value={qaFreezeDept}
                  onChange={(e) => setQaFreezeDept(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-rose-500 bg-white"
                >
                  <option value="ALL">🔒 ALL Departments</option>
                  {departmentsList.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = qaFreezeDept.toUpperCase() === 'ALL'
                    ? 'Freeze all department budget allocations for Q3.'
                    : `Freeze ${qaFreezeDept} department budget allocation for Q3.`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white rounded-lg text-xs font-bold shadow-xs flex items-center gap-1.5"
              >
                <ShieldAlert className="w-3.5 h-3.5" />
                Lock Budget Allocations
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Quick Action Dialog: Transfer / Promote */}
      {activeModal === 'transfer' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-purple-600 font-bold text-sm">
                <TrendingUp className="w-5 h-5" />
                <span>Quick Action: Employee Transfer / Promote</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Employee Name</label>
                <input type="text" value={qaTransferName} onChange={(e) => setQaTransferName(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-purple-500" placeholder="e.g. Alex" />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-bold text-slate-700 block mb-1">New Department</label>
                  <input type="text" value={qaTransferDept} onChange={(e) => setQaTransferDept(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-purple-500" placeholder="e.g. Product" />
                </div>
                <div>
                  <label className="font-bold text-slate-700 block mb-1">New Role / Title</label>
                  <input type="text" value={qaTransferRole} onChange={(e) => setQaTransferRole(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-purple-500" placeholder="e.g. Senior PM" />
                </div>
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = `Move ${qaTransferName} to ${qaTransferDept} as ${qaTransferRole}.`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-xs font-bold shadow-xs flex items-center gap-1.5"
              >
                <TrendingUp className="w-3.5 h-3.5" />
                Execute Transfer / Promotion
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Quick Action Dialog: Log Sick Day */}
      {activeModal === 'leave' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-amber-600 font-bold text-sm">
                <Zap className="w-5 h-5" />
                <span>Quick Action: Log Sick Day + Slack</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Employee Name</label>
                <input type="text" value={qaLeaveName} onChange={(e) => setQaLeaveName(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-amber-500" placeholder="e.g. Marcus" />
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = `Log ${qaLeaveName}'s sick day today and notify his team on Slack.`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg text-xs font-bold shadow-xs flex items-center gap-1.5"
              >
                <Zap className="w-3.5 h-3.5" />
                Record Sick Day & Notify Slack
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Quick Action Dialog: Hold Payroll */}
      {activeModal === 'payroll' && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50 animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2 text-rose-700 font-bold text-sm">
                <Shield className="w-5 h-5" />
                <span>Quick Action: Hold Payroll</span>
              </div>
              <button onClick={() => setActiveModal(null)} className="text-slate-400 hover:text-slate-600 font-bold text-sm">✕</button>
            </div>
            <div className="space-y-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Department / Division</label>
                <select
                  value={qaPayrollDept}
                  onChange={(e) => setQaPayrollDept(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-rose-500 bg-white"
                >
                  <option value="ALL">⛔ ALL Divisions</option>
                  {departmentsList.map(d => <option key={d} value={d}>{d}</option>)}
                </select>
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100">
              <button onClick={() => setActiveModal(null)} className="px-3.5 py-2 border border-slate-300 rounded-lg text-xs font-bold text-slate-600 hover:bg-slate-50">Cancel</button>
              <button
                onClick={() => {
                  const cmd = qaPayrollDept.toUpperCase() === 'ALL'
                    ? 'Place a payroll hold on all divisions.'
                    : `Place a payroll hold on the ${qaPayrollDept} division.`;
                  setPrompt(cmd);
                  handleExecute(cmd);
                }}
                className="px-4 py-2 bg-rose-700 hover:bg-rose-800 text-white rounded-lg text-xs font-bold shadow-xs flex items-center gap-1.5"
              >
                <Shield className="w-3.5 h-3.5" />
                Place Payroll Hold
              </button>
            </div>
          </div>
        </div>
      )}

      {/* In-App Document Preview Modal */}
      <DocumentPreviewModal
        {...previewModalDoc}
        onClose={() => setPreviewModalDoc(prev => ({ ...prev, isOpen: false }))}
      />
    </div>
  );
};
