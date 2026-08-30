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
    totalAudited: number;
    compliantCount: number;
    violationCount: number;
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

  const handleStatusAction = async (id: number, status: string, reason?: string) => {
    try {
      await updateExpenseStatus(id, status, reason);
      await loadData();
    } catch (err) {
      console.error("Failed to update expense status:", err);
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

  // Filtered Expense Claims
  const filteredExpenses = expenses.filter(exp => {
    if (selectedFilter === 'ALL') return true;
    const statusStr = exp.statusName || String(exp.status);
    if (selectedFilter === 'Pending') return statusStr.toLowerCase().includes('pending') || exp.status === 1;
    if (selectedFilter === 'Approved') return statusStr.toLowerCase().includes('approved') || exp.status === 2;
    if (selectedFilter === 'Compliant') return statusStr.toLowerCase().includes('compliant') || exp.status === 4;
    if (selectedFilter === 'NonCompliant') return statusStr.toLowerCase().includes('noncompliant') || statusStr.toLowerCase().includes('flag') || exp.status === 5 || exp.amount > 50;
    if (selectedFilter === 'Rejected') return statusStr.toLowerCase().includes('reject') || exp.status === 3;
    return true;
  });

  const totalAmount = expenses.reduce((sum, e) => sum + e.amount, 0);
  const violationCount = expenses.filter(e => e.amount > 50 || e.status === 5 || (e.statusName && e.statusName.toLowerCase().includes('noncompliant'))).length;

  return (
    <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
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
              Review employee expense claims against corporate policy limits, check meal/travel caps, and authorize approvals.
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
            <h3 className="text-2xl font-bold text-slate-900 mt-1">${totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2 })}</h3>
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
              {expenses.filter(e => e.amount <= 50 || e.status === 4 || (e.statusName && e.statusName.toLowerCase().includes('compliant'))).length}
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
              <span className="font-bold text-sm text-cyan-400">Nexus Agent Expense Audit Execution Report</span>
            </div>
            <button onClick={() => setAuditResult(null)} className="text-slate-400 hover:text-white">
              <X className="w-4 h-4" />
            </button>
          </div>

          <p className="text-xs text-slate-300 leading-relaxed font-medium">{auditResult.summary}</p>

          {auditResult.flaggedClaims.length > 0 && (
            <div className="space-y-2">
              <span className="text-xs font-bold text-rose-400 uppercase tracking-wider">Flagged Non-Compliant Claims:</span>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                {auditResult.flaggedClaims.map((fl: any, idx: number) => (
                  <div key={idx} className="p-3 bg-slate-950 border border-rose-900/50 rounded-xl space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="font-bold text-white">{fl.employeeName}</span>
                      <span className="text-rose-400 font-bold">${fl.amount.toFixed(2)}</span>
                    </div>
                    <p className="text-[11px] text-slate-400 leading-tight">{fl.reason}</p>
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
                  className={`px-3 py-1 rounded-lg text-xs font-semibold transition ${
                    selectedFilter === filter
                      ? 'bg-slate-900 text-white shadow-xs'
                      : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                  }`}
                >
                  {filter}
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
                  <th className="py-3.5 px-4">Employee</th>
                  <th className="py-3.5 px-4">Expense Category</th>
                  <th className="py-3.5 px-4">Claimed Amount</th>
                  <th className="py-3.5 px-4">Policy Limit</th>
                  <th className="py-3.5 px-4">Variance</th>
                  <th className="py-3.5 px-4">Compliance Status</th>
                  <th className="py-3.5 px-4 text-right">Agent Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-slate-700">
                {filteredExpenses.map((exp) => {
                  const emp = employees.find(e => e.id === exp.employeeId);
                  const empName = exp.employeeName || emp?.name || `Employee #${exp.employeeId}`;
                  const allowed = 50.00; // Meal default policy cap
                  const overflow = exp.amount - allowed;
                  const isViolation = overflow > 0 || exp.status === 5;
                  const statusStr = exp.statusName || String(exp.status);

                  return (
                    <tr key={exp.id} className="hover:bg-slate-50/80 transition">
                      <td className="py-3.5 px-4 font-bold text-slate-900">{empName}</td>
                      <td className="py-3.5 px-4 text-slate-700 font-medium">
                        {exp.category || exp.description || 'Client Meal / Expense'}
                      </td>
                      <td className="py-3.5 px-4 font-bold text-slate-900">${exp.amount.toFixed(2)}</td>
                      <td className="py-3.5 px-4 text-slate-600">${allowed.toFixed(2)}</td>
                      <td className={`py-3.5 px-4 font-bold ${isViolation ? 'text-rose-600' : 'text-emerald-600'}`}>
                        {isViolation ? `+$${overflow.toFixed(2)}` : `-$${Math.abs(overflow).toFixed(2)}`}
                      </td>
                      <td className="py-3.5 px-4">
                        <span className={`text-[10px] px-2.5 py-1 rounded-full font-bold inline-flex items-center gap-1 ${
                          isViolation
                            ? 'bg-rose-50 text-rose-700 border border-rose-200'
                            : statusStr.toLowerCase().includes('approved')
                            ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                            : 'bg-blue-50 text-blue-700 border border-blue-200'
                        }`}>
                          {isViolation ? 'NON_COMPLIANT' : statusStr.toUpperCase()}
                        </span>
                      </td>
                      <td className="py-3.5 px-4 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            onClick={() => handleStatusAction(exp.id, 'Approved', 'Approved by Manager')}
                            className="px-2.5 py-1 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 border border-emerald-200 rounded text-[11px] font-bold transition"
                            title="Approve claim"
                          >
                            Approve
                          </button>

                          <button
                            onClick={() => handleStatusAction(exp.id, 'NonCompliant', 'Exceeds POL-FIN-002 meal cap')}
                            className="px-2.5 py-1 bg-amber-50 hover:bg-amber-100 text-amber-700 border border-amber-200 rounded text-[11px] font-bold transition"
                            title="Flag for audit"
                          >
                            Flag
                          </button>

                          <button
                            onClick={() => handleStatusAction(exp.id, 'Rejected', 'Policy violation')}
                            className="px-2.5 py-1 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded text-[11px] font-bold transition"
                            title="Reject claim"
                          >
                            Reject
                          </button>
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

      {/* Submit Claim Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-blue-50 text-blue-600 rounded-xl">
                  <Receipt className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-base">Submit Expense Claim</h3>
                  <p className="text-xs text-slate-500">Record claim for automated policy verification.</p>
                </div>
              </div>
              <button onClick={() => setIsModalOpen(false)} className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleSubmitClaim} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Employee</label>
                <select
                  value={employeeId}
                  onChange={e => setEmployeeId(Number(e.target.value))}
                  className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500"
                >
                  {employees.map(e => (
                    <option key={e.id} value={e.id}>{e.name} ({e.designation})</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Category</label>
                  <select
                    value={expenseType}
                    onChange={e => setExpenseType(Number(e.target.value))}
                    className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500"
                  >
                    <option value={2}>Meal ($50 Cap)</option>
                    <option value={1}>Travel ($250 Cap)</option>
                    <option value={3}>Equipment ($500 Cap)</option>
                    <option value={4}>Software ($500 Cap)</option>
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Amount ($)</label>
                  <input
                    type="number"
                    step="0.01"
                    required
                    value={amount}
                    onChange={e => setAmount(Number(e.target.value))}
                    className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 font-bold"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Description / Business Purpose</label>
                <textarea
                  rows={3}
                  placeholder="Client dinner with prospective partner, travel lodging..."
                  value={description}
                  onChange={e => setDescription(e.target.value)}
                  className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500"
                />
              </div>

              <div className="flex items-center justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-xl transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white text-xs font-semibold rounded-xl transition disabled:opacity-50 shadow-md shadow-blue-600/20"
                >
                  {submitting ? 'Submitting...' : 'Submit Expense'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

