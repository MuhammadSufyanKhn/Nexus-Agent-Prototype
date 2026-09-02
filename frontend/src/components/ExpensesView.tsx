import React, { useEffect, useState } from 'react';
import { fetchExpenses, fetchEmployees, auditExpensesWithAI, updateExpenseStatus, createExpenseClaim } from '../services/api';
import type { Expense, Employee } from '../services/api';
import { Receipt, AlertTriangle, Sparkles, Plus, CheckCircle2, ShieldCheck, RefreshCw, X, Filter, FileText } from 'lucide-react';

export const ExpensesView: React.FC = () => {
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedFilter, setSelectedFilter] = useState<string>('ALL');

  // AI Audit State
  const [auditing, setAuditing] = useState(false);
  const [auditResult, setAuditResult] = useState<{
    totalAudited?: number;
    claimsReviewed?: number;
    compliantCount?: number;
    compliantClaims?: number;
    violationCount?: number;
    flaggedClaimsCount?: number;
    totalPolicyVariance?: number;
    flaggedClaims: any[];
    summary: string;
  } | null>(null);

  // Submit Expense Modal
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [employeeId, setEmployeeId] = useState<number>(1);
  const [expenseType, setExpenseType] = useState<number>(2); // 2 = Meal
  const [amount, setAmount] = useState<number>(65.00);
  const [description, setDescription] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    setLoading(true);
    try {
      const [expenseData, empData] = await Promise.all([
        fetchExpenses(),
        fetchEmployees()
      ]);
      setExpenses(expenseData);
      setEmployees(empData);
      if (empData.length > 0) setEmployeeId(empData[0].id);
    } catch (err) {
      console.error("Expenses fetch error:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();

    const handleUpdate = (e: any) => {
      loadData();
      const detail = e?.detail;
      if (detail?.filter) {
        setSelectedFilter(detail.filter);
      } else if (detail?.intent === 'EXPENSE_FILTER') {
        setSelectedFilter('NonCompliant');
      }
    };

    window.addEventListener('nexus-data-updated', handleUpdate);
    return () => window.removeEventListener('nexus-data-updated', handleUpdate);
  }, []);

  const handleRunAiAudit = async () => {
    setAuditing(true);
    try {
      const res = await auditExpensesWithAI();
      setAuditResult(res);
      await loadData();
    } catch (err) {
      console.error("Failed to run AI expense audit:", err);
    } finally {
      setAuditing(false);
    }
  };

  // Action processing state
  const [processingId, setProcessingId] = useState<number | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const handleStatusAction = async (id: number, status: string, reason?: string) => {
    setProcessingId(id);
    try {
      const updated = await updateExpenseStatus(id, status, reason);
      setExpenses(prev => prev.map(e => e.id === id ? { 
        ...e, 
        status: updated.status, 
        statusName: updated.statusName, 
        complianceStatus: updated.complianceStatus,
        reviewedBy: updated.reviewedBy,
        reviewedDate: updated.reviewedDate,
        flagReason: updated.flagReason
      } : e));
      setToastMessage(`Claim ${updated.claimNumber || '#' + id} marked as ${status}.`);
      setTimeout(() => setToastMessage(null), 4000);
      await loadData();
      window.dispatchEvent(new CustomEvent('nexus-data-updated'));
    } catch (err: any) {
      console.error("Failed to update expense status:", err);
      setToastMessage(`Failed to update claim: ${err.message || 'Server error'}`);
      setTimeout(() => setToastMessage(null), 4000);
    } finally {
      setProcessingId(null);
    }
  };

  const handleSubmitClaim = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      await createExpenseClaim({
        employeeId,
        expenseType,
        amount,
        description: description || 'Corporate Expense Claim'
      });
      setIsModalOpen(false);
      setDescription('');
      await loadData();
    } catch (err) {
      console.error("Failed to submit expense claim:", err);
    } finally {
      setSubmitting(false);
    }
  };

  // Helper for POL-FIN-002 policy limits
  const getPolicyLimit = (exp: Expense): number => {
    if (exp.policyLimit && exp.policyLimit > 0) return exp.policyLimit;
    const cat = (exp.category || '').toLowerCase();
    if (cat.includes('travel') || cat.includes('flight')) return 250.00;
    if (cat.includes('equip') || cat.includes('hardware')) return 500.00;
    if (cat.includes('soft') || cat.includes('tool') || cat.includes('license')) return 500.00;
    if (cat.includes('meal') || cat.includes('dinner') || cat.includes('lunch') || cat.includes('food')) return 50.00;
    return 100.00;
  };

  const getVariance = (exp: Expense): number => {
    if (typeof exp.variance === 'number') return exp.variance;
    return exp.amount - getPolicyLimit(exp);
  };

  const isViolation = (exp: Expense): boolean => {
    const statusStr = (exp.statusName || String(exp.status)).toLowerCase();
    if (statusStr.includes('approved') || exp.status === 2) return false;
    if (statusStr.includes('rejected') || exp.status === 3) return false;

    const comp = (exp.complianceStatus || '').toLowerCase();
    if (comp === 'flagged' || comp === 'noncompliant') return true;
    if (exp.status === 5 || statusStr.includes('noncompliant')) return true;
    return getVariance(exp) > 0;
  };

  // Filtered Expense Claims
  const filteredExpenses = expenses.filter(exp => {
    if (selectedFilter === 'ALL') return true;
    const statusStr = (exp.statusName || String(exp.status)).toLowerCase();
    const isApproved = statusStr.includes('approved') || exp.status === 2;
    const isRejected = statusStr.includes('rejected') || exp.status === 3;
    const isViol = !isApproved && !isRejected && isViolation(exp);

    if (selectedFilter === 'Pending') return (statusStr.includes('pending') || exp.status === 1) && !isViol;
    if (selectedFilter === 'Approved') return isApproved;
    if (selectedFilter === 'Compliant') return !isViol;
    if (selectedFilter === 'NonCompliant' || selectedFilter === 'Flagged') return isViol;
    if (selectedFilter === 'Rejected') return isRejected;
    return true;
  });

  const totalAmount = expenses.reduce((sum, e) => sum + e.amount, 0);
  const violationCount = expenses.filter(isViolation).length;
  const compliantCount = expenses.filter(e => !isViolation(e)).length;

  return (
    <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
      {/* Action Feedback Toast */}
      {toastMessage && (
        <div className="p-3.5 bg-emerald-50 border border-emerald-200 rounded-xl text-xs font-bold text-emerald-800 flex items-center justify-between shadow-xs animate-in fade-in duration-200">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-600" />
            <span>{toastMessage}</span>
          </div>
          <button onClick={() => setToastMessage(null)} className="text-emerald-700 hover:text-emerald-900 cursor-pointer">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Top Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-gradient-to-r from-slate-900 via-slate-950 to-slate-900 p-6 rounded-2xl border border-slate-800 text-white shadow-xl">
        <div className="flex items-center gap-4">
          <div className="p-3 bg-blue-600/20 border border-blue-500/30 rounded-xl text-blue-400 backdrop-blur-md">
            <Receipt className="w-7 h-7" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold tracking-tight">Expense Review & Policy Compliance</h2>
              <span className="text-[10px] bg-blue-500/20 text-blue-300 border border-blue-500/30 font-mono font-bold px-2 py-0.5 rounded">
                POL-FIN-002 ENFORCED
              </span>
            </div>
            <p className="text-xs text-slate-300 mt-1">
              Review employee expense claims against corporate policy limits, check meal ($50) & travel ($250) caps, and authorize approvals.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={handleRunAiAudit}
            disabled={auditing}
            className="px-4 py-2.5 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold rounded-xl transition text-xs flex items-center gap-2 shadow-lg shadow-blue-600/20 disabled:opacity-50 cursor-pointer"
          >
            {auditing ? (
              <>
                <RefreshCw className="w-4 h-4 animate-spin" />
                <span>Checking Compliance...</span>
              </>
            ) : (
              <>
                <Sparkles className="w-4 h-4 text-cyan-300" />
                <span>Run AI Policy Compliance Sweep</span>
              </>
            )}
          </button>

          <button
            onClick={() => setIsModalOpen(true)}
            className="px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-white border border-slate-700 font-semibold rounded-xl transition text-xs flex items-center gap-2 cursor-pointer"
          >
            <Plus className="w-4 h-4" />
            <span>Submit Expense Claim</span>
          </button>
        </div>
      </div>

      {/* Metrics Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Total Expenses Claimed</p>
            <h3 className="text-2xl font-bold text-slate-900 mt-1">${totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</h3>
            <span className="text-[11px] text-slate-400 font-medium">{expenses.length} Total Claims</span>
          </div>
          <div className="p-3 bg-blue-50 text-blue-600 rounded-xl">
            <Receipt className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Policy Violations</p>
            <h3 className="text-2xl font-bold text-rose-600 mt-1">{violationCount}</h3>
            <span className="text-[11px] text-rose-500 font-semibold">Exceeds POL-FIN-002 Cap</span>
          </div>
          <div className="p-3 bg-rose-50 text-rose-600 rounded-xl">
            <AlertTriangle className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Compliant Claims</p>
            <h3 className="text-2xl font-bold text-emerald-600 mt-1">
              {compliantCount}
            </h3>
            <span className="text-[11px] text-emerald-600 font-medium">Within Category Caps</span>
          </div>
          <div className="p-3 bg-emerald-50 text-emerald-600 rounded-xl">
            <CheckCircle2 className="w-5 h-5" />
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">Corporate Policy Rules</p>
            <h3 className="text-sm font-bold text-indigo-900 mt-1">Meal: $50 • Travel: $250</h3>
            <span className="text-[11px] text-slate-400 font-mono">POL-FIN-002 Thresholds</span>
          </div>
          <div className="p-3 bg-indigo-50 text-indigo-600 rounded-xl">
            <FileText className="w-5 h-5" />
          </div>
        </div>
      </div>

      {/* AI Audit Results Summary Banner if available */}
      {auditResult && (
        <div className="bg-slate-900 text-white p-5 rounded-2xl border border-blue-900/60 shadow-xl space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <ShieldCheck className="w-5 h-5 text-cyan-400" />
              <span className="font-bold text-sm text-cyan-400">Nexus Agent Expense Compliance Sweep Report</span>
            </div>
            <button onClick={() => setAuditResult(null)} className="text-slate-400 hover:text-white">
              <X className="w-4 h-4" />
            </button>
          </div>

          <p className="text-xs text-slate-300 leading-relaxed font-medium whitespace-pre-line">{auditResult.summary}</p>

          {auditResult.flaggedClaims && auditResult.flaggedClaims.length > 0 && (
            <div className="space-y-2">
              <span className="text-xs font-bold text-rose-400 uppercase tracking-wider">Flagged Non-Compliant Claims:</span>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                {auditResult.flaggedClaims.map((fl: any, idx: number) => (
                  <div key={idx} className="p-3 bg-slate-950 border border-rose-900/50 rounded-xl space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-white">{fl.claimNumber ? `${fl.claimNumber} • ` : ''}{fl.employeeName}</span>
                      <span className="text-rose-400 font-bold">${fl.amount.toFixed(2)}</span>
                    </div>
                    <p className="text-[11px] text-slate-400 leading-tight">{fl.flagReason || fl.reason}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Filters and Table */}
      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-xs overflow-hidden space-y-4 p-4">
        <div className="flex flex-col sm:flex-row items-center justify-between gap-3 border-b border-slate-100 pb-4">
          <div className="flex items-center gap-2">
            <Filter className="w-4 h-4 text-slate-400" />
            <span className="text-xs font-bold text-slate-700">Filter Claims:</span>
            <div className="flex items-center gap-1 overflow-x-auto">
              {['ALL', 'Pending', 'Approved', 'Compliant', 'NonCompliant', 'Rejected'].map((filter) => (
                <button
                  key={filter}
                  onClick={() => setSelectedFilter(filter)}
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition cursor-pointer ${
                    selectedFilter === filter
                      ? 'bg-slate-900 text-white shadow-xs'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  {filter === 'NonCompliant' ? 'Flagged / Violations' : filter}
                </button>
              ))}
            </div>
          </div>
        </div>

        {loading ? (
          <div className="p-12 text-center text-slate-400 text-xs flex items-center justify-center gap-2">
            <RefreshCw className="w-4 h-4 animate-spin text-blue-600" />
            <span>Syncing expense claims from SQL database...</span>
          </div>
        ) : filteredExpenses.length === 0 ? (
          <div className="p-12 text-center text-slate-400 text-xs space-y-2">
            <Receipt className="w-8 h-8 text-slate-300 mx-auto" />
            <p className="font-bold text-slate-700">No expense claims match the selected filter</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50/80 border-b border-slate-100 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                <tr>
                  <th className="py-3.5 px-4">Claim #</th>
                  <th className="py-3.5 px-4">Employee</th>
                  <th className="py-3.5 px-4">Category</th>
                  <th className="py-3.5 px-4">Claimed</th>
                  <th className="py-3.5 px-4">Policy Cap</th>
                  <th className="py-3.5 px-4">Variance</th>
                  <th className="py-3.5 px-4">Compliance Status</th>
                  <th className="py-3.5 px-4 text-right">Agent Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-slate-700">
                {filteredExpenses.map((exp) => {
                  const emp = employees.find(e => e.id === exp.employeeId);
                  const empName = exp.employeeName || emp?.name || `Employee #${exp.employeeId}`;
                  const claimNo = exp.claimNumber || `EXP-${exp.id.toString().padStart(4, '0')}`;
                  const allowed = getPolicyLimit(exp);
                  const overflow = getVariance(exp);
                  const statusStr = (exp.statusName || String(exp.status)).toLowerCase();
                  const isApproved = statusStr.includes('approved') || exp.status === 2;
                  const isRejected = statusStr.includes('rejected') || exp.status === 3;
                  const isViol = !isApproved && !isRejected && isViolation(exp);

                  return (
                    <tr key={exp.id} className="hover:bg-slate-50/80 transition">
                      <td className="py-3.5 px-4 font-mono font-bold text-blue-700">{claimNo}</td>
                      <td className="py-3.5 px-4 font-bold text-slate-900">{empName}</td>
                      <td className="py-3.5 px-4 text-slate-700 font-medium">
                        {exp.category || exp.description || 'General Expense'}
                      </td>
                      <td className="py-3.5 px-4 font-bold text-slate-900">${exp.amount.toFixed(2)}</td>
                      <td className="py-3.5 px-4 text-slate-600">${allowed.toFixed(2)}</td>
                      <td className={`py-3.5 px-4 font-bold ${isViol ? 'text-rose-600' : 'text-emerald-600'}`}>
                        {overflow > 0 ? `+$${overflow.toFixed(2)}` : `-$${Math.abs(overflow).toFixed(2)}`}
                      </td>
                      <td className="py-3.5 px-4">
                        <span className={`text-[10px] px-2.5 py-1 rounded-full font-bold inline-flex items-center gap-1 ${
                          isApproved
                            ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                            : isRejected
                            ? 'bg-rose-50 text-rose-700 border border-rose-200'
                            : isViol
                            ? 'bg-amber-50 text-amber-700 border border-amber-200'
                            : 'bg-blue-50 text-blue-700 border border-blue-200'
                        }`}>
                          {isApproved ? 'APPROVED' : isRejected ? 'REJECTED' : isViol ? 'FLAGGED' : 'PENDING'}
                        </span>
                      </td>
                      <td className="py-3.5 px-4 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          {processingId === exp.id ? (
                            <span className="text-[11px] text-slate-500 font-semibold flex items-center gap-1">
                              <RefreshCw className="w-3 h-3 animate-spin text-blue-600" /> Saving...
                            </span>
                          ) : (
                            <>
                              <button
                                onClick={() => handleStatusAction(exp.id, 'Approved', 'Approved by Manager')}
                                disabled={isApproved}
                                className={`px-2.5 py-1 rounded text-[11px] font-bold transition cursor-pointer ${
                                  isApproved
                                    ? 'bg-emerald-100/40 text-emerald-500 border border-emerald-200 opacity-60 cursor-not-allowed'
                                    : 'bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-200'
                                }`}
                                title="Approve claim"
                              >
                                Approve
                              </button>

                              <button
                                onClick={() => handleStatusAction(exp.id, 'NonCompliant', 'Flagged for manager policy review')}
                                disabled={isViol}
                                className={`px-2.5 py-1 rounded text-[11px] font-bold transition cursor-pointer ${
                                  isViol
                                    ? 'bg-amber-100/40 text-amber-500 border border-amber-200 opacity-60 cursor-not-allowed'
                                    : 'bg-amber-50 hover:bg-amber-100 text-amber-700 border border-amber-200'
                                }`}
                                title="Flag for audit"
                              >
                                Flag
                              </button>

                              <button
                                onClick={() => handleStatusAction(exp.id, 'Rejected', 'Policy violation')}
                                disabled={isRejected}
                                className={`px-2.5 py-1 rounded text-[11px] font-bold transition cursor-pointer ${
                                  isRejected
                                    ? 'bg-rose-100/40 text-rose-500 border border-rose-200 opacity-60 cursor-not-allowed'
                                    : 'bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200'
                                }`}
                                title="Reject claim"
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
        )}
      </div>

      {/* Submit Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-xs p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-slate-200">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2">
                <Receipt className="w-5 h-5 text-blue-600" />
                <h3 className="font-bold text-slate-900">Submit New Expense Claim</h3>
              </div>
              <button onClick={() => setIsModalOpen(false)} className="text-slate-400 hover:text-slate-600">
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleSubmitClaim} className="space-y-4 text-xs">
              <div>
                <label className="block text-slate-700 font-semibold mb-1">Employee</label>
                <select
                  value={employeeId}
                  onChange={(e) => setEmployeeId(Number(e.target.value))}
                  className="w-full border border-slate-200 rounded-xl p-2.5 text-slate-800 bg-white"
                >
                  {employees.map(emp => (
                    <option key={emp.id} value={emp.id}>{emp.name} ({emp.designation || 'Staff'})</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-slate-700 font-semibold mb-1">Expense Type</label>
                <select
                  value={expenseType}
                  onChange={(e) => setExpenseType(Number(e.target.value))}
                  className="w-full border border-slate-200 rounded-xl p-2.5 text-slate-800 bg-white"
                >
                  <option value={1}>Travel Reimbursement (Cap: $250.00)</option>
                  <option value={2}>Business Meal / Dinner (Cap: $50.00)</option>
                  <option value={3}>Equipment / Hardware (Cap: $500.00)</option>
                  <option value={4}>Software / License (Cap: $500.00)</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-700 font-semibold mb-1">Claim Amount ($)</label>
                <input
                  type="number"
                  step="0.01"
                  required
                  value={amount}
                  onChange={(e) => setAmount(Number(e.target.value))}
                  className="w-full border border-slate-200 rounded-xl p-2.5 text-slate-800"
                />
              </div>

              <div>
                <label className="block text-slate-700 font-semibold mb-1">Description</label>
                <textarea
                  rows={2}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="e.g. Client Dinner with partners"
                  className="w-full border border-slate-200 rounded-xl p-2.5 text-slate-800"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 border border-slate-200 rounded-xl text-slate-600 font-semibold hover:bg-slate-50 cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-xl font-bold shadow-md shadow-blue-600/20 disabled:opacity-50 cursor-pointer"
                >
                  {submitting ? 'Submitting...' : 'Submit Claim'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
