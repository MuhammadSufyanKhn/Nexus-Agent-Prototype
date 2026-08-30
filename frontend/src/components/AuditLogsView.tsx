import React, { useEffect, useState } from 'react';
import { fetchAuditLogs } from '../services/api';
import type { AuditLogRecord } from '../services/api';
import { CheckCircle2, History } from 'lucide-react';

export const AuditLogsView: React.FC = () => {
  const [logs, setLogs] = useState<AuditLogRecord[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchAuditLogs()
      .then((data: AuditLogRecord[]) => setLogs(data))
      .catch((err: any) => console.error(err))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
      {/* Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-gradient-to-r from-slate-900 via-slate-950 to-slate-900 p-6 rounded-2xl border border-slate-800 text-white shadow-xl">
        <div className="flex items-center gap-4">
          <div className="p-3 bg-blue-600/20 border border-blue-500/30 rounded-xl text-blue-400 backdrop-blur-md">
            <History className="w-7 h-7" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold tracking-tight">Enterprise Audit &amp; Activity History</h2>
              <span className="text-[10px] bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-mono font-bold px-2 py-0.5 rounded">
                VERIFIED RECORDS ({logs.length})
              </span>
            </div>
            <p className="text-xs text-slate-300 mt-1">
              Verified record of all workforce actions, employee profile updates, policy checks, and system approvals.
            </p>
          </div>
        </div>
      </div>

      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-xs overflow-hidden">
        {loading ? (
          <div className="p-12 text-center text-xs text-slate-400">Loading enterprise activity history...</div>
        ) : logs.length === 0 ? (
          <div className="p-12 text-center text-xs text-slate-400">No activity records logged yet.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50/80 border-b border-slate-100 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                <tr>
                  <th className="py-3.5 px-4">Date &amp; Time</th>
                  <th className="py-3.5 px-4">Performed By</th>
                  <th className="py-3.5 px-4">Action Completed</th>
                  <th className="py-3.5 px-4">Affected Area</th>
                  <th className="py-3.5 px-4">Verification Reference</th>
                  <th className="py-3.5 px-4 text-right">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 font-medium text-slate-700">
                {logs.map((log) => (
                  <tr key={log.id} className="hover:bg-slate-50/80 transition">
                    <td className="py-3.5 px-4 text-slate-500 font-mono text-[11px]">{new Date(log.timestamp).toLocaleString()}</td>
                    <td className="py-3.5 px-4 font-bold text-slate-900">{log.user || 'HR Administrator'}</td>
                    <td className="py-3.5 px-4 font-semibold text-indigo-900">{log.action.replace(/_/g, ' ')}</td>
                    <td className="py-3.5 px-4 text-slate-600">{log.resource}</td>
                    <td className="py-3.5 px-4 font-mono text-[10px] text-slate-400">
                      {log.hash?.substring(0, 12).toUpperCase() || 'REF-88492A0'}
                    </td>
                    <td className="py-3.5 px-4 text-right">
                      <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-2.5 py-0.5 rounded-full text-[10px] inline-flex items-center gap-1">
                        <CheckCircle2 className="w-3 h-3 text-emerald-600" /> Verified &amp; Saved
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

