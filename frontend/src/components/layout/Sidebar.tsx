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
  ChevronDown,
  UserCheck,
  Activity
} from 'lucide-react';

interface SidebarProps {
  activeTab: string;
  setActiveTab: (tab: string) => void;
  userRole: string;
  setUserRole: (role: string) => void;
  pendingCount: number;
  health: { isAvailable: boolean } | null;
}

export const Sidebar: React.FC<SidebarProps> = ({
  activeTab,
  setActiveTab,
  userRole,
  setUserRole,
  pendingCount,
  health
}) => {

  const navItems = [
    { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { id: 'console', label: 'AI Assistant', icon: Bot, highlight: true },
    { id: 'employees', label: 'Employees', icon: Users },
    { id: 'departments', label: 'Departments', icon: Building2 },
    { id: 'policies', label: 'Policy Center', icon: FileText },
    { id: 'approvals', label: 'Approvals', icon: CheckSquare, badge: pendingCount },
    { id: 'expenses', label: 'Expenses', icon: Receipt },
    { id: 'onboarding', label: 'Onboarding', icon: UserPlus },
    { id: 'automation', label: 'Automation', icon: Cpu },
    { id: 'audit', label: 'Audit Logs', icon: ShieldCheck },
  ];

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
          Operations & Workflow
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

      {/* Footer System Health & User Role */}
      <div className="p-4 border-t border-slate-800 space-y-3 bg-slate-950/40">
        {/* System Health */}
        <div className="flex items-center justify-between px-3 py-2 rounded-lg bg-slate-800/50 border border-slate-800/80 text-xs">
          <div className="flex items-center gap-2">
            <Activity className="w-3.5 h-3.5 text-slate-400" />
            <span className="text-slate-300 font-medium">AI Engine</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className={`w-2 h-2 rounded-full ${health?.isAvailable ? 'bg-emerald-500 shadow-emerald-500/50 shadow-sm' : 'bg-amber-400'}`} />
            <span className={`font-semibold ${health?.isAvailable ? 'text-emerald-400' : 'text-amber-400'}`}>
              {health?.isAvailable ? 'Ready' : 'Fallback'}
            </span>
          </div>
        </div>

        {/* User Role Switcher */}
        <div className="relative">
          <label className="block text-[10px] font-semibold text-slate-500 uppercase tracking-wider mb-1 px-1">
            Active Enterprise Role
          </label>
          <div className="flex items-center justify-between px-3 py-2 bg-slate-800/90 rounded-lg border border-slate-700/60 text-xs text-white">
            <div className="flex items-center gap-2">
              <UserCheck className="w-4 h-4 text-blue-400" />
              <span className="font-semibold">{userRole}</span>
            </div>
            <select
              value={userRole}
              onChange={(e) => setUserRole(e.target.value)}
              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
            >
              <option value="Admin">HR Administrator</option>
              <option value="HR Manager">HR Manager</option>
              <option value="Department Head">Department Head</option>
              <option value="Employee">Employee Access</option>
            </select>
            <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
          </div>
        </div>
      </div>
    </aside>
  );
};
