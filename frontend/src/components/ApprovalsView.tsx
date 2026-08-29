import React, { useEffect, useState } from 'react';
import { fetchPendingApprovals, decideApproval } from '../services/api';
import type { PendingApproval } from '../services/api';
import { 
  ShieldAlert, 
  CheckCircle2, 
  XCircle, 
  Clock, 
  AlertTriangle, 
  RefreshCw, 
  Search, 
  DollarSign, 
  Layers, 
  UserCheck, 
  ShieldCheck, 
  Lock, 
  ArrowRight,
  TrendingUp,
  FileSpreadsheet,
  AlertCircle,
  Database,
  ArrowUpRight,
  Check
} from 'lucide-react';

interface ApprovalsViewProps {
  userRole: string;
  onApprovalChanged: () => void;
}

interface ParsedPlan {
  title: string;
  totalFinancialImpact: number;
  steps: Array<{ stepNumber: number; toolName: string; description: string; riskLevel?: number }>;
  affectedRecords: Array<{
    recordId: number;
    entityName: string;
    primaryLabel: string;
    changes: Array<{ fieldName: string; oldValue: string; newValue: string; difference?: string }>;
  }>;
  warnings: string[];
}

function parseApprovalReason(rawReason: string): ParsedPlan | null {
  if (!rawReason) return null;
  const trimmed = rawReason.trim();
  if (trimmed.startsWith('{') && trimmed.endsWith('}')) {
    try {
      const parsed = JSON.parse(trimmed);
      return {
        title: parsed.Title || parsed.title || 'Workforce Action Plan',
        totalFinancialImpact: parsed.TotalFinancialImpact ?? parsed.totalFinancialImpact ?? 0,
        steps: (parsed.Steps || parsed.steps || []).map((s: any) => ({
          stepNumber: s.StepNumber ?? s.stepNumber ?? 1,
          toolName: s.ToolName || s.toolName || 'system.execution',
          description: s.Description || s.description || 'System execution step',
          riskLevel: s.RiskLevel ?? s.riskLevel
        })),
        affectedRecords: (parsed.AffectedRecords || parsed.affectedRecords || []).map((r: any) => ({
          recordId: r.RecordId ?? r.recordId ?? 0,
          entityName: r.EntityName || r.entityName || 'System Master',
          primaryLabel: r.PrimaryLabel || r.primaryLabel || 'Target Entity',
          changes: (r.Changes || r.changes || []).map((c: any) => ({
            fieldName: c.FieldName || c.fieldName || 'Property',
            oldValue: c.OldValue || c.oldValue || '[PREVIOUS]',
            newValue: c.NewValue || c.newValue || '[NEW]',
            difference: c.Difference || c.difference || ''
          }))
        })),
        warnings: parsed.Warnings || parsed.warnings || []
      };
    } catch {
      return null;
    }
  }
  return null;
}

export const ApprovalsView: React.FC<ApprovalsViewProps> = ({
  userRole,
  onApprovalChanged
}) => {
  const [approvals, setApprovals] = useState<PendingApproval[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'PENDING' | 'APPROVED' | 'REJECTED'>('PENDING');
  const [searchQuery, setSearchQuery] = useState('');
  const [filterRisk, setFilterRisk] = useState<string>('ALL');
  const [processingId, setProcessingId] = useState<string | null>(null);

  const loadApprovals = async () => {
    setLoading(true);
    try {
      const data = await fetchPendingApprovals();
      setApprovals(data);
    } catch (err) {
      console.error('Failed to load pending approvals:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadApprovals();
  }, []);

  const handleDecision = async (approvalId: string, approved: boolean) => {
    setProcessingId(approvalId);
    try {
      await decideApproval({
        approvalId,
        approved,
        approvedBy: `${userRole} Administrator`,
        reason: approved ? 'Authorized by Executive HR Administrator' : 'Rejected by Executive HR Administrator'
      });
      await loadApprovals();
      onApprovalChanged();
    } catch (err) {
      console.error('Failed to process approval decision:', err);
    } finally {
      setProcessingId(null);
    }
  };

  const filteredApprovals = approvals.filter(app => {
    const matchesSearch = (app.reason || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
                          (app.requestedBy || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
                          (app.id || '').toLowerCase().includes(searchQuery.toLowerCase());
    const matchesRisk = filterRisk === 'ALL' || app.riskLevel.toString().toUpperCase() === filterRisk.toUpperCase();
    return matchesSearch && matchesRisk;
  });

  const getRiskBadge = (riskLevel: string | number) => {
    const levelStr = riskLevel.toString().toUpperCase();
    if (levelStr === 'CRITICAL' || levelStr === '3' || levelStr === 'HIGH') {
      return (
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold bg-rose-500/10 text-rose-600 border border-rose-500/20 shadow-xs">
          <span className="w-2 h-2 rounded-full bg-rose-500 animate-pulse" />
          <AlertTriangle className="w-3.5 h-3.5" /> High Risk Gate
        </span>
      );
    }
    if (levelStr === 'MEDIUM' || levelStr === '2') {
      return (
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold bg-amber-500/10 text-amber-700 border border-amber-500/30">
          <Clock className="w-3.5 h-3.5 text-amber-600" /> Medium Risk
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-extrabold bg-emerald-500/10 text-emerald-700 border border-emerald-500/30">
        <ShieldCheck className="w-3.5 h-3.5 text-emerald-600" /> Standard Operation
      </span>
    );
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 py-6 space-y-6">
      {/* Hero Banner Header */}
      <div className="relative overflow-hidden bg-slate-900 rounded-2xl p-6 sm:p-8 text-white shadow-xl border border-slate-800">
        <div className="absolute -right-10 -bottom-10 w-72 h-72 bg-purple-600/20 rounded-full blur-3xl pointer-events-none" />
        <div className="absolute right-1/3 -top-10 w-60 h-60 bg-blue-600/20 rounded-full blur-3xl pointer-events-none" />

        <div className="relative z-10 flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
          <div className="space-y-2">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-purple-500/20 border border-purple-500/30 text-purple-300 text-xs font-semibold backdrop-blur-md">
              <ShieldAlert className="w-3.5 h-3.5 text-purple-400" />
              <span>Human-in-the-Loop Pre-Execution Gate</span>
            </div>
            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight bg-gradient-to-r from-white via-slate-100 to-slate-300 bg-clip-text text-transparent">
              Enterprise Approval Center
            </h1>
            <p className="text-sm text-slate-400 max-w-xl">
              Review pre-execution Action Plans, authorize high-risk workforce mutations, and inspect financial impact previews before database commit.
            </p>
          </div>

          {/* Quick Metrics Cards */}
          <div className="grid grid-cols-3 gap-3 w-full md:w-auto">
            <div className="bg-slate-800/80 backdrop-blur-md p-3.5 rounded-xl border border-slate-700/60 text-center min-w-[100px]">
              <p className="text-2xl font-black text-amber-400">{approvals.length}</p>
              <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mt-0.5">Pending</p>
            </div>
            <div className="bg-slate-800/80 backdrop-blur-md p-3.5 rounded-xl border border-slate-700/60 text-center min-w-[100px]">
              <p className="text-2xl font-black text-emerald-400">100%</p>
              <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mt-0.5">RBAC Lock</p>
            </div>
            <div className="bg-slate-800/80 backdrop-blur-md p-3.5 rounded-xl border border-slate-700/60 text-center min-w-[100px]">
              <p className="text-2xl font-black text-purple-400">SHA-256</p>
              <p className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mt-0.5">Audit Sealed</p>
            </div>
          </div>
        </div>
      </div>

      {/* Control Bar: Filter, Search & Refresh */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4 bg-white p-4 rounded-xl border border-slate-200 shadow-xs">
        {/* Navigation Tabs */}
        <div className="flex bg-slate-100 p-1 rounded-xl text-xs font-bold text-slate-600">
          <button
            onClick={() => setActiveTab('PENDING')}
            className={`px-4 py-2 rounded-lg transition-all flex items-center gap-2 ${
              activeTab === 'PENDING' ? 'bg-white text-slate-900 shadow-sm font-extrabold' : 'hover:text-slate-900'
            }`}
          >
            <Clock className="w-3.5 h-3.5 text-amber-500" />
            <span>Pending Approvals</span>
            <span className="ml-1 px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 text-[10px] font-extrabold">
              {approvals.length}
            </span>
          </button>
        </div>

        {/* Search & Refresh */}
        <div className="flex items-center gap-3">
          <div className="relative flex-1 sm:w-64">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search plan or requester..."
              className="w-full pl-9 pr-4 py-2 bg-slate-50 border border-slate-200 rounded-lg text-xs font-semibold focus:outline-none focus:border-purple-500 focus:bg-white transition-all"
            />
          </div>

          <button
            onClick={loadApprovals}
            disabled={loading}
            className="p-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl text-xs font-bold transition-all flex items-center gap-1.5 disabled:opacity-50"
            title="Refresh Approvals"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin text-purple-600' : ''}`} />
          </button>
        </div>
      </div>

      {/* Content Area */}
      {loading ? (
        <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center space-y-3 shadow-xs">
          <RefreshCw className="w-8 h-8 text-purple-600 animate-spin mx-auto" />
          <h4 className="font-bold text-slate-800 text-sm">Fetching Action Plans...</h4>
          <p className="text-xs text-slate-500">Querying SQL Server pending approvals queue & RBAC authorization engine</p>
        </div>
      ) : filteredApprovals.length === 0 ? (
        <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center space-y-4 shadow-xs">
          <div className="w-16 h-16 bg-emerald-50 text-emerald-500 rounded-full flex items-center justify-center mx-auto shadow-inner">
            <CheckCircle2 className="w-10 h-10" />
          </div>
          <div className="space-y-1">
            <h4 className="font-bold text-slate-900 text-base">All Approvals Clear!</h4>
            <p className="text-xs text-slate-500 max-w-md mx-auto">
              There are no pending high-risk workforce execution plans awaiting executive authorization. All operations are up-to-date.
            </p>
          </div>
          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-100 text-slate-600 text-xs font-semibold">
            <Lock className="w-3.5 h-3.5 text-emerald-600" />
            <span>Zero pending execution bottlenecks</span>
          </div>
        </div>
      ) : (
        <div className="space-y-6">
          {filteredApprovals.map((app) => {
            const parsedPlan = parseApprovalReason(app.reason);
            const planTitle = parsedPlan?.title || (typeof app.reason === 'string' && !app.reason.startsWith('{') ? app.reason : `Workforce Action Plan #${app.id.substring(0, 8)}`);
            const finImpact = parsedPlan?.totalFinancialImpact ?? app.totalFinancialImpact ?? 0;
            const steps = parsedPlan?.steps || [];
            const records = parsedPlan?.affectedRecords || [];
            const warnings = parsedPlan?.warnings || [];

            return (
              <div
                key={app.id}
                className="bg-white rounded-2xl border border-slate-200/90 shadow-md hover:shadow-lg transition-all overflow-hidden"
              >
                {/* Card Header Bar */}
                <div className="bg-slate-50/90 border-b border-slate-200/80 px-6 py-4 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
                  <div className="flex items-center gap-3 flex-wrap">
                    {getRiskBadge(app.riskLevel)}
                    <span className="text-xs font-bold text-slate-700 flex items-center gap-1.5 bg-white px-3 py-1 rounded-lg border border-slate-200">
                      <UserCheck className="w-3.5 h-3.5 text-purple-600" />
                      Requester: <span className="text-slate-900 font-extrabold">{app.requestedBy}</span>
                    </span>
                  </div>
                  <div className="flex items-center gap-3 text-xs text-slate-500 font-medium">
                    <span className="font-mono bg-slate-200/70 text-slate-800 px-2.5 py-0.5 rounded-md text-[11px] font-bold">
                      ID: #{app.id.substring(0, 8)}
                    </span>
                    <span>{new Date(app.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                  </div>
                </div>

                {/* Card Main Body */}
                <div className="p-6 space-y-6">
                  {/* Title & Financial Impact Banner */}
                  <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
                    <div className="space-y-1 max-w-2xl">
                      <h3 className="text-lg font-extrabold text-slate-900 flex items-start gap-2.5 leading-snug">
                        <Layers className="w-5 h-5 text-purple-600 shrink-0 mt-0.5" />
                        <span>{planTitle}</span>
                      </h3>
                      <p className="text-xs text-slate-500">
                        Pre-execution preview generated by Nexus AI Agent. No database mutations have occurred yet.
                      </p>
                    </div>

                    {/* Financial Impact Callout Card */}
                    <div className="bg-purple-50 border border-purple-200 rounded-xl px-5 py-3 flex items-center gap-3.5 shrink-0 shadow-2xs">
                      <div className="p-2.5 bg-purple-600 text-white rounded-xl shadow-xs">
                        <DollarSign className="w-5 h-5" />
                      </div>
                      <div>
                        <p className="text-[10px] font-extrabold text-purple-700 uppercase tracking-wider">Financial Impact</p>
                        <p className="text-lg font-black text-purple-950">
                          ${finImpact ? finImpact.toLocaleString(undefined, { minimumFractionDigits: 2 }) : '0.00'}
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* Warnings Callout Box */}
                  {warnings.length > 0 && (
                    <div className="bg-amber-50/90 border border-amber-200/80 rounded-xl p-4 space-y-2">
                      <h5 className="text-xs font-extrabold uppercase tracking-wider text-amber-800 flex items-center gap-1.5">
                        <AlertCircle className="w-4 h-4 text-amber-600" /> Policy & Impact Notes
                      </h5>
                      <ul className="space-y-1 text-xs text-amber-900 font-semibold pl-6 list-disc">
                        {warnings.map((w, idx) => (
                          <li key={idx}>{w}</li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {/* Proposed Record Changes Table */}
                  {records.length > 0 && (
                    <div className="space-y-2.5">
                      <h5 className="text-xs font-extrabold uppercase tracking-wider text-slate-500 flex items-center gap-1.5">
                        <Database className="w-3.5 h-3.5 text-slate-400" /> Proposed Record Modifications ({records.length})
                      </h5>
                      <div className="border border-slate-200 rounded-xl overflow-hidden shadow-2xs">
                        <table className="w-full text-left text-xs">
                          <thead className="bg-slate-50 border-b border-slate-200 text-slate-500 font-bold uppercase text-[10px] tracking-wider">
                            <tr>
                              <th className="px-4 py-2.5">Entity / Record</th>
                              <th className="px-4 py-2.5">Field</th>
                              <th className="px-4 py-2.5">Previous Value</th>
                              <th className="px-4 py-2.5">Proposed New Value</th>
                              <th className="px-4 py-2.5">Diff / Impact</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-slate-100 font-medium text-slate-700 bg-white">
                            {records.flatMap((rec, rIdx) => 
                              rec.changes.map((c, cIdx) => (
                                <tr key={`${rIdx}-${cIdx}`} className="hover:bg-slate-50/80 transition-colors">
                                  <td className="px-4 py-3 font-bold text-slate-900">
                                    <div className="flex flex-col">
                                      <span>{rec.primaryLabel}</span>
                                      <span className="text-[10px] font-normal text-slate-400">{rec.entityName}</span>
                                    </div>
                                  </td>
                                  <td className="px-4 py-3 font-mono text-purple-700 text-[11px]">{c.fieldName}</td>
                                  <td className="px-4 py-3 text-slate-500 line-through bg-slate-50/50 rounded">{c.oldValue}</td>
                                  <td className="px-4 py-3 font-bold text-emerald-700 bg-emerald-50/50 rounded">{c.newValue}</td>
                                  <td className="px-4 py-3 font-semibold text-purple-900">{c.difference || 'Value Update'}</td>
                                </tr>
                              ))
                            )}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}

                  {/* Workflow Execution Steps */}
                  <div className="bg-slate-50/80 rounded-xl p-4 border border-slate-200/80 space-y-3">
                    <h5 className="text-xs font-extrabold uppercase tracking-wider text-slate-500 flex items-center gap-1.5">
                      <FileSpreadsheet className="w-3.5 h-3.5 text-slate-400" /> Execution Sequence Steps ({steps.length > 0 ? steps.length : 3})
                    </h5>
                    
                    {steps.length > 0 ? (
                      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
                        {steps.map((step, idx) => (
                          <div key={idx} className="bg-white p-3.5 rounded-xl border border-slate-200 text-xs flex items-start gap-3 shadow-2xs hover:border-purple-300 transition-all">
                            <span className="w-6 h-6 rounded-full bg-purple-100 text-purple-700 font-extrabold text-xs flex items-center justify-center shrink-0 mt-0.5">
                              {step.stepNumber}
                            </span>
                            <div className="space-y-0.5">
                              <span className="font-mono text-[10px] font-bold uppercase text-purple-600 bg-purple-50 px-1.5 py-0.5 rounded border border-purple-100">
                                {step.toolName}
                              </span>
                              <p className="font-bold text-slate-900 mt-1">{step.description}</p>
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                        <div className="bg-white p-3 rounded-xl border border-slate-200 text-xs flex items-start gap-2.5 shadow-2xs">
                          <span className="w-5 h-5 rounded-full bg-purple-100 text-purple-700 font-bold text-[11px] flex items-center justify-center shrink-0">1</span>
                          <div>
                            <p className="font-bold text-slate-800">Policy Evaluation</p>
                            <p className="text-[11px] text-slate-500">Verify against HR Policy POL-HR-001</p>
                          </div>
                        </div>
                        <div className="bg-white p-3 rounded-xl border border-slate-200 text-xs flex items-start gap-2.5 shadow-2xs">
                          <span className="w-5 h-5 rounded-full bg-purple-100 text-purple-700 font-bold text-[11px] flex items-center justify-center shrink-0">2</span>
                          <div>
                            <p className="font-bold text-slate-800">Database Transaction</p>
                            <p className="text-[11px] text-slate-500">Commit SQL record update</p>
                          </div>
                        </div>
                        <div className="bg-white p-3 rounded-xl border border-slate-200 text-xs flex items-start gap-2.5 shadow-2xs">
                          <span className="w-5 h-5 rounded-full bg-purple-100 text-purple-700 font-bold text-[11px] flex items-center justify-center shrink-0">3</span>
                          <div>
                            <p className="font-bold text-slate-800">Cryptographic Seal</p>
                            <p className="text-[11px] text-slate-500">Append SHA-256 audit block</p>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Footer Action Buttons */}
                  <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
                    <button
                      onClick={() => handleDecision(app.id, false)}
                      disabled={processingId === app.id}
                      className="px-5 py-2.5 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 rounded-xl text-xs font-bold transition-all flex items-center gap-2 hover:border-slate-400 disabled:opacity-50"
                    >
                      <XCircle className="w-4 h-4 text-slate-400" />
                      <span>Reject Action</span>
                    </button>

                    <button
                      onClick={() => handleDecision(app.id, true)}
                      disabled={processingId === app.id}
                      className="px-6 py-2.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-xl text-xs font-extrabold shadow-md hover:shadow-lg transition-all flex items-center gap-2 disabled:opacity-50 cursor-pointer"
                    >
                      {processingId === app.id ? (
                        <RefreshCw className="w-4 h-4 animate-spin" />
                      ) : (
                        <CheckCircle2 className="w-4 h-4 text-emerald-200" />
                      )}
                      <span>Authorize & Execute Plan</span>
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
