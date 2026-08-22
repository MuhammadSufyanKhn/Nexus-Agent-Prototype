import React, { useEffect, useState } from 'react';
import { fetchDepartments, fetchBudgets, fetchEmployees } from '../services/api';
import type { Department, Budget, Employee } from '../services/api';
import { Building2 } from 'lucide-react';

export const DepartmentsView: React.FC = () => {
  const [departments, setDepartments] = useState<Department[]>([]);
  const [budgets, setBudgets] = useState<Budget[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      fetchDepartments(),
      fetchBudgets(),
      fetchEmployees()
    ]).then(([deptData, budgetData, empData]) => {
      setDepartments(deptData);
      setBudgets(budgetData);
      setEmployees(empData);
    }).catch(err => console.error("Departments fetch error:", err))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <Building2 className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Corporate Departments ({departments.length})</h3>
            <p className="text-xs text-slate-500">Live database sync across headcount distribution and allocated Q3 budget status.</p>
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
            const allocated = deptBudget?.allocatedAmount ?? 50000;
            const spent = deptBudget?.spentAmount ?? deptBudget?.actualAmount ?? 0;
            const overflow = spent - allocated;
            const isExceeded = overflow > 0;

            let managerName = "Sarah Jenkins";
            if (dept.name.includes("Human")) managerName = "Tariq Mahmood";
            else if (dept.name.includes("Marketing")) managerName = "Ahmed Khan";
            else if (dept.name.includes("Operations")) managerName = "Tariq Mahmood";

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
                    {isExceeded ? `Budget Exceeded (${deptBudget?.quarter || 'Q3'})` : 'Compliant'}
                  </span>
                </div>

                <div className="grid grid-cols-3 gap-3 text-xs">
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Headcount</span>
                    <span className="font-bold text-slate-900">{deptEmps.length} employees</span>
                  </div>
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Allocated Q3 Budget</span>
                    <span className="font-bold text-slate-900">${allocated.toLocaleString()}</span>
                  </div>
                  <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block text-[10px]">Actual Q3 Spent</span>
                    <span className={`font-bold ${isExceeded ? 'text-rose-600' : 'text-slate-900'}`}>
                      ${spent.toLocaleString()}
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
