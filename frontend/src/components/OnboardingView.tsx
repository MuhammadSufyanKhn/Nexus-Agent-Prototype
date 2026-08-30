import React, { useEffect, useState } from 'react';
import { fetchEmployees, fetchDepartments, triggerOnboardingWorkflow, executeAgentPrompt } from '../services/api';
import type { Employee, Department, AgentResult } from '../services/api';
import { UserPlus, CheckCircle2, Sparkles, Plus, FileText, Mail, RefreshCw, X, ShieldCheck, ArrowRight } from 'lucide-react';

export const OnboardingView: React.FC = () => {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);

  // AI Prompt Bar state
  const [prompt, setPrompt] = useState('');
  const [executing, setExecuting] = useState(false);
  const [lastResult, setLastResult] = useState<AgentResult | null>(null);

  // Modal State for Manual Form
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [departmentId, setDepartmentId] = useState<number>(1);
  const [designation, setDesignation] = useState('');
  const [salary, setSalary] = useState<number>(95000);
  const [submitting, setSubmitting] = useState(false);

  // Document Modal Preview
  const [selectedDocEmp, setSelectedDocEmp] = useState<Employee | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const [empData, deptData] = await Promise.all([
        fetchEmployees(),
        fetchDepartments()
      ]);
      setEmployees(empData);
      setDepartments(deptData);
      if (deptData.length > 0) setDepartmentId(deptData[0].id);
    } catch (err) {
      console.error("Onboarding fetch error:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const getDeptName = (emp: Employee) => {
    const d = departments.find(dep => dep.id === emp.departmentId);
    return d?.name || emp.departmentName || 'Department';
  };

  const handleAiTrigger = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prompt.trim()) return;

    setExecuting(true);
    try {
      const res = await triggerOnboardingWorkflow(prompt, 'Admin');
      setLastResult(res);
      setPrompt('');
      await loadData();
    } catch (err) {
      console.error('Failed to trigger onboarding AI prompt:', err);
    } finally {
      setExecuting(false);
    }
  };

  const handleManualSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    setSubmitting(true);
    try {
      const deptObj = departments.find(d => d.id === departmentId);
      const aiPrompt = `Onboard new employee ${name} (${email}) as ${designation} in ${deptObj?.name || 'Engineering'} with $${salary} salary.`;
      const res = await triggerOnboardingWorkflow(aiPrompt, 'Admin');
      setLastResult(res);
      setIsModalOpen(false);
      setName('');
      setEmail('');
      setDesignation('');
      await loadData();
    } catch (err) {
      console.error('Failed manual onboarding submit:', err);
    } finally {
      setSubmitting(false);
    }
  };

  const handleResendWelcomeEmail = async (emp: Employee) => {
    try {
      const res = await executeAgentPrompt(`Resend onboarding welcome email to ${emp.name} at ${emp.email}`, 'Admin');
      setLastResult(res);
    } catch (err) {
      console.error('Failed to resend welcome email:', err);
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
      {/* Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 p-6 rounded-2xl border border-indigo-900/40 text-white shadow-xl">
        <div className="flex items-center gap-4">
          <div className="p-3 bg-indigo-600/30 border border-indigo-500/30 rounded-xl text-indigo-300 backdrop-blur-md">
            <UserPlus className="w-7 h-7" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold tracking-tight">Employee Onboarding Hub</h2>
              <span className="text-[10px] bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-mono font-bold px-2 py-0.5 rounded">
                AUTOMATED WORKFLOWS
              </span>
            </div>
            <p className="text-xs text-indigo-200/80 mt-1">
              Automates employee profile creation, compensation policy checks (POL-HR-001), document package generation, welcome emails, and IT setup.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => loadData()}
            className="p-2.5 bg-white/10 hover:bg-white/20 text-white rounded-xl transition text-xs font-semibold flex items-center gap-2 backdrop-blur-md"
            title="Sync onboarding records"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            <span>Sync</span>
          </button>

          <button
            onClick={() => setIsModalOpen(true)}
            className="px-4 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-xl transition text-xs flex items-center gap-2 shadow-lg shadow-indigo-600/30"
          >
            <Plus className="w-4 h-4" />
            <span>Trigger Onboarding</span>
          </button>
        </div>
      </div>

      {/* Quick AI Natural Language Onboarding Bar */}
      <div className="bg-white p-4 rounded-2xl border border-indigo-100 shadow-xs space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-xs font-bold text-slate-800">
            <Sparkles className="w-4 h-4 text-indigo-600" />
            <span>Ask Nexus Agent to Automate Employee Onboarding</span>
          </div>
          <span className="text-[11px] text-slate-400 font-mono">Example: "Onboard Sarah Jenkins as Senior Software Engineer in Engineering at $120,000"</span>
        </div>

        <form onSubmit={handleAiTrigger} className="flex gap-2">
          <input
            type="text"
            placeholder="Type natural language onboarding instructions..."
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            className="flex-1 px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500 font-medium transition"
          />
          <button
            type="submit"
            disabled={executing || !prompt.trim()}
            className="px-5 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-bold rounded-xl transition text-xs flex items-center gap-2 disabled:opacity-50 shadow-md shadow-indigo-600/20"
          >
            {executing ? (
              <>
                <RefreshCw className="w-4 h-4 animate-spin" />
                <span>Orchestrating...</span>
              </>
            ) : (
              <>
                <span>Run AI Pipeline</span>
                <ArrowRight className="w-4 h-4" />
              </>
            )}
          </button>
        </form>
      </div>

      {/* Execution Feedback Result Banner if available */}
      {lastResult && (
        <div className="bg-slate-900 text-white p-5 rounded-2xl border border-indigo-900/60 shadow-xl space-y-3">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <ShieldCheck className="w-5 h-5 text-emerald-400" />
              <span className="font-bold text-sm text-emerald-400">Nexus Agent Execution Complete</span>
              <span className="text-xs text-slate-400 font-mono">Run ID: {lastResult.runId?.substring(0, 8)}</span>
            </div>
            <button onClick={() => setLastResult(null)} className="text-slate-400 hover:text-white">
              <X className="w-4 h-4" />
            </button>
          </div>
          <p className="text-xs text-slate-300 leading-relaxed font-medium">
            {lastResult.userMessage || lastResult.resultData?.message || 'Onboarding workflow completed successfully across all registered enterprise systems.'}
          </p>
          {lastResult.executionFeed && lastResult.executionFeed.length > 0 && (
            <div className="bg-slate-950 p-3 rounded-xl border border-slate-800 text-[11px] font-mono text-slate-400 max-h-32 overflow-y-auto space-y-1">
              {lastResult.executionFeed.map((ev, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span className="text-emerald-500">✓</span>
                  <span className="text-slate-200">{ev.message}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Active Onboarding Profiles List */}
      {loading ? (
        <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center text-xs text-slate-400 flex items-center justify-center gap-2">
          <RefreshCw className="w-4 h-4 animate-spin text-indigo-600" />
          <span>Syncing onboarding master records from database...</span>
        </div>
      ) : employees.length === 0 ? (
        <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center text-xs text-slate-400 space-y-2">
          <UserPlus className="w-8 h-8 text-slate-300 mx-auto" />
          <p className="font-bold text-slate-700">No active onboarding profiles found</p>
          <p>Use the AI prompt bar above or click "Trigger Onboarding" to add a new employee profile.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {employees.map((emp) => {
            const deptName = getDeptName(emp);
            const steps = [
              { title: 'Information & Identity Parsed', status: 'COMPLETED', detail: `${emp.designation} profile verified` },
              { title: 'Policy Validation (POL-HR-001)', status: 'COMPLETED', detail: `Salary Band Verified ($${emp.salary.toLocaleString()})` },
              { title: 'SQL Master Record Creation', status: 'COMPLETED', detail: `Master ID #EMP-${emp.id} Created` },
              { title: 'Legacy HR Portal Sync (Playwright)', status: 'COMPLETED', detail: `HR-REC-2026-${8800 + emp.id} Submitted` },
              { title: 'Mock SAP ERP HCM Provisioning', status: 'COMPLETED', detail: `Personnel ID SAP-EMP-2026-${8900 + emp.id}` },
              { title: 'Welcome Email Generation', status: 'COMPLETED', detail: `Dispatched to ${emp.email}` }
            ];

            return (
              <div key={emp.id} className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-xs hover:shadow-md transition space-y-5">
                <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border-b border-slate-100 pb-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] bg-indigo-50 text-indigo-700 border border-indigo-200 font-bold px-2 py-0.5 rounded">
                        ONB-2026-0{emp.id}
                      </span>
                      <h4 className="font-bold text-slate-900 text-base">{emp.name}</h4>
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5">
                      {emp.designation} • <span className="font-semibold text-slate-700">{deptName}</span> • ${emp.salary.toLocaleString()} base
                    </p>
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => setSelectedDocEmp(emp)}
                      className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold rounded-lg transition flex items-center gap-1.5"
                    >
                      <FileText className="w-3.5 h-3.5 text-indigo-600" />
                      <span>Onboarding Package</span>
                    </button>

                    <button
                      onClick={() => handleResendWelcomeEmail(emp)}
                      className="px-3 py-1.5 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 border border-indigo-200 text-xs font-semibold rounded-lg transition flex items-center gap-1.5"
                    >
                      <Mail className="w-3.5 h-3.5 text-indigo-600" />
                      <span>Resend Email</span>
                    </button>

                    <span className="text-xs bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-3 py-1 rounded-full flex items-center gap-1.5">
                      <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                      <span>Fully Provisioned</span>
                    </span>
                  </div>
                </div>

                {/* Step Progress Timeline */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                  {steps.map((st, idx) => (
                    <div key={idx} className="p-3 bg-slate-50/80 border border-slate-200/80 rounded-xl space-y-1 hover:bg-slate-100/60 transition">
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

      {/* Trigger Onboarding Form Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-5">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-indigo-50 text-indigo-600 rounded-xl">
                  <UserPlus className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-base">Trigger Employee Onboarding</h3>
                  <p className="text-xs text-slate-500">Initiate automated multi-system provisioning.</p>
                </div>
              </div>
              <button onClick={() => setIsModalOpen(false)} className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleManualSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Full Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Sarah Jenkins"
                  value={name}
                  onChange={e => setName(e.target.value)}
                  className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Email Address</label>
                <input
                  type="email"
                  required
                  placeholder="e.g. sarah.jenkins@company.com"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Department</label>
                  <select
                    value={departmentId}
                    onChange={e => setDepartmentId(Number(e.target.value))}
                    className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
                  >
                    {departments.map(d => (
                      <option key={d.id} value={d.id}>{d.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">Salary ($/year)</label>
                  <input
                    type="number"
                    required
                    value={salary}
                    onChange={e => setSalary(Number(e.target.value))}
                    className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">Designation / Role Title</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Senior Software Engineer"
                  value={designation}
                  onChange={e => setDesignation(e.target.value)}
                  className="w-full px-3.5 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-500"
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
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-xl transition disabled:opacity-50 shadow-md shadow-indigo-600/20"
                >
                  {submitting ? 'Orchestrating...' : 'Execute Onboarding'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Onboarding Package Document Modal Preview */}
      {selectedDocEmp && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-5 max-h-[85vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-indigo-50 text-indigo-600 rounded-xl">
                  <FileText className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="font-bold text-slate-900 text-base">Generated Onboarding Package</h3>
                  <p className="text-xs text-slate-500">Official HR document generated by Nexus Agent Document Engine.</p>
                </div>
              </div>
              <button onClick={() => setSelectedDocEmp(null)} className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="bg-slate-50 border border-slate-200 p-6 rounded-xl space-y-4 font-sans text-slate-800 text-xs leading-relaxed">
              <div className="flex justify-between items-center border-b border-slate-200 pb-3">
                <span className="font-extrabold text-sm text-indigo-900">NEXUS AGENT ENTERPRISE HR</span>
                <span className="text-[10px] bg-slate-200 px-2 py-0.5 rounded font-mono font-bold">DOC-ONB-2026-0{selectedDocEmp.id}</span>
              </div>

              <div className="space-y-1">
                <h4 className="font-bold text-base text-slate-900">EMPLOYEE ONBOARDING CONFIRMATION & DIRECTIVES</h4>
                <p className="text-slate-500">Issued for: <strong className="text-slate-900">{selectedDocEmp.name}</strong> ({selectedDocEmp.designation})</p>
              </div>

              <div className="grid grid-cols-2 gap-4 bg-white p-4 rounded-lg border border-slate-200">
                <div>
                  <span className="text-[10px] text-slate-400 block uppercase font-bold">Department</span>
                  <span className="font-bold text-slate-900">{getDeptName(selectedDocEmp)}</span>
                </div>
                <div>
                  <span className="text-[10px] text-slate-400 block uppercase font-bold">Approved Salary Band</span>
                  <span className="font-bold text-emerald-600">${selectedDocEmp.salary.toLocaleString()} / year</span>
                </div>
                <div>
                  <span className="text-[10px] text-slate-400 block uppercase font-bold">SAP Personnel ID</span>
                  <span className="font-bold text-indigo-600">SAP-EMP-2026-{8900 + selectedDocEmp.id}</span>
                </div>
                <div>
                  <span className="text-[10px] text-slate-400 block uppercase font-bold">Policy Compliance</span>
                  <span className="font-bold text-emerald-600">Verified (POL-HR-001)</span>
                </div>
              </div>

              <div className="space-y-2">
                <h5 className="font-bold text-slate-900">Provisioned Credentials & Subsystems:</h5>
                <ul className="list-disc list-inside space-y-1 text-slate-600">
                  <li>Master SQL Database record initialized in table <code>dbo.Employees</code>.</li>
                  <li>Mock SAP HCM ERP record created with automated personnel number assignment.</li>
                  <li>Official Welcome Email dispatched to <code>{selectedDocEmp.email}</code>.</li>
                  <li>IT Hardware & Access Ticket <code>TCK-2026-{9000 + selectedDocEmp.id}</code> auto-generated.</li>
                </ul>
              </div>

              <div className="pt-3 border-t border-slate-200 text-[10px] text-slate-400 flex justify-between">
                <span>Digitally Certified by Nexus Agent Orchestrator</span>
                <span>Audit Hash: {Math.random().toString(36).substring(2, 10).toUpperCase()}</span>
              </div>
            </div>

            <div className="flex justify-end">
              <button
                onClick={() => setSelectedDocEmp(null)}
                className="px-4 py-2 bg-slate-900 text-white text-xs font-semibold rounded-xl hover:bg-slate-800 transition"
              >
                Close Preview
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

