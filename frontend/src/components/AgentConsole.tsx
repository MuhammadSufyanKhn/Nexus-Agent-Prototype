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
  DollarSign,
  Trash2,
  CheckCircle2,
  AlertTriangle,
  Clock,
  ShieldAlert,
  Sparkles
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

  const quickPrompts = [
    {
      label: 'Onboard Employee',
      icon: UserPlus,
      prompt: 'Onboard Ahmed Khan as a mid-level .NET developer in IT according to company policy. Apply the correct salary, create his employee record, submit the legacy IT onboarding form, create his Mock SAP record, and generate his welcome email.'
    },
    {
      label: 'Analyze Budget',
      icon: BarChart3,
      prompt: 'Show me departments exceeding their allocated Q3 budget.'
    },
    {
      label: 'Check Policy',
      icon: FileCheck,
      prompt: "Check whether Ahmed's latest expense complies with company policy."
    },
    {
      label: 'Update Salary',
      icon: DollarSign,
      prompt: 'Increase salaries of all IT developers by 10%.'
    },
    {
      label: 'Security Test',
      icon: Trash2,
      prompt: 'Delete all employees.'
    }
  ];

  const handleExecute = async (promptToRun?: string) => {
    const targetPrompt = promptToRun || prompt;
    if (!targetPrompt.trim()) return;

    setLoading(true);
    setErrorMsg(null);
    setResult(null);

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

  const handleApprovalDecision = async (approved: boolean) => {
    if (!result?.actionPlan?.approvalId) return;

    setApprovalProcessing(true);
    try {
      const decisionRes = await decideApproval({
        approvalId: result.actionPlan.approvalId,
        approved,
        approvedBy: `${userRole} User`,
        reason: approved ? 'Action Plan approved by HR Manager' : 'Action Plan rejected during review'
      });

      if (decisionRes) {
        setResult((prev) => {
          if (!prev) return null;
          return {
            ...prev,
            requiresApproval: false,
            actionPlan: prev.actionPlan ? { ...prev.actionPlan, status: approved ? 'APPROVED' : 'REJECTED' } : undefined
          };
        });
      }
      onApprovalStateChange();
    } catch (err: any) {
      setErrorMsg('Failed to record approval decision.');
    } finally {
      setApprovalProcessing(false);
    }
  };

  // Helper render functions
  const renderActionPlan = (plan: ActionPlan) => {
    return (
      <div className="bg-white rounded-xl border border-amber-200 shadow-md p-6 space-y-6 animate-in fade-in duration-200">
        {/* Header */}
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

        {/* Warnings */}
        {plan.warnings && plan.warnings.length > 0 && (
          <div className="bg-amber-50/60 border border-amber-200/80 rounded-lg p-3 text-xs text-amber-800 space-y-1">
            <div className="font-bold flex items-center gap-1.5 text-amber-900">
              <AlertTriangle className="w-3.5 h-3.5" /> Safety & Policy Notes:
            </div>
            {plan.warnings.map((w, idx) => (
              <div key={idx} className="ml-5 list-disc">• {w}</div>
            ))}
          </div>
        )}

        {/* Change Table */}
        {plan.affectedRecords && plan.affectedRecords.length > 0 && (
          <div className="space-y-2">
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500">Proposed Record Changes</h4>
            <div className="border border-slate-200 rounded-lg overflow-hidden">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold">
                  <tr>
                    <th className="py-2.5 px-3">Target Entity</th>
                    <th className="py-2.5 px-3">Primary Label</th>
                    <th className="py-2.5 px-3 text-right">Record ID</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 bg-white">
                  {plan.affectedRecords.map((rec, idx) => (
                    <tr key={idx} className="hover:bg-slate-50/80">
                      <td className="py-2.5 px-3 font-semibold text-slate-800">{rec.entityName}</td>
                      <td className="py-2.5 px-3 font-medium text-emerald-700">{rec.primaryLabel || 'Pending Provision'}</td>
                      <td className="py-2.5 px-3 text-right font-bold text-slate-800">#{rec.recordId}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Action Buttons */}
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
            {approvalProcessing ? 'Executing...' : 'Approve & Execute Plan'}
          </button>
        </div>
      </div>
    );
  };

  const renderSqlAnalytics = (data: SqlAnalyticsResult) => {
    return (
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2 text-slate-900 font-bold text-sm">
            <BarChart3 className="w-4 h-4 text-blue-600" />
            <span>Data Analysis & Budget Overview</span>
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

        {/* Table */}
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
  };

  const renderComplianceResult = (comp: ComplianceResult) => {
    const isCompliant = comp.status === 'COMPLIANT';

    return (
      <div className={`rounded-xl border p-5 shadow-xs space-y-3 ${
        isCompliant ? 'bg-emerald-50/50 border-emerald-200' : 'bg-rose-50/50 border-rose-200'
      }`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            {isCompliant ? (
              <CheckCircle2 className="w-5 h-5 text-emerald-600" />
            ) : (
              <AlertTriangle className="w-5 h-5 text-rose-600" />
            )}
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
          <div>
            <span className="text-slate-400 block text-[10px]">Claimed Amount</span>
            <span className="font-bold text-slate-800">${comp.claimedAmount?.toFixed(2) ?? '0.00'}</span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Allowed Policy Limit</span>
            <span className="font-bold text-slate-800">${comp.allowedAmount?.toFixed(2) ?? '0.00'}</span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Variance</span>
            <span className={`font-bold ${(comp.difference ?? 0) > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
              ${comp.difference?.toFixed(2) ?? '0.00'}
            </span>
          </div>
          <div>
            <span className="text-slate-400 block text-[10px]">Employee</span>
            <span className="font-bold text-slate-800">{comp.employeeName || '—'}</span>
          </div>
        </div>

        {comp.reason && (
          <p className={`text-xs ${isCompliant ? 'text-emerald-800' : 'text-rose-800'} font-medium`}>
            {comp.reason}
          </p>
        )}
      </div>
    );
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
            placeholder="What would you like Nexus to do? (e.g. Onboard Ahmed Khan as a mid-level .NET developer in IT...)"
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
                  onClick={() => {
                    setPrompt(q.prompt);
                    handleExecute(q.prompt);
                  }}
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

      {/* Error Alert */}
      {errorMsg && (
        <div className="p-4 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 font-semibold flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{errorMsg}</span>
          </div>
          <button onClick={() => setErrorMsg(null)} className="text-rose-600 font-bold hover:underline">Dismiss</button>
        </div>
      )}

      {/* Execution Results Section */}
      {result && (
        <div className="space-y-6 animate-in fade-in duration-300">
          {/* Execution Timeline Status */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-xs flex items-center justify-between">
            <div className="flex items-center gap-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-600" />
              <div>
                <div className="text-xs font-bold text-slate-900">Request Processing Completed</div>
                <div className="text-[11px] text-slate-500">Intent: {result.intent || 'HR Operation'}</div>
              </div>
            </div>
            {result.executionTimeMs && (
              <span className="text-[11px] text-slate-400 font-medium flex items-center gap-1">
                <Clock className="w-3.5 h-3.5" /> {result.executionTimeMs}ms
              </span>
            )}
          </div>

          {/* Action Plan Interceptor */}
          {result.requiresApproval && result.actionPlan && renderActionPlan(result.actionPlan)}

          {/* Result Data Payload */}
          {result.resultData && (
            <div>
              {result.intent === 'BUDGET_ANALYSIS' && renderSqlAnalytics(result.resultData)}
              {result.intent === 'EXPENSE_COMPLIANCE' && renderComplianceResult(result.resultData)}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
