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
import { AuditLogsView } from './components/AuditLogsView';
import { CvCheckerView } from './components/CvCheckerView';
import { JobOpeningsView } from './components/JobOpeningsView';
import { CandidateApplicationPortal } from './components/CandidateApplicationPortal';
import { WelcomeModal } from './components/WelcomeModal';
import { InstructionsView } from './components/InstructionsView';
import { fetchPendingApprovals, checkLLMHealth } from './services/api';

import type { LLMHealthStatus } from './services/api';

export function App() {
  const [activeTab, setActiveTab] = useState(() => localStorage.getItem('nexus_active_tab') || 'console');
  const [userRole, setUserRole] = useState('Admin');
  const [pendingCount, setPendingCount] = useState(0);
  const [health, setHealth] = useState<LLMHealthStatus | null>(null);
  const [isWelcomeOpen, setIsWelcomeOpen] = useState(true);
  const [prefillCommand, setPrefillCommand] = useState<string | undefined>(undefined);

  // Candidate Portal standalone routing
  const [candidatePortalJobId, setCandidatePortalJobId] = useState<number | null>(() => {
    const params = new URLSearchParams(window.location.search);
    const jobId = params.get('jobId');
    if (jobId) return Number(jobId);
    if (window.location.hash.startsWith('#/apply/')) {
      const parts = window.location.hash.split('/');
      if (parts[2]) return Number(parts[2]);
    }
    return null;
  });

  const [isCandidatePortal, setIsCandidatePortal] = useState<boolean>(() => {
    const params = new URLSearchParams(window.location.search);
    return window.location.port === '3001' || params.get('portal') === 'candidate' || window.location.hash.startsWith('#/apply');
  });

  // Cross-view selection for CV screening
  const [cvJobId, setCvJobId] = useState<number | undefined>(undefined);
  const [cvCandidateId, setCvCandidateId] = useState<number | undefined>(undefined);

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
    localStorage.setItem('nexus_active_tab', activeTab);
  }, [activeTab]);

  useEffect(() => {
    refreshGlobalState();
    const interval = setInterval(refreshGlobalState, 30000);
    return () => clearInterval(interval);
  }, []);

  const [, setNavContext] = useState<any>(null);

  // Called by AgentConsole after a successful AI action to route user to the correct module
  const handleNavigate = (tab: string, context?: any) => {
    setActiveTab(tab);
    if (context) {
      setNavContext(context);
      if (context.highlightName) {
        window.dispatchEvent(new CustomEvent('filter-employee', { detail: context.highlightName }));
      }
      if (context.policyCode) {
        window.dispatchEvent(new CustomEvent('filter-policy', { detail: context.policyCode }));
      }
    }
  };

  // If viewing standalone candidate application portal
  if (isCandidatePortal) {
    return (
      <CandidateApplicationPortal
        initialJobId={candidatePortalJobId || undefined}
        onBackToPortal={() => {
          setIsCandidatePortal(false);
          setCandidatePortalJobId(null);
          window.history.pushState({}, '', window.location.pathname);
        }}
      />
    );
  }

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

        {/* Welcome Pop-up Modal */}
        <WelcomeModal
          isOpen={isWelcomeOpen}
          onClose={() => setIsWelcomeOpen(false)}
          onGoToInstructions={() => setActiveTab('instructions')}
        />

        {/* Page Content */}
        <main className="flex-1 pb-16">
          {activeTab === 'console' && (
            <AgentConsole
              userRole={userRole}
              onApprovalStateChange={refreshGlobalState}
              onNavigate={handleNavigate}
              prefillCommand={prefillCommand}
            />
          )}
          {activeTab === 'instructions' && (
            <InstructionsView
              onNavigateToConsole={(cmd) => {
                if (cmd) {
                  setPrefillCommand(cmd);
                }
                setActiveTab('console');
              }}
            />
          )}
          {activeTab === 'dashboard' && (
            <DashboardView
              onNavigateToApprovals={() => setActiveTab('approvals')}
              onNavigateToConsole={() => setActiveTab('console')}
            />
          )}
          {activeTab === 'employees' && <EmployeesView />}
          {activeTab === 'departments' && <DepartmentsView />}
          {activeTab === 'jobs' && (
            <JobOpeningsView
              onScreenCandidate={(jobId, candidateId) => {
                setCvJobId(jobId);
                setCvCandidateId(candidateId);
                setActiveTab('cv');
              }}
              onOpenCandidatePortal={(jobId) => {
                setCandidatePortalJobId(jobId);
                setIsCandidatePortal(true);
              }}
            />
          )}
          {activeTab === 'cv' && (
            <CvCheckerView 
              initialJobId={cvJobId}
              initialCandidateId={cvCandidateId}
            />
          )}
          {activeTab === 'policies' && <PoliciesView />}
          {activeTab === 'expenses' && <ExpensesView />}

          {activeTab === 'approvals' && (
            <ApprovalsView userRole={userRole} onApprovalChanged={refreshGlobalState} />
          )}
          {activeTab === 'onboarding' && <OnboardingView />}
          {activeTab === 'audit' && <AuditLogsView />}
        </main>
      </div>
    </div>
  );
}

export default App;
