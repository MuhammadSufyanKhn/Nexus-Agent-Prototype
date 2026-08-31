import React from 'react';
import {
  LayoutDashboard,
  Bot,
  Users,
  Building2,
  FileText,
  CheckSquare,
  Receipt,
  UserPlus,
  ShieldCheck,
  Activity,
  Shield,
  Ticket,
  Sparkles,
  Briefcase
} from 'lucide-react';

interface SidebarProps {
  activeTab: string;
  setActiveTab: (tab: string) => void;
  userRole: string;
  setUserRole: (role: string) => void;
  pendingCount: number;
  health: { isAvailable: boolean; provider?: string; modelName?: string } | null;
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeTab,
  setActiveTab,
  pendingCount,
  health
}) => {

  const coreNav = [
    { id: 'dashboard',   label: 'Dashboard',          icon: LayoutDashboard },
    { id: 'console',     label: 'AI Assistant',        icon: Bot, highlight: true },
    { id: 'employees',   label: 'Employee Directory',  icon: Users },
    { id: 'departments', label: 'Departments',         icon: Building2 },
  ];

  const opsNav = [
    { id: 'jobs',        label: 'Job Openings',        icon: Briefcase },
    { id: 'cv',          label: 'CV Screening',        icon: Sparkles },
    { id: 'policies',    label: 'Policy Center',       icon: FileText },
    { id: 'tickets',     label: 'Service Desk',        icon: Ticket },
    { id: 'approvals',   label: 'HR Approvals',        icon: CheckSquare, badge: pendingCount },
    { id: 'expenses',    label: 'Expense Review',      icon: Receipt },
    { id: 'onboarding',  label: 'Onboarding Hub',      icon: UserPlus },
  ];

  const govNav = [
    { id: 'audit',       label: 'Activity History',    icon: ShieldCheck },
  ];

  const isGeminiOnline = health?.isAvailable === true;

  return (
    <aside className="w-64 bg-slate-900 text-slate-300 flex flex-col h-screen sticky top-0 border-r border-slate-800 shadow-xl z-20 select-none">
      {/* Brand Header */}
      <div className="p-5 border-b border-slate-800 flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-600 via-indigo-600 to-cyan-500 flex items-center justify-center text-white font-black text-xl shadow-lg shadow-blue-500/20">
          N
        </div>
        <div>
          <div className="flex items-center gap-1.5">
            <h1 className="font-extrabold text-white tracking-wider text-base">NEXUS</h1>
            <span className="text-[10px] bg-blue-500/20 text-blue-400 border border-blue-500/30 px-1.5 py-0.5 rounded font-bold font-mono">HR</span>
          </div>
          <p className="text-[10px] text-slate-400 font-medium tracking-wide">Enterprise Workforce AI</p>
        </div>
      </div>

      {/* Navigation Links */}
      <nav className="flex-1 px-3 py-4 space-y-4 overflow-y-auto">
        {/* Core Section */}
        <div className="space-y-1">
          <div className="px-3 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
            Main Operations
          </div>
          {coreNav.map((item) => {
            const Icon = item.icon;
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveTab(item.id)}
                className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-semibold transition-all ${
                  isActive
                    ? 'bg-blue-600 text-white shadow-sm font-bold'
                    : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-200'
                }`}
              >
                <div className="flex items-center gap-2.5">
                  <Icon className={`w-4 h-4 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                  <span>{item.label}</span>
                </div>
              </button>
            );
          })}
        </div>

        {/* Workforce Services Section */}
        <div className="space-y-1">
          <div className="px-3 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
            Workforce Services
          </div>
          {opsNav.map((item) => {
            const Icon = item.icon;
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveTab(item.id)}
                className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-semibold transition-all ${
                  isActive
                    ? 'bg-blue-600 text-white shadow-sm font-bold'
                    : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-200'
                }`}
              >
                <div className="flex items-center gap-2.5">
                  <Icon className={`w-4 h-4 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                  <span>{item.label}</span>
                </div>

                {item.badge !== undefined && item.badge > 0 && (
                  <span className={`text-[10px] px-2 py-0.5 rounded-full font-bold ${
                    isActive ? 'bg-white text-blue-700' : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                  }`}>
                    {item.badge}
                  </span>
                )}
              </button>
            );
          })}
        </div>

        {/* Governance & Automation Section */}
        <div className="space-y-1">
          <div className="px-3 text-[10px] font-bold text-slate-500 uppercase tracking-wider">
            Governance &amp; Intelligence
          </div>
          {govNav.map((item) => {
            const Icon = item.icon;
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                onClick={() => setActiveTab(item.id)}
                className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-semibold transition-all ${
                  isActive
                    ? 'bg-blue-600 text-white shadow-sm font-bold'
                    : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-200'
                }`}
              >
                <div className="flex items-center gap-2.5">
                  <Icon className={`w-4 h-4 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                  <span>{item.label}</span>
                </div>
              </button>
            );
          })}
        </div>
      </nav>

      {/* Footer: System Health + Role */}
      <div className="p-4 border-t border-slate-800 space-y-3 bg-slate-950/40">
        {/* Nexus AI Health */}
        <div className="flex items-center justify-between px-3 py-2 rounded-lg bg-slate-800/50 border border-slate-800/80 text-xs">
          <div className="flex items-center gap-2">
            <Activity className="w-3.5 h-3.5 text-slate-400" />
            <span className="text-slate-300 font-medium">AI Copilot</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className={`w-2 h-2 rounded-full ${
              isGeminiOnline
                ? 'bg-emerald-500 shadow-emerald-500/50 shadow-sm'
                : health === null
                  ? 'bg-slate-500 animate-pulse'
                  : 'bg-rose-400'
            }`} />
            <span className={`font-semibold text-[10px] ${
              isGeminiOnline ? 'text-emerald-400' : health === null ? 'text-slate-400' : 'text-rose-400'
            }`}>
              {health === null ? 'Checking...' : isGeminiOnline ? 'Active & Ready' : 'Offline'}
            </span>
          </div>
        </div>

        {/* Active Role */}
        <div>
          <label className="block text-[10px] font-semibold text-slate-500 uppercase tracking-wider mb-1 px-1">
            Current Session Role
          </label>
          <div className="flex items-center gap-2 px-3 py-2 bg-slate-800/90 rounded-lg border border-slate-700/60 text-xs text-white">
            <Shield className="w-4 h-4 text-blue-400 shrink-0" />
            <div className="flex flex-col leading-none">
              <span className="font-bold text-white">Executive HR</span>
              <span className="text-[10px] text-slate-400 font-medium">Administrator</span>
            </div>
            <span className="ml-auto text-[9px] bg-blue-500/20 text-blue-400 border border-blue-500/30 px-1.5 py-0.5 rounded font-bold uppercase tracking-wide">
              Full Access
            </span>
          </div>
        </div>
      </div>
    </aside>
  );
};

