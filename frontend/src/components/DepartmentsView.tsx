import React, { useEffect, useState } from 'react';
import { fetchDepartments, fetchBudgets, fetchEmployees, fetchMasterBudget } from '../services/api';
import type { Department, Budget, Employee, MasterBudgetInfo } from '../services/api';
import { Building2, Landmark } from 'lucide-react';

export const DepartmentsView: React.FC = () => {
  const [departments, setDepartments] = useState<Department[]>([]);
  const [budgets, setBudgets] = useState<Budget[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [masterBudget, setMasterBudget] = useState<MasterBudgetInfo | null>(null);
  const [loading, setLoading] = useState(true);

  const loadData = () => {
    setLoading(true);
    Promise.all([
      fetchDepartments(),
      fetchBudgets(),
      fetchEmployees(),
      fetchMasterBudget()
    ]).then(([deptData, budgetData, empData, masterData]) => {
      setDepartments(deptData);
      setBudgets(budgetData);
      setEmployees(empData);
      setMasterBudget(masterData);
    }).catch(err => console.error("Departments fetch error:", err))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData();

    const handleUpdate = () => {
      loadData();
    };

    window.addEventListener('nexus-data-updated', handleUpdate);
    window.addEventListener('budget-updated', handleUpdate);
    return () => {
      window.removeEventListener('nexus-data-updated', handleUpdate);
      window.removeEventListener('budget-updated', handleUpdate);
    };
  }, []);

  const totalAllocatedFromDepts = departments.reduce((sum, d) => {
    const deptBudget = budgets.find(b => b.departmentId === d.id);
    const amt = d.allocatedBudget ?? deptBudget?.allocatedAmount ?? deptBudget?.actualAmount ?? 0;
    return sum + amt;
  }, 0);

  const masterPool = masterBudget?.totalBudgetPool ?? 1000000000;
  const totalAllocated = masterBudget?.totalAllocatedAcrossDepartments || totalAllocatedFromDepts;
  const remainingPool = masterPool - totalAllocated;

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Top Banner: Master Corporate Budget Pool */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 text-white rounded-2xl p-6 shadow-xl border border-indigo-500/20 space-y-4">
        <div className="flex items-center justify-between border-b border-indigo-500/20 pb-4">
          <div className="flex items-center gap-3">
            <div className="p-3 bg-indigo-500/20 text-indigo-300 rounded-xl border border-indigo-400/30">
              <Landmark className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 bg-indigo-500/30 text-indigo-200 rounded border border-indigo-400/30">
                  Fiscal Year {masterBudget?.fiscalYear || '2026-2027'}
                </span>
                <span className="text-xs text-indigo-300 font-medium">• Real-Time HR Sync</span>
              </div>
              <h3 className="text-xl font-extrabold tracking-tight text-white mt-1">
                Master Corporate Budget Pool
              </h3>
            </div>
          </div>
          <div className="text-right">
            <span className="text-xs text-indigo-300 font-semibold block uppercase tracking-wider">Total Corporate Budget</span>
            <span className="text-2xl font-black text-emerald-400 tracking-tight">
              ${masterPool.toLocaleString()}
            </span>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-1 text-xs">
          <div className="bg-white/5 backdrop-blur-xs p-3.5 rounded-xl border border-white/10 space-y-1">
            <span className="text-slate-400 block text-[11px]">Total Allocated to Departments</span>
            <span className="text-lg font-bold text-white">${totalAllocated.toLocaleString()}</span>
          </div>
          <div className="bg-white/5 backdrop-blur-xs p-3.5 rounded-xl border border-white/10 space-y-1">
            <span className="text-slate-400 block text-[11px]">Remaining Unallocated Pool Balance</span>
            <span className="text-lg font-bold text-emerald-400">${remainingPool.toLocaleString()}</span>
          </div>
          <div className="bg-white/5 backdrop-blur-xs p-3.5 rounded-xl border border-white/10 space-y-1">
            <span className="text-slate-400 block text-[11px]">Active Corporate Departments</span>
            <span className="text-lg font-bold text-indigo-300">{departments.length} Units</span>
          </div>
        </div>
      </div>

      <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <Building2 className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Department Allocations ({departments.length})</h3>
            <p className="text-xs text-slate-500">Authoritative SQL Server record data per organizational unit.</p>
          </div>
        </div>
      </div>

      {loading ? (
        <div className="bg-white p-8 text-center text-xs text-slate-400 rounded-xl border border-slate-200">
          Syncing department records from SQL database...
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {departments.map((dept) => {
            const deptEmps = employees.filter(e => e.departmentId === dept.id);
            const deptBudget = budgets.find(b => b.departmentId === dept.id);

            const allocated = dept.allocatedBudget ?? deptBudget?.allocatedAmount ?? deptBudget?.actualAmount ?? 0;
            const spent = dept.actualSpent ?? deptBudget?.spentAmount ?? 0;
            const remaining = dept.remainingBudget ?? (allocated - spent);
            const isExceeded = spent > allocated && allocated > 0;

            const managerName = dept.headOfDepartment || "Unassigned";

            return (
              <div key={dept.id} className="bg-white rounded-xl border border-slate-200 p-5 shadow-2xs space-y-4">
                <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                  <div>
                    <h4 className="font-bold text-slate-900 text-sm">{dept.name}</h4>
                    <p className="text-xs text-slate-400 font-medium">Head of Dept: {managerName}</p>
                  </div>
                  <span className={`text-[10px] px-2 py-0.5 rounded font-bold ${
                    isExceeded
                      ? 'bg-rose-50 text-rose-700 border border-rose-200'
                      : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                  }`}>
                    {isExceeded ? 'Budget Exceeded' : 'Compliant'}
                  </span>
                </div>

                <div className="grid grid-cols-4 gap-2 text-xs">
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Headcount</span>
                    <span className="font-bold text-slate-900">{deptEmps.length || dept.employeeCount || 0} emps</span>
                  </div>
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Allocated Budget</span>
                    <span className="font-bold text-slate-900">${allocated.toLocaleString()}</span>
                  </div>
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Actual Spent</span>
                    <span className={`font-bold ${isExceeded ? 'text-rose-600' : 'text-slate-900'}`}>
                      ${spent.toLocaleString()}
                    </span>
                  </div>
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Remaining Budget</span>
                    <span className={`font-bold ${remaining < 0 ? 'text-rose-600' : 'text-emerald-700'}`}>
                      ${remaining.toLocaleString()}
                    </span>
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
