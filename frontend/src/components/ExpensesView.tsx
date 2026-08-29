import React, { useEffect, useState } from 'react';
import { fetchExpenses, fetchEmployees } from '../services/api';
import type { Expense, Employee } from '../services/api';
import { Receipt, AlertTriangle } from 'lucide-react';

export const ExpensesView: React.FC = () => {
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      fetchExpenses(),
      fetchEmployees()
    ]).then(([expenseData, empData]) => {
      setExpenses(expenseData);
      setEmployees(empData);
    }).catch(err => console.error("Expenses fetch error:", err))
      .finally(() => setLoading(false));
  }, []);

  // Locate non-compliant expense claims
  const violation = expenses.find(e => e.amount > 50 || e.statusName?.toLowerCase().includes("violation") || e.status === 3);

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <Receipt className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Expense Audit & Compliance ({expenses.length})</h3>
            <p className="text-xs text-slate-500">Live database audit of employee expense claims against POL-FIN-002 corporate limits.</p>
          </div>
        </div>
      </div>

      {/* Non-Compliant Highlight Banner if present */}
      {violation && (
        <div className="bg-rose-50 border border-rose-200 rounded-xl p-5 shadow-2xs space-y-3">
          <div className="flex items-center gap-2 text-rose-900 font-bold text-sm">
            <AlertTriangle className="w-5 h-5 text-rose-600" />
            <span>Policy Violation Alert: {violation.employeeName || employees.find(e => e.id === violation.employeeId)?.name || `Employee #${violation.employeeId}`} (${violation.amount.toFixed(2)} Claim)</span>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs bg-white p-3 rounded-lg border border-rose-150">
            <div>
              <span className="text-slate-400 block text-[10px]">Claimed Amount</span>
              <span className="font-bold text-slate-900">${violation.amount.toFixed(2)}</span>
            </div>
            <div>
              <span className="text-slate-400 block text-[10px]">Allowed Policy Cap</span>
              <span className="font-bold text-slate-900">$50.00 (POL-FIN-002)</span>
            </div>
            <div>
              <span className="text-slate-400 block text-[10px]">Exceeded Overflow</span>
              <span className="font-bold text-rose-600">+${(violation.amount - 50).toFixed(2)} Exceeded</span>
            </div>
          </div>
        </div>
      )}

      {/* Table */}
      <div className="bg-white rounded-xl border border-slate-200 shadow-2xs overflow-hidden">
        {loading ? (
          <div className="p-8 text-center text-xs text-slate-400">Syncing expense records from SQL database...</div>
        ) : (
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-semibold uppercase tracking-wider">
              <tr>
                <th className="py-3 px-4">Employee</th>
                <th className="py-3 px-4">Expense Category</th>
                <th className="py-3 px-4">Claimed</th>
                <th className="py-3 px-4">Policy Cap</th>
                <th className="py-3 px-4">Variance</th>
                <th className="py-3 px-4">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {expenses.map((exp) => {
                const emp = employees.find(e => e.id === exp.employeeId);
                const empName = exp.employeeName || emp?.name || `Employee #${exp.employeeId}`;
                const allowed = 50.00;
                const overflow = exp.amount - allowed;
                const isViolation = overflow > 0;

                return (
                  <tr key={exp.id} className="hover:bg-slate-50/80">
                    <td className="py-3 px-4 font-bold text-slate-900">{empName}</td>
                    <td className="py-3 px-4 text-slate-700">{exp.category || exp.description || 'Client Lunch'}</td>
                    <td className="py-3 px-4 font-bold text-slate-900">${exp.amount.toFixed(2)}</td>
                    <td className="py-3 px-4 text-slate-600">${allowed.toFixed(2)}</td>
                    <td className={`py-3 px-4 font-bold ${isViolation ? 'text-rose-600' : 'text-emerald-600'}`}>
                      {isViolation ? `+$${overflow.toFixed(2)}` : `-$${Math.abs(overflow).toFixed(2)}`}
                    </td>
                    <td className="py-3 px-4">
                      <span className={`text-[10px] px-2 py-0.5 rounded font-bold ${
                        isViolation
                          ? 'bg-rose-50 text-rose-700 border border-rose-200'
                          : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                      }`}>
                        {isViolation ? 'NON_COMPLIANT' : 'COMPLIANT'}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};
