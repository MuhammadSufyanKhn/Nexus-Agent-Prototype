import React, { useEffect, useState } from 'react';
import { Cpu, CheckCircle2, Play, RefreshCw, ShieldCheck, X, Activity, Layers, Terminal, UserPlus, Receipt, Ticket, Building2 } from 'lucide-react';
import { fetchWorkflows, executeWorkflow, fetchSubsystems, fetchAutomationHistory } from '../services/api';
import type { WorkflowDefinition, SubsystemStatus, AutomationHistoryItem, AgentResult } from '../services/api';

export const AutomationView: React.FC = () => {
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([]);
  const [subsystems, setSubsystems] = useState<SubsystemStatus[]>([]);
  const [history, setHistory] = useState<AutomationHistoryItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Workflow Execution State
  const [executingWfId, setExecutingWfId] = useState<string | null>(null);
  const [activeResult, setActiveResult] = useState<AgentResult | null>(null);

  const loadData = async () => {
    setLoading(true);
    try {
      const [wfData, subData, histData] = await Promise.all([
        fetchWorkflows(),
        fetchSubsystems(),
        fetchAutomationHistory()
      ]);
      setWorkflows(wfData);
      setSubsystems(subData);
      setHistory(histData);
    } catch (err) {
      console.error("Automation fetch error:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleRunWorkflow = async (wf: WorkflowDefinition) => {
    setExecutingWfId(wf.id);
    try {
      const res = await executeWorkflow(wf.id, wf.defaultPrompt, 'Admin');
      setActiveResult(res);
      await loadData();
    } catch (err) {
      console.error("Failed to execute workflow:", err);
    } finally {
      setExecutingWfId(null);
    }
  };

  const getWfIcon = (id: string) => {
    switch (id) {
      case 'wf-onboarding': return UserPlus;
      case 'wf-expense-audit': return Receipt;
      case 'wf-it-provisioning': return Ticket;
      case 'wf-budget-reallocate': return Building2;
      default: return Cpu;
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-6 py-6 space-y-6">
      {/* Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-gradient-to-r from-slate-900 via-slate-950 to-slate-900 p-6 rounded-2xl border border-slate-800 text-white shadow-xl">
        <div className="flex items-center gap-4">
          <div className="p-3 bg-cyan-600/20 border border-cyan-500/30 rounded-xl text-cyan-400 backdrop-blur-md">
            <Cpu className="w-7 h-7" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-bold tracking-tight">Automated Business Workflows</h2>
              <span className="text-[10px] bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 font-mono font-bold px-2 py-0.5 rounded">
                AI AUTOMATION ACTIVE
              </span>
            </div>
            <p className="text-xs text-slate-300 mt-1">
              Trigger, monitor, and manage automated workforce processes executed seamlessly by Nexus AI Assistant.
            </p>
          </div>
        </div>

        <button
          onClick={() => loadData()}
          className="p-2.5 bg-white/10 hover:bg-white/20 text-white rounded-xl transition text-xs font-semibold flex items-center gap-2 backdrop-blur-md self-start md:self-auto cursor-pointer"
          title="Refresh automation state"
        >
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          <span>Refresh Workflows</span>
        </button>
      </div>

      {/* Execution Feedback Result Banner if available */}
      {activeResult && (
        <div className="bg-slate-900 text-white p-6 rounded-2xl border border-cyan-900/60 shadow-2xl space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center gap-2">
              <ShieldCheck className="w-5 h-5 text-cyan-400" />
              <span className="font-bold text-sm text-cyan-400">Workflow Execution Result</span>
              <span className="text-xs text-slate-400 font-mono">Run ID: {activeResult.runId?.substring(0, 8)}</span>
            </div>
            <button onClick={() => setActiveResult(null)} className="text-slate-400 hover:text-white">
              <X className="w-4 h-4" />
            </button>
          </div>

          <p className="text-xs text-slate-300 leading-relaxed font-medium">
            {activeResult.userMessage || activeResult.resultData?.message || 'Workflow pipeline executed successfully across all target systems.'}
          </p>

          {activeResult.executionFeed && activeResult.executionFeed.length > 0 && (
            <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 text-xs font-mono text-slate-300 max-h-40 overflow-y-auto space-y-1.5">
              <span className="text-[10px] text-slate-500 uppercase tracking-wider font-bold block mb-1">Execution Event Stream:</span>
              {activeResult.executionFeed.map((ev, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span className="text-cyan-400">►</span>
                  <span>{ev.message}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Section 1: Runnable Enterprise Workflows Catalog */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
            <Layers className="w-4 h-4 text-cyan-600" />
            <span>Runnable Enterprise Automated Workflows ({workflows.length})</span>
          </div>
          <span className="text-xs text-slate-400">Trigger end-to-end automated pipelines with 1 click</span>
        </div>

        {loading ? (
          <div className="bg-white p-12 rounded-2xl border border-slate-200 text-center text-xs text-slate-400 flex items-center justify-center gap-2">
            <RefreshCw className="w-4 h-4 animate-spin text-cyan-600" />
            <span>Loading workflows catalog...</span>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {workflows.map((wf) => {
              const IconComp = getWfIcon(wf.id);
              const isRunning = executingWfId === wf.id;

              return (
                <div key={wf.id} className="bg-white rounded-2xl border border-slate-200/80 p-5 shadow-xs hover:shadow-md transition flex flex-col justify-between space-y-4">
                  <div className="space-y-3">
                    <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                      <div className="flex items-center gap-3">
                        <div className="p-2.5 bg-slate-900 text-cyan-400 rounded-xl">
                          <IconComp className="w-5 h-5" />
                        </div>
                        <div>
                          <h4 className="font-bold text-slate-900 text-sm">{wf.title}</h4>
                          <span className="text-[10px] font-bold text-slate-500 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                            {wf.subsystem}
                          </span>
                        </div>
                      </div>
                    </div>

                    <p className="text-xs text-slate-600 leading-relaxed font-normal">{wf.description}</p>

                    {/* Steps breakdown */}
                    <div className="flex flex-wrap gap-1.5 pt-1">
                      {wf.steps.map((st, i) => (
                        <span key={i} className="text-[10px] bg-slate-50 text-slate-600 border border-slate-200 px-2 py-0.5 rounded font-medium">
                          {i + 1}. {st}
                        </span>
                      ))}
                    </div>
                  </div>

                  <div className="pt-2 border-t border-slate-100 flex items-center justify-between">
                    <span className="text-[10px] text-slate-400 font-mono">Gemini Intent Orchestrated</span>
                    <button
                      onClick={() => handleRunWorkflow(wf)}
                      disabled={isRunning}
                      className="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white font-bold rounded-xl transition text-xs flex items-center gap-2 disabled:opacity-50 shadow-sm"
                    >
                      {isRunning ? (
                        <>
                          <RefreshCw className="w-3.5 h-3.5 animate-spin text-cyan-400" />
                          <span>Executing Pipeline...</span>
                        </>
                      ) : (
                        <>
                          <Play className="w-3.5 h-3.5 text-cyan-400 fill-cyan-400" />
                          <span>Execute Workflow</span>
                        </>
                      )}
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Section 2: Connected Subsystems & Integrations Readiness */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
            <Activity className="w-4 h-4 text-emerald-600" />
            <span>Connected Subsystems & Connectors Readiness</span>
          </div>
          <span className="text-xs text-slate-400">Live operational status</span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {subsystems.map((sub, idx) => (
            <div key={idx} className="bg-white rounded-2xl border border-slate-200/80 p-4 shadow-xs space-y-2">
              <div className="flex items-center justify-between border-b border-slate-100 pb-2">
                <div>
                  <h5 className="font-bold text-slate-900 text-xs">{sub.name}</h5>
                  <span className="text-[10px] text-slate-500 font-mono">{sub.type}</span>
                </div>
                <span className="text-[10px] bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-2 py-0.5 rounded flex items-center gap-1">
                  <CheckCircle2 className="w-3 h-3 text-emerald-600" />
                  <span>ONLINE</span>
                </span>
              </div>
              <p className="text-[11px] text-slate-500 leading-tight">{sub.metrics}</p>
              <div className="text-[10px] text-slate-400 font-mono truncate">Target: {sub.target}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Section 3: Recent Automation Execution Runs History */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
            <Terminal className="w-4 h-4 text-indigo-600" />
            <span>Recent Automation Execution Log Stream ({history.length})</span>
          </div>
          <span className="text-xs text-slate-400">Cryptographically signed execution records</span>
        </div>

        <div className="bg-white rounded-2xl border border-slate-200/80 shadow-xs overflow-hidden">
          {history.length === 0 ? (
            <div className="p-8 text-center text-xs text-slate-400">No automation runs recorded yet. Execute a workflow above.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50/80 border-b border-slate-100 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                  <tr>
                    <th className="py-3 px-4">Run ID</th>
                    <th className="py-3 px-4">Original Prompt / Trigger</th>
                    <th className="py-3 px-4">Parsed Intent</th>
                    <th className="py-3 px-4">Status</th>
                    <th className="py-3 px-4">Timestamp</th>
                    <th className="py-3 px-4">Audit Logs</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {history.map((h) => (
                    <tr key={h.runId} className="hover:bg-slate-50/60 transition font-mono">
                      <td className="py-3 px-4 font-bold text-cyan-600">{h.runId.substring(0, 8)}</td>
                      <td className="py-3 px-4 font-sans text-slate-800 max-w-[240px] truncate" title={h.originalPrompt}>
                        {h.originalPrompt}
                      </td>
                      <td className="py-3 px-4 text-slate-600">
                        <span className="px-2 py-0.5 bg-slate-100 text-slate-800 rounded font-semibold text-[10px]">
                          {h.intent}
                        </span>
                      </td>
                      <td className="py-3 px-4">
                        <span className="text-[10px] px-2 py-0.5 rounded font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                          {h.status}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-slate-400 text-[10px]">
                        {new Date(h.startedAt).toLocaleTimeString()}
                      </td>
                      <td className="py-3 px-4 text-slate-500 text-[10px]">
                        {h.auditLogs?.length > 0 ? `${h.auditLogs.length} blocks` : '1 block logged'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};