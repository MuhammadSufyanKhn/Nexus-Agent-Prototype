import React, { useEffect, useState } from 'react';
import { fetchPendingApprovals, decideApproval } from '../services/api';
import type { PendingApproval } from '../services/api';
import { CheckSquare, CheckCircle2 } from 'lucide-react';

interface ApprovalsViewProps {
  userRole: string;
  onApprovalChanged: () => void;
}

export const ApprovalsView: React.FC<ApprovalsViewProps> = ({
  userRole,
  onApprovalChanged
}) => {
  const [approvals, setApprovals] = useState<PendingApproval[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<'PENDING' | 'APPROVED' | 'REJECTED'>('PENDING');

  const loadApprovals = async () => {
    setLoading(true);
    try {
      const data = await fetchPendingApprovals();
      setApprovals(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadApprovals();
  }, []);

  const handleDecision = async (approvalId: string, approved: boolean) => {
    try {
      await decideApproval({
        approvalId,
        approved,
        approvedBy: `${userRole} Administrator`,
        reason: approved ? 'Approved by HR Executive' : 'Rejected by HR Executive'
      });
      loadApprovals();
      onApprovalChanged();
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-amber-50 text-amber-600 rounded-lg">
            <CheckSquare className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Enterprise Approval Center</h3>
            <p className="text-xs text-slate-500">Review high-risk workforce operations and authorize pre-execution action plans.</p>
          </div>
        </div>

        {/* Tabs */}
        <div className="flex bg-slate-100 p-1 rounded-lg text-xs font-bold text-slate-600">
          <button
            onClick={() => setTab('PENDING')}
            className={`px-3 py-1 rounded-md transition-all ${tab === 'PENDING' ? 'bg-white text-slate-900 shadow-xs' : 'hover:text-slate-900'}`}
          >
            Pending ({approvals.length})
          </button>
        </div>
      </div>

      {/* Approvals List */}
      {loading ? (
        <div className="bg-white p-8 rounded-xl border border-slate-200 text-center text-xs text-slate-400">
          Loading approval requests...
        </div>
      ) : approvals.length === 0 ? (
        <div className="bg-white p-8 rounded-xl border border-slate-200 text-center space-y-2">
          <CheckCircle2 className="w-8 h-8 text-emerald-500 mx-auto" />
          <h4 className="font-bold text-slate-800 text-sm">No Pending Approvals</h4>
          <p className="text-xs text-slate-500 max-w-sm mx-auto">
            All high-risk HR actions have been reviewed and processed. Zero pending authorization bottlenecks.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {approvals.map((app) => (
            <div key={app.id} className="bg-white rounded-xl border border-slate-200 p-5 shadow-2xs space-y-4">
              <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                <div className="flex items-center gap-2">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-amber-700 bg-amber-50 border border-amber-200 px-2 py-0.5 rounded">
                    Risk Level: {app.riskLevel}
                  </span>
                  <span className="text-xs font-semibold text-slate-500">Requested by: {app.requestedBy}</span>
                </div>
                <span className="text-xs text-slate-400 font-medium">
                  {new Date(app.createdAt).toLocaleTimeString()}
                </span>
              </div>

              <div>
                <h4 className="font-bold text-slate-900 text-base">Workforce Action Plan #{app.id.substring(0, 8)}</h4>
                {app.reason && (
                  <p className="text-xs text-slate-600 mt-1 font-medium bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    {app.reason}
                  </p>
                )}
              </div>

              <div className="flex items-center justify-end gap-3 pt-2">
                <button
                  onClick={() => handleDecision(app.id, false)}
                  className="px-4 py-1.5 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 rounded-lg text-xs font-bold transition-colors"
                >
                  Reject Action
                </button>
                <button
                  onClick={() => handleDecision(app.id, true)}
                  className="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition-colors shadow-xs"
                >
                  Authorize & Execute
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
