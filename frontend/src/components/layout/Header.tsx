import React, { useState } from 'react';
import { Search, Bell, Shield, CheckCircle2, AlertTriangle } from 'lucide-react';

interface HeaderProps {
  activeTab: string;
  userRole: string;
  pendingCount: number;
  onNavigateToApprovals: () => void;
}

export const Header: React.FC<HeaderProps> = ({
  activeTab,
  userRole,
  pendingCount,
  onNavigateToApprovals
}) => {
  const [showNotifications, setShowNotifications] = useState(false);

  const getTabTitle = (tab: string) => {
    switch (tab) {
      case 'dashboard':
        return { title: 'Executive HR Dashboard', desc: 'Workforce overview, pending approvals, and operational health metrics.' };
      case 'console':
        return { title: 'Nexus AI Assistant', desc: 'Ask Nexus to analyze workforce data, manage employee profiles, or execute HR actions.' };
      case 'employees':
        return { title: 'Employee Directory', desc: 'Manage workforce profiles, designations, compensation bands, and status.' };
      case 'departments':
        return { title: 'Department Operations', desc: 'Department headcounts, budget allocations, and organizational structure.' };
      case 'policies':
        return { title: 'Policy Center', desc: 'Corporate handbook guidelines, salary bands, and expense compliance limits.' };
      case 'approvals':
        return { title: 'HR Approval Center', desc: 'Review, authorize, or decline proposed workforce actions.' };
      case 'expenses':
        return { title: 'Expense Review & Compliance', desc: 'Review employee expense claims against corporate policy limits.' };
      case 'tickets':
        return { title: 'Workplace Service Desk', desc: 'Report workplace or technical issues and track resolution progress.' };
      case 'onboarding':
        return { title: 'Employee Onboarding Hub', desc: 'Automated employee provisioning, documents, and welcome communications.' };
      case 'automation':
        return { title: 'Automated Workflows', desc: 'Catalog of automated business processes managed by Nexus AI Assistant.' };
      case 'audit':
        return { title: 'Enterprise Activity History', desc: 'Verified record of all workforce management actions.' };
      case 'cv':
        return { title: 'Candidate CV Screening', desc: 'AI-assisted resume screening and candidate qualification evaluation.' };
      default:
        return { title: 'NEXUS HR Platform', desc: 'Enterprise workforce management.' };
    }
  };

  const { title, desc } = getTabTitle(activeTab);

  return (
    <header className="bg-white border-b border-slate-200 sticky top-0 z-10 px-8 py-4 flex items-center justify-between shadow-2xs select-none">
      {/* Contextual Title */}
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">{title}</h2>
        <p className="text-xs text-slate-500 font-medium mt-0.5">{desc}</p>
      </div>

      {/* Right Controls */}
      <div className="flex items-center gap-5">
        {/* Search Bar */}
        <div className="relative hidden md:block">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            placeholder="Search employees, policies..."
            className="pl-9 pr-4 py-1.5 bg-slate-100/80 border border-slate-200 rounded-lg text-xs text-slate-800 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 focus:border-blue-500 transition-all w-64"
          />
        </div>

        {/* Notifications Dropdown */}
        <div className="relative">
          <button
            onClick={() => setShowNotifications(!showNotifications)}
            className="p-2 text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition-colors relative"
          >
            <Bell className="w-5 h-5" />
            {pendingCount > 0 && (
              <span className="absolute top-1.5 right-1.5 w-2.5 h-2.5 bg-amber-500 border-2 border-white rounded-full" />
            )}
          </button>

          {showNotifications && (
            <div className="absolute right-0 mt-2 w-80 bg-white rounded-xl shadow-xl border border-slate-200 p-3 z-50 animate-in fade-in slide-in-from-top-2 duration-150">
              <div className="flex items-center justify-between border-b border-slate-100 pb-2 mb-2 px-1">
                <span className="text-xs font-bold text-slate-800">Notifications</span>
                <span className="text-[10px] text-blue-600 font-semibold cursor-pointer hover:underline">Mark all read</span>
              </div>

              <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
                {pendingCount > 0 ? (
                  <div
                    onClick={() => {
                      onNavigateToApprovals();
                      setShowNotifications(false);
                    }}
                    className="p-2.5 bg-amber-50 border border-amber-200 rounded-lg flex items-start gap-2.5 cursor-pointer hover:bg-amber-100/60 transition-colors"
                  >
                    <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
                    <div>
                      <div className="text-xs font-bold text-amber-900">Action Plan Approval Required</div>
                      <div className="text-[11px] text-amber-700 mt-0.5">{pendingCount} high-risk action plan(s) waiting for HR review.</div>
                    </div>
                  </div>
                ) : (
                  <div className="p-3 text-center text-xs text-slate-400">No pending action items</div>
                )}

                <div className="p-2.5 bg-slate-50 border border-slate-150 rounded-lg flex items-start gap-2.5">
                  <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" />
                  <div>
                    <div className="text-xs font-semibold text-slate-800">Policy Sync Verified</div>
                    <div className="text-[11px] text-slate-500 mt-0.5">HR Compensation Policy POL-HR-001 active.</div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Divider */}
        <div className="h-6 w-px bg-slate-200" />

        {/* User Profile */}
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-slate-900 text-white flex items-center justify-center font-bold text-xs shadow-xs">
            HR
          </div>
          <div className="hidden lg:block text-left">
            <div className="text-xs font-bold text-slate-800 leading-tight">Sarah Ahmed</div>
            <div className="flex items-center gap-1 mt-0.5">
              <Shield className="w-3 h-3 text-blue-600" />
              <span className="text-[10px] font-semibold text-slate-500 uppercase tracking-wide">{userRole}</span>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};
