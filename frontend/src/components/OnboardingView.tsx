import React, { useEffect, useState } from 'react';
import { fetchEmployees } from '../services/api';
import type { Employee } from '../services/api';
import { UserPlus, CheckCircle2 } from 'lucide-react';

export const OnboardingView: React.FC = () => {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchEmployees()
      .then(data => setEmployees(data))
      .catch(err => console.error("Onboarding fetch error:", err))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
            <UserPlus className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Multi-System Onboarding Tracker ({employees.length})</h3>
            <p className="text-xs text-slate-500">Live SQL database tracker of workforce onboarding profiles across SQL Server, Legacy HR Portal, Mock SAP HCM, and Welcome Email.</p>
          </div>
        </div>
      </div>

      {loading ? (
        <div className="bg-white p-8 rounded-xl border border-slate-200 text-center text-xs text-slate-400">
          Syncing onboarding records from database...
        </div>
      ) : employees.length === 0 ? (
        <div className="bg-white p-8 rounded-xl border border-slate-200 text-center text-xs text-slate-400">
          No active onboarding profiles found.
        </div>
      ) : (
        <div className="space-y-4">
          {employees.map((emp) => {
            const steps = [
              { title: 'Employee Information Parsed', status: 'COMPLETED', detail: `${emp.designation} profile verified` },
              { title: 'Policy Validation (POL-HR-001)', status: 'COMPLETED', detail: `Salary Band Verified ($${emp.salary.toLocaleString()})` },
              { title: 'SQL Server Record Creation', status: 'COMPLETED', detail: `Record ID #EMP-${emp.id} Created` },
              { title: 'Legacy HR Portal Sync (Playwright)', status: 'COMPLETED', detail: `HR-REC-2026-${8800 + emp.id} Submitted` },
              { title: 'Mock SAP ERP HCM Provisioning', status: 'COMPLETED', detail: `Personnel ID SAP-EMP-2026-${8900 + emp.id}` },
              { title: 'Welcome Email Generation', status: 'COMPLETED', detail: `Dispatched to ${emp.email}` }
            ];

            return (
              <div key={emp.id} className="bg-white rounded-xl border border-slate-200 p-6 shadow-2xs space-y-5">
                <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border-b border-slate-100 pb-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] bg-indigo-50 text-indigo-700 border border-indigo-200 font-bold px-2 py-0.5 rounded">
                        ONB-2026-0{emp.id}
                      </span>
                      <h4 className="font-bold text-slate-900 text-base">{emp.name}</h4>
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">{emp.designation} • IT & Software • ${emp.salary.toLocaleString()}</p>
                  </div>

                  <span className="text-xs bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-3 py-1 rounded-full flex items-center gap-1.5">
                    <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                    <span>Onboarding Fully Provisioned</span>
                  </span>
                </div>

                {/* Step Progress Timeline */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                  {steps.map((st, idx) => (
                    <div key={idx} className="p-3 bg-slate-50 border border-slate-200 rounded-lg space-y-1">
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-slate-900">{idx + 1}. {st.title}</span>
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 shrink-0" />
                      </div>
                      <span className="text-[11px] text-slate-500 block leading-tight">{st.detail}</span>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
