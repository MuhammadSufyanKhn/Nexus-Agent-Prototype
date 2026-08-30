import React, { useEffect, useState } from 'react';
import { fetchPendingApprovals, decideApproval } from '../services/api';
import type { PendingApproval } from '../services/api';
import { 
  ShieldCheck, 
  CheckCircle2, 
  Clock, 
  AlertTriangle, 
  RefreshCw, 
  Search
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
        title: parsed.Title || parsed.title || 'Workforce Action Request',
        totalFinancialImpact: parsed.TotalFinancialImpact ?? parsed.totalFinancialImpact ?? 0,
        steps: (parsed.Steps || parsed.steps || []).map((s: any) => ({
          stepNumber: s.StepNumber ?? s.stepNumber ?? 1,
          toolName: s.ToolName || s.toolName || 'HR System',
          description: s.Description || s.description || 'Process workforce request',
          riskLevel: s.RiskLevel ?? s.riskLevel
        })),
        affectedRecords: (parsed.AffectedRecords || parsed.affectedRecords || []).map((r: any) => ({
          recordId: r.RecordId ?? r.recordId ?? 0,
          entityName: r.EntityName || r.entityName || 'Employee Profile',
          primaryLabel: r.PrimaryLabel || r.primaryLabel || 'Target Employee',
          changes: (r.Changes || r.changes || []).map((c: any) => ({
            fieldName: c.FieldName || c.fieldName || 'Property',
            oldValue: c.OldValue || c.oldValue || 'Previous Value',
            newValue: c.NewValue || c.newValue || 'New Value',
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
  const [searchQuery, setSearchQuery] = useState('');
  const [filterRisk, setFilterRisk] = useState<string>('ALL');
  const [processingId, setProcessingId] = useState<string | null>(null);
  const [completedSuccess, setCompletedSuccess] = useState<string | null>(null);

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
        reason: approved ? 'Approved by Executive HR Administrator' : 'Declined by Executive HR Administrator'
      });
      setCompletedSuccess(approved ? 'Action Successfully Completed & Applied to Employee Records.' : 'Request Declined.');
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
        <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold bg-amber-500/10 text-amber-700 border border-amber-500/30">
          <AlertTriangle className="w-3.5 h-3.5 text-amber-600" /> HR Manager Review Required
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold bg-blue-500/10 text-blue-700 border border-blue-500/30">
        <ShieldCheck className="w-3.5 h-3.5 text-blue-600" /> Standard HR Action
      </span>
    );
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 py-6 space-y-6">
      {/* Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 p-6 rounded-2xl border border-indigo-900/40 text-white shadow-xl">
        <div className="flex items-center gap-4">
          <div className="p-3 bg-indigo-600/30 border border-indigo-500/30 rounded-xl text-indigo-300 backdrop-blur-md">
            <CheckCircle2 className="w-7 h-7" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold tracking-tight">HR Action Approval Center</h2>
              <span className="text-[10px] bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-mono font-bold px-2 py-0.5 rounded">
                REVIEW & AUTHORIZE
              </span>
            </div>
            <p className="text-xs text-indigo-200/80 mt-1">
              Review requests, check proposed employee & salary changes, verify policy alignment, and authorize workforce updates.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={loadApprovals}
            disabled={loading}
            className="p-2.5 bg-white/10 hover:bg-white/20 text-white rounded-xl transition text-xs font-semibold flex items-center gap-2 backdrop-blur-md"
            title="Refresh pending items"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            <span>Refresh Queue</span>
          </button>
        </div>
      </div>

      {/* Success Completion Toast Banner */}
      {completedSuccess && (
        <div className="bg-emerald-900 text-white p-4 rounded-2xl border border-emerald-700 shadow-lg flex items-center justify-between">
          <div className="flex items-center gap-3">
            <CheckCircle2 className="w-5 h-5 text-emerald-400" />
            <span className="text-xs font-bold">{completedSuccess}</span>
          </div>
          <button onClick={() => setCompletedSuccess(null)} className="text-emerald-300 hover:text-white text-xs font-semibold">
            Dismiss
          </button>
        </div>
      )}

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-4 bg-white p-4 rounded-2xl border border-slate-200 shadow-xs">
        <div className="flex items-center gap-2">
          <Clock className="w-4 h-4 text-amber-500" />
          <span className="text-xs font-bold text-slate-800">Pending Requests ({approvals.length})</span>
        </div>

        <div className="flex items-center gap-3">
          <select
            value={filterRisk}
            onChange={(e) => setFilterRisk(e.target.value)}
            className="px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold text-slate-700 focus:outline-none focus:border-indigo-500 transition-all cursor-pointer"
          >
            <option value="ALL">All Risk Levels</option>
            <option value="HIGH">High Risk Gate</option>
            <option value="MEDIUM">Medium Risk</option>
            <option value="LOW">Standard Operation</option>
          </select>

          <div className="relative flex-1 sm:w-64">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by requester or details..."
              className="w-full pl-9 pr-4 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold focus:outline-none focus:border-indigo-500 focus:bg-white transition-all"
            />
          </div>
        </div>
      </div>

      {/* Approvals List */}
      {loading ? (
        <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center space-y-3 shadow-xs">
          <RefreshCw className="w-8 h-8 text-indigo-600 animate-spin mx-auto" />
          <h4 className="font-bold text-slate-800 text-sm">Loading Approval Queue...</h4>
        </div>
      ) : filteredApprovals.length === 0 ? (
        <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center space-y-3 shadow-xs">
          <div className="w-14 h-14 bg-emerald-50 text-emerald-600 rounded-full flex items-center justify-center mx-auto">
            <CheckCircle2 className="w-8 h-8" />
          </div>
          <h4 className="font-bold text-slate-900 text-base">All Approvals Completed!</h4>
          <p className="text-xs text-slate-500 max-w-md mx-auto">
            There are currently no pending workforce requests awaiting HR authorization.
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          {filteredApprovals.map((app) => {
            const parsedPlan = parseApprovalReason(app.reason);
            const planTitle = parsedPlan?.title || (typeof app.reason === 'string' && !app.reason.startsWith('{') ? app.reason : `Workforce Request #${app.id.substring(0, 8)}`);
            const finImpact = parsedPlan?.totalFinancialImpact ?? 0;
            const steps = parsedPlan?.steps || [];
            const records = parsedPlan?.affectedRecords || [];
            const warnings = parsedPlan?.warnings || [];

            return (
              <div
                key={app.id}
                className="bg-white rounded-2xl border border-slate-200 shadow-md hover:shadow-lg transition-all overflow-hidden space-y-5 p-6"
              >
                {/* Header Bar */}
                <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border-b border-slate-100 pb-4">
                  <div className="flex items-center gap-3">
                    {getRiskBadge(app.riskLevel)}
                    <span className="text-xs text-slate-500">Requested by: <strong className="text-slate-800">{app.requestedBy || 'HR Admin'}</strong></span>
                  </div>
                  <span className="text-xs font-mono text-slate-400">Request Ref: #{app.id.substring(0, 8)}</span>
                </div>

                {/* Section 1: What You Requested */}
                <div className="space-y-2">
                  <span className="text-[10px] font-extrabold text-indigo-900 uppercase tracking-wider block">1. What You Requested</span>
                  <h3 className="text-base font-bold text-slate-900">{planTitle}</h3>
                </div>

                {/* Section 2: What Will Happen */}
                <div className="space-y-2 bg-slate-50 p-4 rounded-xl border border-slate-200/80">
                  <span className="text-[10px] font-extrabold text-slate-500 uppercase tracking-wider block">2. What Will Happen</span>
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
                    {steps.length > 0 ? (
                      steps.map((s, idx) => (
                        <div key={idx} className="flex items-center gap-2">
                          <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                          <span>{s.description}</span>
                        </div>
                      ))
                    ) : (
                      <>
                        <div className="flex items-center gap-2">
                          <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                          <span>Employee Profile Updated</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                          <span>Corporate Policy Verified</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                          <span>Audit Record Saved</span>
                        </div>
                      </>
                    )}
                  </div>
                  {warnings.length > 0 && (
                    <div className="pt-2 text-[11px] text-amber-800 font-medium">
                      ⚠️ Note: {warnings.join(', ')}
                    </div>
                  )}
                </div>

                {/* Section 3: People or Departments Affected */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="bg-indigo-50/60 border border-indigo-100 p-4 rounded-xl space-y-1">
                    <span className="text-[10px] font-extrabold text-indigo-700 uppercase tracking-wider block">3. People &amp; Departments Affected</span>
                    <p className="text-xs font-bold text-slate-900">
                      {records.length > 0 ? records.map(r => r.primaryLabel).join(', ') : 'Workforce Member Profile'}
                    </p>
                  </div>

                  <div className="bg-purple-50/60 border border-purple-100 p-4 rounded-xl space-y-1">
                    <span className="text-[10px] font-extrabold text-purple-700 uppercase tracking-wider block">Financial Impact</span>
                    <p className="text-sm font-black text-purple-950">
                      ${finImpact ? finImpact.toLocaleString(undefined, { minimumFractionDigits: 2 }) : '0.00'} annual adjustment
                    </p>
                  </div>
                </div>

                {/* Section 4: Changes to Be Made */}
                {records.length > 0 && (
                  <div className="space-y-2">
                    <span className="text-[10px] font-extrabold text-slate-500 uppercase tracking-wider block">4. Changes to Be Made</span>
                    <div className="border border-slate-200 rounded-xl overflow-hidden text-xs">
                      <table className="w-full text-left">
                        <thead className="bg-slate-50 text-slate-600 font-bold uppercase text-[10px]">
                          <tr>
                            <th className="p-3">Target Profile</th>
                            <th className="p-3">Information Field</th>
                            <th className="p-3">Current Value</th>
                            <th className="p-3">New Proposed Value</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                          {records.flatMap((rec, rIdx) =>
                            rec.changes.map((c, cIdx) => (
                              <tr key={`${rIdx}-${cIdx}`}>
                                <td className="p-3 font-bold text-slate-900">{rec.primaryLabel}</td>
                                <td className="p-3 text-indigo-700 font-semibold">{c.fieldName}</td>
                                <td className="p-3 text-slate-400 line-through">{c.oldValue}</td>
                                <td className="p-3 font-bold text-emerald-600">{c.newValue}</td>
                              </tr>
                            ))
                          )}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {/* Section 5: Confirm Action Buttons */}
                <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
                  <button
                    onClick={() => handleDecision(app.id, false)}
                    disabled={processingId === app.id}
                    className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl transition"
                  >
                    Decline Request
                  </button>

                  <button
                    onClick={() => handleDecision(app.id, true)}
                    disabled={processingId === app.id}
                    className="px-5 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl transition shadow-md shadow-emerald-600/20 flex items-center gap-2"
                  >
                    {processingId === app.id ? (
                      <>
                        <RefreshCw className="w-4 h-4 animate-spin" />
                        <span>Applying Changes...</span>
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="w-4 h-4 text-emerald-200" />
                        <span>Approve &amp; Apply Changes</span>
                      </>
                    )}
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

