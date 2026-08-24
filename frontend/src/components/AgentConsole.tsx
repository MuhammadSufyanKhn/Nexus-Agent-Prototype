import React, { useState } from 'react';
import {
  executeAgentPrompt,
  decideApproval
} from '../services/api';
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
  Clock,
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
  Database,
  Zap
} from 'lucide-react';

interface AgentConsoleProps {
  userRole: string;
  onApprovalStateChange: () => void;
}

export const AgentConsole: React.FC<AgentConsoleProps> = ({
  userRole,
  onApprovalStateChange
}) => {
  const [prompt, setPrompt] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<AgentResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [approvalProcessing, setApprovalProcessing] = useState(false);
  const [feedExpanded, setFeedExpanded] = useState(false);

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

  const quickPrompts = [
    {
      label: 'Onboard Employee',
      icon: UserPlus,
      type: 'onboard',
      prompt: 'Onboard employee Ali in IT as Junior .NET Developer with salary 80000'
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
    },
    {
      label: 'Update Salary',
      icon: TrendingUp,
      type: 'salary',
      prompt: 'Update salary for Ali to 90000'
    },
    {
      label: 'Security Test',
      icon: Shield,
      type: 'security',
      prompt: 'security test'
    }
  ];

  const handleExecute = async (promptToRun?: string) => {
    const targetPrompt = promptToRun || prompt;
    if (!targetPrompt.trim()) return;

    setLoading(true);
    setErrorMsg(null);
    setResult(null);
    setFeedExpanded(false);
    setEditedParams({});
    setActiveModal(null);

    try {
      const res = await executeAgentPrompt(targetPrompt, userRole);
      setResult(res);
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
    if (q.type === 'budget' || q.type === 'security') {
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
        reason: approved ? 'Action Plan approved by Admin' : 'Action Plan rejected during review',
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
        if (onApprovalStateChange) onApprovalStateChange();
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to record approval decision.');
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

  const renderActionPlan = (plan: ActionPlan) => (
    <div className="bg-white rounded-xl border border-amber-200 shadow-md p-6 space-y-6 animate-in fade-in duration-200">
      <div className="flex items-center justify-between border-b border-amber-100 pb-4">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-amber-50 text-amber-600 rounded-lg border border-amber-200">
            <ShieldAlert className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold uppercase tracking-wider text-amber-700 bg-amber-100 px-2 py-0.5 rounded">
                Approval Required
              </span>
              <span className="text-xs font-semibold text-slate-500">• Risk Level: {plan.riskLevel}</span>
            </div>
            <h3 className="text-lg font-bold text-slate-900 mt-1">{plan.title}</h3>
          </div>
        </div>
        <div className="text-right">
          <span className="text-xs text-slate-500 font-medium block">Total Financial Impact</span>
          <span className="text-xl font-extrabold text-slate-900">
            +${plan.totalFinancialImpact?.toLocaleString() ?? '0.00'}
          </span>
        </div>
      </div>

      {/* Editable Workflow Banner */}
      <div className="p-3.5 bg-blue-50/80 border border-blue-200 rounded-xl text-xs text-blue-900 flex items-start gap-2.5">
        <Sparkles className="w-4 h-4 text-blue-600 shrink-0 mt-0.5" />
        <div>
          <span className="font-bold block">Interactive Editable Preview</span>
          You can edit any proposed values below before executing. The backend will revalidate your inputs and apply your exact edits upon approval.
        </div>
      </div>

      {plan.warnings && plan.warnings.length > 0 && (
        <div className="bg-amber-50/60 border border-amber-200/80 rounded-lg p-3 text-xs text-amber-800 space-y-1">
          <div className="font-bold flex items-center gap-1.5 text-amber-900">
            <AlertTriangle className="w-3.5 h-3.5" /> Safety &amp; Policy Notes:
          </div>
          {plan.warnings.map((w, idx) => (
            <div key={idx} className="ml-5">• {w}</div>
          ))}
        </div>
      )}

      {/* Editable Parameters Grid */}
      {plan.affectedRecords && plan.affectedRecords.length > 0 && (
        <div className="space-y-3">
          <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500 flex items-center justify-between">
            <span>Proposed Record Changes &amp; Input Fields</span>
            <span className="text-[10px] text-slate-400 font-medium font-normal">Click any field value to edit</span>
          </h4>
          <div className="space-y-3">
            {plan.affectedRecords.map((rec, rIdx) => (
              <div key={rIdx} className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-3">
                <div className="flex items-center justify-between border-b border-slate-200 pb-2">
                  <span className="font-bold text-xs text-slate-800">{rec.entityName} Master</span>
                  <span className="text-xs font-semibold text-emerald-700">{rec.primaryLabel || 'New Record'}</span>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  {rec.changes?.map((c, cIdx) => {
                    const isEdited = editedParams[c.fieldName] !== undefined;
                    const currentValue = editedParams[c.fieldName] ?? c.newValue;
                    const sourceTag = isEdited
                      ? { label: 'User-edited', class: 'bg-purple-100 text-purple-700 border-purple-200' }
                      : c.valueSource === 'Policy-derived'
                        ? { label: 'Policy-derived', class: 'bg-emerald-100 text-emerald-700 border-emerald-200' }
                        : { label: 'AI-proposed', class: 'bg-blue-100 text-blue-700 border-blue-200' };

                    return (
                      <div key={cIdx} className="bg-white p-3 rounded-lg border border-slate-200 space-y-1.5 shadow-2xs">
                        <div className="flex items-center justify-between">
                          <label className="text-[11px] font-bold text-slate-600 uppercase tracking-wide">
                            {c.fieldName.replace(/([A-Z])/g, ' $1').trim()}
                          </label>
                          <span className={`text-[10px] px-1.5 py-0.5 rounded font-bold border ${sourceTag.class}`}>
                            {sourceTag.label}
                          </span>
                        </div>
                        <input
                          type="text"
                          value={currentValue}
                          onChange={(e) => setEditedParams({ ...editedParams, [c.fieldName]: e.target.value })}
                          className="w-full text-xs font-semibold text-slate-900 bg-slate-50 hover:bg-white focus:bg-white border border-slate-300 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 rounded-lg px-2.5 py-1.5 transition-all outline-hidden"
                          placeholder={`Enter ${c.fieldName}...`}
                        />
                        <div className="text-[10px] text-slate-400 flex items-center justify-between">
                          <span>Original AI: {c.newValue}</span>
                          {c.oldValue && <span>Previous: {c.oldValue}</span>}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Proposed Tool Execution Steps */}
      {plan.steps && plan.steps.length > 0 && (
        <div className="space-y-2">
          <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500">Execution Plan Sequence</h4>
          <div className="space-y-1.5">
            {plan.steps.map((s, sIdx) => (
              <div key={sIdx} className="flex items-center justify-between p-2.5 bg-slate-50 rounded-lg border border-slate-100 text-xs">
                <span className="font-semibold text-slate-700">Step {s.stepNumber}: {s.description}</span>
                <span className="font-mono text-[10px] text-slate-400 bg-white px-2 py-0.5 rounded border border-slate-200">{s.toolName}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="flex items-center justify-end gap-3 pt-2">
        <button
          onClick={() => handleApprovalDecision(false)}
          disabled={approvalProcessing}
          className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 rounded-lg text-xs font-bold transition-colors"
        >
          Reject Changes
        </button>
        <button
          onClick={() => handleApprovalDecision(true)}
          disabled={approvalProcessing}
          className="px-5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition-colors shadow-sm flex items-center gap-1.5"
        >
          {approvalProcessing ? 'Executing...' : Object.keys(editedParams).length > 0 ? 'Revalidate & Execute Edited Plan' : 'Approve & Execute Plan'}
        </button>
      </div>
    </div>
  );

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
      <div className={`rounded-xl border p-5 shadow-xs space-y-3 ${
        isCompliant ? 'bg-emerald-50/50 border-emerald-200' : 'bg-rose-50/50 border-rose-200'
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

    // Build field list from result object
    const fields = typeof data === 'object' && data !== null
      ? Object.entries(data)
          .filter(([k]) => !['message', 'result', 'summary'].includes(k))
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
            <div className="text-sm font-semibold text-slate-900">{message}</div>
          </div>
          <CheckCircle2 className="w-5 h-5 text-emerald-500 ml-auto shrink-0" />
        </div>

        {/* Array results (e.g. policy list, employee list) */}
        {(data?.policies || data?.employees || data?.departments || data?.budgets) && (
          <div className="space-y-2">
            {(data.policies ?? data.employees ?? data.departments ?? data.budgets ?? []).slice(0, 10).map((item: any, idx: number) => (
              <div key={idx} className="flex items-center justify-between p-2.5 bg-slate-50 rounded-lg border border-slate-100 text-xs">
                <span className="font-semibold text-slate-800">{item.title ?? item.name ?? item.code ?? item.departmentName ?? `Record #${item.id}`}</span>
                <span className="text-slate-500">{item.category ?? item.designation ?? item.description ?? item.quarter ?? ''}</span>
                <span className={`font-bold ${item.isActive === false ? 'text-rose-500' : 'text-emerald-600'}`}>
                  {item.isActive === false ? 'Inactive' : item.status ?? item.salary ? `$${Number(item.salary ?? 0).toLocaleString()}` : ''}
                </span>
              </div>
            ))}
          </div>
        )}

        {/* Key-value fields for single record operations */}
        {fields.length > 0 && !data?.policies && !data?.employees && !data?.departments && (
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
          <span>Execution Timeline ({feed.length} steps)</span>
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

  // Choose which result renderer to use based on intent
  const renderResultData = (res: AgentResult) => {
    if (!res.resultData) return null;
    const intent = res.intent ?? '';
    const data = res.resultData as any;

    if (intent === 'GENERAL_CONVERSATION' || data?.isConversational) {
      return renderConversationalResult(data?.message ?? 'Hello! How can I assist you today?');
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
            onChange={(e) => setPrompt(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                handleExecute();
              }
            }}
            placeholder="What would you like Nexus to do? e.g. 'Ali ka onboarding karo salary 500', 'show leave policy', 'increase IT budget by 50k'..."
            rows={3}
            className="w-full p-4 bg-slate-50 border border-slate-200 rounded-xl text-sm text-slate-900 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 transition-all resize-none font-medium"
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

      {/* LLM Error Banner */}
      {result?.llmError && (
        <div className="p-4 bg-orange-50 border border-orange-200 rounded-xl text-xs text-orange-900 font-medium flex items-start gap-3">
          <Wifi className="w-4 h-4 text-orange-500 shrink-0 mt-0.5" />
          <div>
            <div className="font-bold text-orange-800 mb-1">Gemini API Error — Using Rule-Based Fallback</div>
            <div>{result.llmError}</div>
          </div>
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
                    <span className={`inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-full font-bold uppercase tracking-wider mb-1 ${
                      result.state === 'CONFIRMATION_REQUIRED' ? 'bg-amber-100 text-amber-800 border border-amber-300' :
                      result.state === 'CLARIFICATION_REQUIRED' ? 'bg-orange-100 text-orange-800 border border-orange-300' :
                      result.state === 'READY_TO_EXECUTE' ? 'bg-emerald-100 text-emerald-800 border border-emerald-300' :
                      'bg-blue-100 text-blue-800 border border-blue-300'
                    }`}>
                      {result.state === 'CONFIRMATION_REQUIRED' && <ShieldAlert className="w-2.5 h-2.5" />}
                      {result.state === 'CLARIFICATION_REQUIRED' && <AlertCircle className="w-2.5 h-2.5" />}
                      {result.state === 'READY_TO_EXECUTE' && <CheckCircle2 className="w-2.5 h-2.5" />}
                      {result.state === 'ANSWER_DIRECT' && <Bot className="w-2.5 h-2.5" />}
                      {result.state?.replace(/_/g, ' ')}
                    </span>
                  )}
                  <div className="text-xs font-bold text-slate-900">
                    {result.requiresApproval
                      ? 'Awaiting Your Approval'
                      : result.isSuccess
                        ? 'Request Completed Successfully'
                        : 'Clarification Needed'}
                  </div>
                  <div className="text-[11px] text-slate-500 flex items-center gap-2 flex-wrap mt-0.5">
                    <span>Intent: <span className="font-semibold text-slate-700">{intentLabel(result.intent)}</span></span>
                    {result.targetSystem && result.targetSystem !== 'UNKNOWN' && (
                      <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-bold ${
                        result.targetSystem === 'SQL_SERVER' ? 'bg-slate-100 text-slate-600 border border-slate-200' :
                        result.targetSystem === 'N8N_WORKFLOW' ? 'bg-purple-100 text-purple-700 border border-purple-200' :
                        'bg-pink-100 text-pink-700 border border-pink-200'
                      }`}>
                        <Database className="w-2.5 h-2.5" />
                        {result.targetSystem === 'SQL_SERVER' ? 'SQL Server' :
                         result.targetSystem === 'N8N_WORKFLOW' ? 'n8n Workflow' :
                         result.targetSystem === 'ZAPIER' ? 'Zapier' : result.targetSystem}
                      </span>
                    )}
                    {result.llmError && <span className="text-orange-600">(rule-based)</span>}
                  </div>
                </div>
              </div>
              {result.executionTimeMs && (
                <span className="text-[11px] text-slate-400 font-medium flex items-center gap-1 shrink-0">
                  <Clock className="w-3.5 h-3.5" /> {result.executionTimeMs}ms
                </span>
              )}
            </div>

            {/* User Message from spec */}
            {result.userMessage && result.state !== 'ANSWER_DIRECT' && (
              <div className="mt-3 pt-3 border-t border-slate-100 text-xs text-slate-700 font-medium leading-relaxed">
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

          {/* ── ExecutionPayload Panel (spec output field: READY_TO_EXECUTE) ── */}
          {result.executionPayload && result.state === 'READY_TO_EXECUTE' && (
            <div className="bg-emerald-50/60 border border-emerald-200 rounded-xl p-4 shadow-xs space-y-2">
              <div className="flex items-center gap-2 text-emerald-800 font-bold text-xs">
                <Zap className="w-4 h-4 text-emerald-600" />
                <span>Execution Payload</span>
                <span className="ml-auto text-[10px] bg-emerald-100 text-emerald-700 px-2 py-0.5 rounded-full border border-emerald-200 font-semibold">
                  {(result.executionPayload as any)?.targetSystem ?? 'SQL_SERVER'}
                </span>
              </div>
              <pre className="text-[10px] text-slate-700 bg-white rounded-lg border border-emerald-100 p-2.5 overflow-x-auto font-mono leading-relaxed">
                {JSON.stringify(result.executionPayload, null, 2)}
              </pre>
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
                <label className="font-bold text-slate-700 block mb-1">Policy Title or Code</label>
                <input type="text" value={qaPolicyQuery} onChange={(e) => setQaPolicyQuery(e.target.value)} className="w-full p-2.5 border border-slate-300 rounded-lg text-xs font-semibold focus:border-blue-500" placeholder="e.g. leave policy, POL-HR-001..." />
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

    </div>
  );
};
