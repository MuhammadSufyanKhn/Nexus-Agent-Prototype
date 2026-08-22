import React, { useEffect, useState } from 'react';
import { fetchEmployees, fetchPendingApprovals, fetchDepartments, fetchBudgets, fetchExpenses } from '../services/api';
import type { Employee, Department, Budget } from '../services/api';
import { Users, CheckSquare, ShieldCheck, Building2, ArrowRight } from 'lucide-react';

interface DashboardViewProps {
  onNavigateToApprovals: () => void;
  onNavigateToConsole: () => void;
}

export const DashboardView: React.FC<DashboardViewProps> = ({
  onNavigateToApprovals,
  onNavigateToConsole
}) => {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [pendingApprovalsCount, setPendingApprovalsCount] = useState<number>(0);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [budgets, setBudgets] = useState<Budget[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      fetchEmployees(),
      fetchPendingApprovals(),
      fetchDepartments(),
      fetchBudgets(),
      fetchExpenses()
    ]).then(([empData, pendingData, deptData, budgetData]) => {
      setEmployees(empData);
      setPendingApprovalsCount(pendingData.length);
      setDepartments(deptData);
      setBudgets(budgetData);
    }).catch(err => console.error("Dashboard fetch error:", err))
      .finally(() => setLoading(false));
  }, []);

  const totalEmployees = employees.length;
  const activeEmployees = employees.filter(e => e.status === 1 || e.statusName === 'Active').length;

  const kpis = [
    {
      label: 'Total Workforce',
      value: loading ? '...' : totalEmployees.toString(),
      change: `${activeEmployees} active employees`,
      isPositive: true,
      icon: Users,
      color: 'blue'
    },
    {
      label: 'Pending Approvals',
      value: loading ? '...' : pendingApprovalsCount.toString(),
      change: pendingApprovalsCount > 0 ? `${pendingApprovalsCount} requiring action` : 'All requests cleared',
      isPositive: pendingApprovalsCount === 0,
      icon: CheckSquare,
      color: 'amber',
      action: onNavigateToApprovals
    },
    {
      label: 'Active Departments',
      value: loading ? '...' : departments.length.toString(),
      change: 'Corporate organizational units',
      isPositive: true,
      icon: Building2,
      color: 'emerald'
    },
    {
      label: 'System Status',
      value: '100% Online',
      change: 'SQL Server & Local AI active',
      isPositive: true,
      icon: ShieldCheck,
      color: 'indigo'
    }
  ];

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Top KPI Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {kpis.map((kpi, idx) => {
          const Icon = kpi.icon;
          return (
            <div
              key={idx}
              onClick={kpi.action}
              className={`bg-white rounded-xl border border-slate-200 p-5 shadow-2xs hover:shadow-md transition-all ${
                kpi.action ? 'cursor-pointer hover:border-blue-300' : ''
              }`}
            >
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">{kpi.label}</span>
                <div className={`p-2 rounded-lg ${
                  kpi.color === 'blue' ? 'bg-blue-50 text-blue-600' :
                  kpi.color === 'amber' ? 'bg-amber-50 text-amber-600' :
                  kpi.color === 'emerald' ? 'bg-emerald-50 text-emerald-600' : 'bg-indigo-50 text-indigo-600'
                }`}>
                  <Icon className="w-5 h-5" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-slate-900 tracking-tight">{kpi.value}</div>
                <div className={`text-xs font-semibold mt-1 ${
                  kpi.isPositive ? 'text-emerald-600' : 'text-amber-600'
                }`}>
                  {kpi.change}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Main Grid: Department Overview & AI Quick Actions */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Department Distribution */}
        <div className="lg:col-span-2 bg-white rounded-xl border border-slate-200 p-6 shadow-2xs space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <div className="flex items-center gap-2">
              <Building2 className="w-5 h-5 text-blue-600" />
              <h3 className="font-bold text-slate-900 text-sm">Department Headcount & Q3 Budget Status</h3>
            </div>
            <span className="text-xs font-semibold text-slate-400">Live Database Sync</span>
          </div>

          {loading ? (
            <div className="py-8 text-center text-xs text-slate-400">Syncing database state...</div>
          ) : (
            <div className="space-y-4">
              {departments.map((dept) => {
                const deptEmps = employees.filter(e => e.departmentId === dept.id);
                const deptBudget = budgets.find(b => b.departmentId === dept.id && b.quarter === 'Q3');
                const allocated = deptBudget?.allocatedAmount ?? 0;
                const actual = deptBudget?.spentAmount ?? deptBudget?.actualAmount ?? 0;
                const isOver = actual > allocated && allocated > 0;
                const pct = totalEmployees > 0 ? Math.round((deptEmps.length / totalEmployees) * 100) : 0;

                return (
                  <div key={dept.id} className="space-y-1.5">
                    <div className="flex items-center justify-between text-xs font-semibold">
                      <span className="text-slate-800">{dept.name}</span>
                      <div className="flex items-center gap-3">
                        <span className="text-slate-500">{deptEmps.length} employees ({pct}%)</span>
                        {allocated > 0 && (
                          <span className={`text-[10px] px-2 py-0.5 rounded font-bold ${
                            isOver ? 'bg-rose-50 text-rose-700 border border-rose-200' : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                          }`}>
                            {isOver ? 'Exceeded' : 'Compliant'}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="w-full h-2 bg-slate-100 rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full transition-all duration-500 ${
                          isOver ? 'bg-rose-500' : 'bg-blue-600'
                        }`}
                        style={{ width: `${Math.max(pct, 5)}%` }}
                      />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Quick Assistant Launch Card */}
        <div className="bg-slate-900 text-white rounded-xl p-6 shadow-md flex flex-col justify-between space-y-6">
          <div className="space-y-3">
            <div className="inline-flex items-center gap-1.5 text-[10px] uppercase font-bold tracking-wider px-2.5 py-1 bg-blue-500/20 text-blue-300 rounded-full border border-blue-400/30">
              ⚡ Autonomous AI Engine
            </div>
            <h3 className="text-lg font-bold tracking-tight text-white">NEXUS Workforce Operations</h3>
            <p className="text-xs text-slate-300 leading-relaxed">
              Ask NEXUS to evaluate HR policies, run natural language SQL analytics, manage employee onboarding, or execute multi-system enterprise automation.
            </p>
          </div>

          <button
            onClick={onNavigateToConsole}
            className="w-full py-2.5 px-4 bg-blue-600 hover:bg-blue-500 text-white font-bold text-xs rounded-lg transition-colors flex items-center justify-center gap-2 shadow-sm"
          >
            Launch AI Assistant <ArrowRight className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
};
