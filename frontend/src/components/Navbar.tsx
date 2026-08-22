import React from 'react';
import { Shield, Cpu, Activity, Database, CheckSquare, FileText, Lock } from 'lucide-react';
import type { LLMHealthStatus } from '../services/api';

interface NavbarProps {
  activeTab: string;
  setActiveTab: (tab: string) => void;
  userRole: string;
  setUserRole: (role: string) => void;
  pendingCount: number;
  health: LLMHealthStatus | null;
}

export const Navbar: React.FC<NavbarProps> = ({
  activeTab,
  setActiveTab,
  userRole,
  setUserRole,
  pendingCount,
  health
}) => {
  const tabs = [
    { id: 'console', label: 'Agent Console', icon: Cpu },
    { id: 'dashboard', label: 'Dashboard', icon: Activity },
    { id: 'employees', label: 'Employees', icon: Database },
    { id: 'policies', label: 'Policies', icon: FileText },
    { id: 'approvals', label: 'Approvals', icon: CheckSquare, badge: pendingCount },
    { id: 'audit', label: 'Audit Logs', icon: Lock }
  ];

  return (
    <header className="bg-slate-900/90 backdrop-blur border-b border-slate-800 sticky top-0 z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo & Tagline */}
          <div className="flex items-center space-x-3 cursor-pointer" onClick={() => setActiveTab('console')}>
            <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-600 flex items-center justify-center shadow-lg shadow-cyan-500/20 ring-1 ring-cyan-400/30">
              <Shield className="w-6 h-6 text-white" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="font-extrabold text-xl tracking-wider text-transparent bg-clip-text bg-gradient-to-r from-white via-slate-200 to-cyan-400">
                  NEXUS
                </span>
                <span className="text-xs px-2 py-0.5 rounded bg-cyan-950/80 border border-cyan-800/60 text-cyan-400 font-mono">
                  AGENT LITE
                </span>
              </div>
              <p className="text-[10px] text-slate-400 tracking-wider font-semibold uppercase">
                FROM INTENT TO ACTION — SECURELY
              </p>
            </div>
          </div>

          {/* Navigation Tabs */}
          <nav className="hidden md:flex space-x-1">
            {tabs.map((tab) => {
              const Icon = tab.icon;
              const isActive = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  onClick={() => setActiveTab(tab.id)}
                  className={`flex items-center space-x-2 px-3.5 py-2 rounded-lg text-sm font-medium transition-all ${
                    isActive
                      ? 'bg-slate-800 text-cyan-400 border border-slate-700 shadow-inner'
                      : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
                  }`}
                >
                  <Icon className={`w-4 h-4 ${isActive ? 'text-cyan-400' : 'text-slate-400'}`} />
                  <span>{tab.label}</span>
                  {tab.badge !== undefined && tab.badge > 0 && (
                    <span className="px-1.5 py-0.5 text-xs font-bold rounded-full bg-amber-500 text-slate-950 ring-2 ring-slate-900 animate-pulse">
                      {tab.badge}
                    </span>
                  )}
                </button>
              );
            })}
          </nav>

          {/* Status & Role Controls */}
          <div className="flex items-center space-x-4">
            {/* LLM Health Badge */}
            <div className="hidden lg:flex items-center space-x-2 px-3 py-1.5 rounded-lg bg-slate-950/60 border border-slate-800 text-xs font-mono">
              <span className={`w-2 h-2 rounded-full ${health?.isAvailable ? 'bg-emerald-400 shadow-sm shadow-emerald-400' : 'bg-amber-500'}`} />
              <span className="text-slate-300">
                {health?.isAvailable ? `Ollama (${health.modelName})` : 'Offline Fallback'}
              </span>
            </div>

            {/* Role Switcher */}
            <div className="flex items-center space-x-2 bg-slate-950/80 p-1 rounded-lg border border-slate-800">
              <span className="text-xs text-slate-400 px-2 font-mono">Role:</span>
              <select
                value={userRole}
                onChange={(e) => setUserRole(e.target.value)}
                className="bg-slate-900 text-xs text-cyan-400 font-semibold rounded px-2.5 py-1 border border-slate-700 focus:outline-none focus:ring-1 focus:ring-cyan-500 cursor-pointer"
              >
                <option value="Admin">Admin (Full Access)</option>
                <option value="HRManager">HR Manager</option>
                <option value="Employee">Employee (Read Only)</option>
                <option value="UnauthorizedUser">Unauthorized User</option>
              </select>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};
