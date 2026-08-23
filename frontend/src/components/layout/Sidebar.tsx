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
  Cpu,
  ShieldCheck,
  Activity,
  Shield
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

  const navItems = [
    { id: 'dashboard',   label: 'Dashboard',     icon: LayoutDashboard },
    { id: 'console',     label: 'AI Assistant',   icon: Bot, highlight: true },
    { id: 'employees',   label: 'Employees',      icon: Users },
    { id: 'departments', label: 'Departments',    icon: Building2 },
    { id: 'policies',    label: 'Policy Center',  icon: FileText },
    { id: 'approvals',   label: 'Approvals',      icon: CheckSquare, badge: pendingCount },
    { id: 'expenses',    label: 'Expenses',       icon: Receipt },
    { id: 'cv',          label: 'CV Checker',     icon: CheckSquare },
    { id: 'onboarding',  label: 'Onboarding',     icon: UserPlus },
    { id: 'automation',  label: 'Automation',     icon: Cpu },
    { id: 'audit',       label: 'Audit Logs',     icon: ShieldCheck },
  ];

  const isGeminiOnline = health?.isAvailable === true;


  return (
    <aside className="w-64 bg-slate-900 text-slate-300 flex flex-col h-screen sticky top-0 border-r border-slate-800 shadow-xl z-20 select-none">
      {/* Brand Header */}
      <div className="p-5 border-b border-slate-800 flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-500 flex items-center justify-center text-white font-bold text-xl shadow-md">
          N
        </div>
        <div>
          <div className="flex items-center gap-2">
            <h1 className="font-bold text-white tracking-wide text-lg">NEXUS</h1>
            <span className="text-[10px] bg-blue-500/20 text-blue-400 border border-blue-500/30 px-1.5 py-0.5 rounded font-semibold">HR</span>
          </div>
          <p className="text-[11px] text-slate-400 font-medium">Enterprise AI Assistant</p>
        </div>
      </div>

      {/* Navigation Links */}
      <nav className="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
        <div className="px-3 pb-2 text-[11px] font-semibold text-slate-500 uppercase tracking-wider">
          Operations &amp; Workflow
        </div>

        {navItems.map((item) => {
          const Icon = item.icon;
          const isActive = activeTab === item.id;

          return (
            <button
              key={item.id}
              onClick={() => setActiveTab(item.id)}
              className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition-all ${
                isActive
                  ? 'bg-blue-600 text-white shadow-sm font-semibold'
                  : 'text-slate-400 hover:bg-slate-800/80 hover:text-slate-200'
              }`}
            >
              <div className="flex items-center gap-3">
                <Icon className={`w-4 h-4 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                <span>{item.label}</span>
              </div>

              {item.badge !== undefined && item.badge > 0 && (
                <span className={`text-xs px-2 py-0.5 rounded-full font-bold ${
                  isActive ? 'bg-white text-blue-700' : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                }`}>
                  {item.badge}
                </span>
              )}
            </button>
          );
        })}
      </nav>

      {/* Footer: System Health + Role (Admin locked) */}
      <div className="p-4 border-t border-slate-800 space-y-3 bg-slate-950/40">
        {/* Gemini Health */}
        <div className="flex items-center justify-between px-3 py-2 rounded-lg bg-slate-800/50 border border-slate-800/80 text-xs">
          <div className="flex items-center gap-2">
            <Activity className="w-3.5 h-3.5 text-slate-400" />
            <span className="text-slate-300 font-medium">AI Engine</span>
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
              {health === null ? 'Checking...' : isGeminiOnline ? 'Gemini: Ready' : 'Gemini: Offline'}
            </span>
          </div>
        </div>

        {/* Active Role — Admin locked, no switcher */}
        <div>
          <label className="block text-[10px] font-semibold text-slate-500 uppercase tracking-wider mb-1 px-1">
            Active Enterprise Role
          </label>
          <div className="flex items-center gap-2 px-3 py-2 bg-slate-800/90 rounded-lg border border-slate-700/60 text-xs text-white">
            <Shield className="w-4 h-4 text-blue-400 shrink-0" />
            <div className="flex flex-col leading-none">
              <span className="font-bold text-white">Admin</span>
              <span className="text-[10px] text-slate-400 font-medium">HR Administrator</span>
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
