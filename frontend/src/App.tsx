import { useEffect, useState } from 'react';
import { Sidebar } from './components/layout/Sidebar';
import { Header } from './components/layout/Header';
import { AgentConsole } from './components/AgentConsole';
import { DashboardView } from './components/DashboardView';
import { EmployeesView } from './components/EmployeesView';
import { DepartmentsView } from './components/DepartmentsView';
import { PoliciesView } from './components/PoliciesView';
import { ExpensesView } from './components/ExpensesView';
import { ApprovalsView } from './components/ApprovalsView';
import { OnboardingView } from './components/OnboardingView';
import { AutomationView } from './components/AutomationView';
import { AuditLogsView } from './components/AuditLogsView';
import { fetchPendingApprovals, checkLLMHealth } from './services/api';
import type { LLMHealthStatus } from './services/api';

export function App() {
  const [activeTab, setActiveTab] = useState('console');
  const [userRole, setUserRole] = useState('Admin');
  const [pendingCount, setPendingCount] = useState(0);
  const [health, setHealth] = useState<LLMHealthStatus | null>(null);

  const refreshGlobalState = async () => {
    try {
      const approvals = await fetchPendingApprovals();
      setPendingCount(approvals.length);
      const h = await checkLLMHealth();
      setHealth(h);
    } catch (err) {
      console.error('Global state sync error:', err);
    }
  };

  useEffect(() => {
    refreshGlobalState();
    const interval = setInterval(refreshGlobalState, 10000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="flex min-h-screen bg-slate-50 text-slate-900 font-sans antialiased">
      {/* Persistent Left Sidebar */}
      <Sidebar
        activeTab={activeTab}
        setActiveTab={setActiveTab}
        userRole={userRole}
        setUserRole={setUserRole}
        pendingCount={pendingCount}
        health={health}
      />

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top Header */}
        <Header
          activeTab={activeTab}
          userRole={userRole}
          pendingCount={pendingCount}
          onNavigateToApprovals={() => setActiveTab('approvals')}
        />

        {/* Page Content */}
        <main className="flex-1 pb-16">
          {activeTab === 'console' && (
            <AgentConsole userRole={userRole} onApprovalStateChange={refreshGlobalState} />
          )}
          {activeTab === 'dashboard' && (
            <DashboardView
              onNavigateToApprovals={() => setActiveTab('approvals')}
              onNavigateToConsole={() => setActiveTab('console')}
            />
          )}
          {activeTab === 'employees' && <EmployeesView />}
          {activeTab === 'departments' && <DepartmentsView />}
          {activeTab === 'policies' && <PoliciesView />}
          {activeTab === 'expenses' && <ExpensesView />}
          {activeTab === 'approvals' && (
            <ApprovalsView userRole={userRole} onApprovalChanged={refreshGlobalState} />
          )}
          {activeTab === 'onboarding' && <OnboardingView />}
          {activeTab === 'automation' && <AutomationView />}
          {activeTab === 'audit' && <AuditLogsView />}
        </main>
      </div>
    </div>
  );
}

export default App;
