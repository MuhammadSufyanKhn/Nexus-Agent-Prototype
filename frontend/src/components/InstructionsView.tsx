import React, { useState } from 'react';
import {
  BookOpen,
  Bot,
  Receipt,
  Briefcase,
  Building2,
  Users,
  Terminal,
  Copy,
  Check,
  ArrowRight,
  Zap,
  Info,
  UserPlus,
  Search
} from 'lucide-react';


interface InstructionsViewProps {
  onNavigateToConsole?: (prefillCommand?: string) => void;
}

export const InstructionsView: React.FC<InstructionsViewProps> = ({ onNavigateToConsole }) => {
  const [copiedId, setCopiedId] = useState<string | null>(null);
  const [activeTabSection, setActiveTabSection] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 2000);
  };

  const handleTryInConsole = (cmdText: string) => {
    if (onNavigateToConsole) {
      onNavigateToConsole(cmdText);
    }
  };

  const topicSections = [
    {
      id: 'console',
      title: '1. AI Assistant & Natural Language Console',
      icon: Bot,
      color: 'from-blue-600 to-indigo-600',
      borderColor: 'border-blue-500/30',
      badge: 'CORE ENGINE',
      howItWorks: [
        'Type natural language commands in the prompt box (e.g. "onboard", "allocate", "show policy").',
        'Ghost text suggestions automatically appear inline right next to your cursor matching available templates.',
        'Press TAB to auto-complete the suggestion, or use Arrow keys to navigate suggestions.',
        'Upon execution, the Nexus AI Engine parses intent, evaluates policy compliance, and executes multi-step automation.',
        'Automatic routing redirects you to the relevant module tab (e.g. Expenses, Approvals, Onboarding) with real-time feedback.'
      ],
      commands: [
        {
          id: 'c1',
          text: 'Onboard Sarah Jenkins as Senior Software Engineer in Engineering with a salary of $120,000. and send welcome email at [your email]',
          desc: 'Triggers full onboarding workflow, salary band validation, and credentials setup.'
        },
        {
          id: 'c2',
          text: 'Allocate $150,000 budget to the IT department for Q3.',
          desc: 'Increases quarterly department budget pool from master allocation.'
        },
        {
          id: 'c3',
          text: 'Increase IT budget by [your amount] for Q3.',
          desc: 'Transfers budget allocation between department nodes seamlessly.'
        },
        {
          id: 'c4',
          text: 'Log Marcus\'s sick day today and notify his team on Slack.',
          desc: 'Records leave entry in attendance log and triggers team notifications.'
        }
      ]
    },
    {
      id: 'jobs_cv',
      title: '2. Talent Acquisition & CV Resume Screening',
      icon: Briefcase,
      color: 'from-purple-600 to-indigo-600',
      borderColor: 'border-purple-500/30',
      badge: 'RECRUITMENT',
      howItWorks: [
        'Create job requisitions specifying department, salary range, and technical requirements.',
        'Candidates apply via the standalone Candidate Application Portal with CV resume upload.',
        'AI CV Screener analyzes resumes against job descriptions, calculating match fit scores & skill breakdown.',
        'Recruiters can generate customized interview question recommendations based on candidate CV gaps.',
        'Shortlisted candidates can be moved to interview scheduling or directly onboarded as active employees.'
      ],
      commands: [
        {
          id: 'j1',
          text: 'Create a new job opening for .NET Developer in IT department with location Remote / Hybrid, salary $50,000 - $60,000. Role Overview: Lead enterprise architecture, cloud modernization, and system scalability for IT department. Key Technical Requirements: ASP.NET, C#, Entity Framework, Web API development, Database Management, SQL, LINQ. Core Responsibilities: Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles. • Collaborate across multidisciplinary engineering, UX, and AI agent automation pods. • Optimize query execution, conduct peer code reviews, and champion continuous automated testing.',
          desc: 'Creates a new job opening with full overview, requirements, and responsibilities.'
        },
        {
          id: 'j2',
          text: 'Screen candidate resume fit score for Senior Full Stack Developer position.',
          desc: 'Runs AI evaluation on submitted candidate CVs and outputs match score.'
        },
        {
          id: 'j3',
          text: 'Generate interview question recommendations based on candidate CV.',
          desc: 'Produces role-tailored technical & behavioral interview questions.'
        },
        {
          id: 'j4',
          text: 'Screen candidate resume against Senior React Developer position requirements.',
          desc: 'Compares applicant skills in React, TypeScript, and state management.'
        }
      ]
    },
    {
      id: 'employees',
      title: '3. Employee Directory & Attendance / Leave Management',
      icon: Users,
      color: 'from-emerald-600 to-teal-600',
      borderColor: 'border-emerald-500/30',
      badge: 'WORKFORCE MANAGEMENT',
      howItWorks: [
        'Manage full employee directory, designations, manager assignments, and salary records.',
        'Search or filter employees by department, designation, or employment status.',
        'Log employee attendance, single-day sick leaves, half-day medical appointments, or annual vacation.',
        'Promote employees, adjust compensation bands, or transfer team members between departments with full audit logs.'
      ],
      commands: [
        {
          id: 'e1',
          text: 'Show all active employees in the Engineering department.',
          desc: 'Lists engineering team members, designations, and assigned managers.'
        },
        {
          id: 'e2',
          text: 'Find employee records for Sarah Ahmed and show current designation and salary.',
          desc: 'Fetches detailed employee profile and active compensation details.'
        },
        {
          id: 'e3',
          text: 'Update Designation for Ali Khan to Senior .NET Developer.',
          desc: 'Updates job title in SQL database and records designation history.'
        },
        {
          id: 'e4',
          text: 'Log [name] sick day today and notify his team on gmail.',
          desc: 'Registers full-day sick leave entry in HR attendance logs.'
        }
      ]
    },
    {
      id: 'departments',
      title: '4. Department Operations & Master Corporate Budgeting',
      icon: Building2,
      color: 'from-amber-600 to-orange-600',
      borderColor: 'border-amber-500/30',
      badge: 'FINANCIAL GOVERNANCE',
      howItWorks: [
        'Monitor master corporate budget pool ($1,000,000,000 pool) vs allocated department budgets.',
        'Create new department nodes with appointed department heads and initial allocations.',
        'Reallocate budget between departments (e.g. from Marketing to R&D or IT) with real-time recalculation.',
        'Check unallocated master budget balance or place temporary budget freezes for Q3 audit compliance.'
      ],
      commands: [
        {
          id: 'd1',
          text: 'Create AI Innovations department with head Tariq Mahmood',
          desc: 'Establishes a new department node with assigned department head.'
        },
        {
          id: 'd2',
          text: 'Allocate $150,000 budget to the IT department for Q3.',
          desc: 'Increases quarterly department budget pool from master allocation.'
        },
        {
          id: 'd3',
          text: 'Show departments with allocated budgets over [amount].',
          desc: 'Lists departments with more budget than the amount.'
        },
        {
          id: 'd4',
          text: 'Calculate average employee salary in the Engineering department.',
          desc: 'Calculates average salary of employees in the Engineering department'
        }
      ]
    },
    {
      id: 'policies_expenses',
      title: '5. HR Policy Center & Expense Compliance (POL-FIN-002)',
      icon: Receipt,
      color: 'from-rose-600 to-pink-600',
      borderColor: 'border-rose-500/30',
      badge: 'COMPLIANCE',
      howItWorks: [
        'Enforces corporate reimbursement policy limits (POL-FIN-002): Meal cap $50.00/day, Travel cap $250.00/trip.',
        'Execute automated AI policy compliance sweeps across all submitted expense claims.',
        'Rejecting a claim excludes its dollar amount from total claimed metrics, automatically reducing total expense claim amounts.',
        'Query company remote work policies, parental leave guidelines, or salary band policies.'
      ],
      commands: [
        {
          id: 'p1',
          text: 'Run AI Policy Compliance Sweep on all submitted employee expense claims.',
          desc: 'Audits pending claims against meal ($50) & travel ($250) limits.'
        },
        {
          id: 'p2',
          text: 'Display remote work and home office equipment stipend policy.',
          desc: 'Retrieves full remote work policy text and equipment allowance rules.'
        },
        {
          id: 'p3',
          text: 'explain policy for meal',
          desc: 'Returns the reimbursement rules for meals and dining expenses'
        },
        {
          id: 'p4',
          text: 'Show the current corporate compensation policy POL-HR-001',
          desc: 'Retrieves policy handbook guidelines for Compensation Policy (POL-HR-001).'
        }
      ]
    },


    {
      id: 'onboarding',
      title: '6. Employee Onboarding Hub & Email Automation',
      icon: UserPlus,
      color: 'from-indigo-600 to-purple-600',
      borderColor: 'border-indigo-500/30',
      badge: 'ONBOARDING',
      howItWorks: [
        'Automated provisioning for new hires including department setup, designation, and salary verification.',
        'Generates formal HR Welcome Package documents with corporate policy terms.',
        'Resends official welcome emails directly to employee email addresses (e.g. gmail / corporate mail).',
        'Tracks onboarding milestone completion across 30-60-90 day schedules.'
      ],
      commands: [
        {
          id: 'o1',
          text: 'Onboard Sarah Jenkins as Senior Software Engineer in Engineering at $120,000.',
          desc: 'Initializes employee record, policy verification, and onboarding portal.'
        },
        {
          id: 'o2',
          text: 'Resend official onboarding welcome email to Ahmed Khan at ahmed@company.com.',
          desc: 'Triggers official onboarding welcome communication email dispatch.'
        },
        {
          id: 'o3',
          text: 'Generate complete Onboarding Package document for new hire Ali Khan.',
          desc: 'Renders formal HR appointment letter & policy document packet.'
        },
        {
          id: 'o4',
          text: ' Find employee records for Ahmed Khan, display current designation, and retrieve current annual salary',
          desc: 'Fetches comprehensive employee profile with designation and annual salary details.'
        }
      ]
    }
  ];

  const filteredSections = topicSections.filter(sec => {
    if (activeTabSection !== 'all' && sec.id !== activeTabSection) return false;
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase();
    return (
      sec.title.toLowerCase().includes(q) ||
      sec.howItWorks.some(h => h.toLowerCase().includes(q)) ||
      sec.commands.some(c => c.text.toLowerCase().includes(q) || c.desc.toLowerCase().includes(q))
    );
  });

  return (
    <div className="max-w-7xl mx-auto px-6 py-6 space-y-8">
      {/* Top Banner Header */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 p-6 rounded-3xl border border-indigo-500/30 text-white shadow-xl flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div className="flex items-center gap-4">
          <div className="p-3.5 bg-indigo-600/30 border border-indigo-500/40 rounded-2xl text-indigo-300 backdrop-blur-md">
            <BookOpen className="w-8 h-8 text-indigo-400" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-2xl font-black tracking-tight text-white">System Instructions &amp; Commands Guide</h2>
              <span className="text-[10px] bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 font-mono font-bold px-2 py-0.5 rounded">
                VERSION 2.4
              </span>
            </div>
            <p className="text-xs text-slate-300 mt-1 max-w-2xl leading-relaxed">
              Welcome to the Nexus HR instructions guide. Learn how to use prompt commands, how CV screening works, and try out simple example commands.
            </p>
          </div>
        </div>

        <button
          onClick={() => onNavigateToConsole && onNavigateToConsole()}
          className="px-5 py-3 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold rounded-xl text-xs shadow-lg shadow-blue-500/20 flex items-center gap-2 transition shrink-0 cursor-pointer"
        >
          <Bot className="w-4 h-4 text-cyan-300" />
          <span>Open AI Assistant Console</span>
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>

      {/* Visual Workflow Steps (Box Arrow Flowchart) */}
      <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-md space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Zap className="w-5 h-5 text-indigo-600" />
            <h3 className="text-base font-bold text-slate-900">How Nexus AI Assistant Prompt Processing Works</h3>
          </div>
          <span className="text-[11px] text-slate-400 font-medium">Step-by-Step Execution Lifecycle</span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 relative">
          {/* Step 1 */}
          <div className="bg-slate-900 text-white p-4 rounded-2xl border border-slate-800 space-y-2 relative flex flex-col justify-between">
            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <span className="w-6 h-6 rounded-full bg-blue-600 text-white font-bold text-xs flex items-center justify-center">1</span>
                <span className="text-[10px] bg-blue-500/20 text-blue-300 font-mono px-2 py-0.5 rounded">INPUT</span>
              </div>
              <h4 className="font-bold text-xs text-blue-300 pt-1">Enter Prompt / AI Suggestions</h4>
              <p className="text-[11px] text-slate-300 leading-relaxed">
                Type in AI Console. Grayed-out ghost text appears inline. Press <kbd className="bg-slate-800 border border-slate-700 px-1 py-0.2 rounded font-mono text-[9px]">TAB</kbd> to accept suggestion. Text box auto-expands height.
              </p>
            </div>
            <div className="hidden lg:block absolute -right-3 top-1/2 -translate-y-1/2 z-10 bg-indigo-600 text-white p-1 rounded-full shadow-md">
              <ArrowRight className="w-3.5 h-3.5" />
            </div>
          </div>

          {/* Step 2 */}
          <div className="bg-slate-900 text-white p-4 rounded-2xl border border-slate-800 space-y-2 relative flex flex-col justify-between">
            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <span className="w-6 h-6 rounded-full bg-indigo-600 text-white font-bold text-xs flex items-center justify-center">2</span>
                <span className="text-[10px] bg-indigo-500/20 text-indigo-300 font-mono px-2 py-0.5 rounded">EVALUATION</span>
              </div>
              <h4 className="font-bold text-xs text-indigo-300 pt-1">Intent Parsing &amp; Policy Audit</h4>
              <p className="text-[11px] text-slate-300 leading-relaxed">
                Nexus AI evaluates intent against system policies (POL-FIN-002 meal cap $50, travel $250). If sensitive (e.g. salary change), requires manager approval.
              </p>
            </div>
            <div className="hidden lg:block absolute -right-3 top-1/2 -translate-y-1/2 z-10 bg-indigo-600 text-white p-1 rounded-full shadow-md">
              <ArrowRight className="w-3.5 h-3.5" />
            </div>
          </div>

          {/* Step 3 */}
          <div className="bg-slate-900 text-white p-4 rounded-2xl border border-slate-800 space-y-2 relative flex flex-col justify-between">
            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <span className="w-6 h-6 rounded-full bg-purple-600 text-white font-bold text-xs flex items-center justify-center">3</span>
                <span className="text-[10px] bg-purple-500/20 text-purple-300 font-mono px-2 py-0.5 rounded">EXECUTION</span>
              </div>
              <h4 className="font-bold text-xs text-purple-300 pt-1">Tool Execution &amp; Email Dispatch</h4>
              <p className="text-[11px] text-slate-300 leading-relaxed">
                Executes backend tools: updates SQL DB, creates tickets, computes CV match fit, reallocates budgets, and dispatches onboarding welcome emails.
              </p>
            </div>
            <div className="hidden lg:block absolute -right-3 top-1/2 -translate-y-1/2 z-10 bg-indigo-600 text-white p-1 rounded-full shadow-md">
              <ArrowRight className="w-3.5 h-3.5" />
            </div>
          </div>

          {/* Step 4 */}
          <div className="bg-slate-900 text-white p-4 rounded-2xl border border-slate-800 space-y-2 relative flex flex-col justify-between">
            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <span className="w-6 h-6 rounded-full bg-emerald-600 text-white font-bold text-xs flex items-center justify-center">4</span>
                <span className="text-[10px] bg-emerald-500/20 text-emerald-300 font-mono px-2 py-0.5 rounded">ROUTING</span>
              </div>
              <h4 className="font-bold text-xs text-emerald-300 pt-1">Automatic UI Navigation &amp; Sync</h4>
              <p className="text-[11px] text-slate-300 leading-relaxed">
                App automatically routes user to target view (Expenses, Onboarding, Job Openings) with toast feedback, updating global metrics in real time.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Filter Bar & Search */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-4 border-b border-slate-200 pb-4">
        <div className="flex items-center gap-2 overflow-x-auto w-full sm:w-auto">
          <button
            onClick={() => setActiveTabSection('all')}
            className={`px-3 py-1.5 rounded-xl text-xs font-bold transition cursor-pointer ${activeTabSection === 'all'
              ? 'bg-indigo-600 text-white shadow-xs'
              : 'bg-white text-slate-600 hover:bg-slate-100 border border-slate-200'
              }`}
          >
            All Instructions
          </button>
          {topicSections.map(sec => (
            <button
              key={sec.id}
              onClick={() => setActiveTabSection(sec.id)}
              className={`px-3 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition cursor-pointer ${activeTabSection === sec.id
                ? 'bg-indigo-600 text-white shadow-xs'
                : 'bg-white text-slate-600 hover:bg-slate-100 border border-slate-200'
                }`}
            >
              {sec.title.split('.')[1] || sec.title}
            </button>
          ))}
        </div>

        <div className="relative w-full sm:w-72">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="Search instructions or commands..."
            className="w-full pl-9 pr-4 py-2 bg-white border border-slate-200 rounded-xl text-xs text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-indigo-500/20 focus:border-indigo-600"
          />
        </div>
      </div>

      {/* Detailed Topic Sections */}
      <div className="space-y-8">
        {filteredSections.map(topic => {
          const Icon = topic.icon;
          return (
            <div
              key={topic.id}
              className="bg-white border border-slate-200/90 rounded-3xl p-6 shadow-sm space-y-6 overflow-hidden relative"
            >
              {/* Topic Header */}
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-100">
                <div className="flex items-center gap-3">
                  <div className={`p-3 rounded-2xl bg-gradient-to-r ${topic.color} text-white shadow-md`}>
                    <Icon className="w-6 h-6" />
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="text-lg font-extrabold text-slate-900 tracking-tight">{topic.title}</h3>
                      <span className={`text-[9px] px-2 py-0.5 rounded font-mono font-bold uppercase border bg-slate-100 text-slate-700 ${topic.borderColor}`}>
                        {topic.badge}
                      </span>
                    </div>
                    <p className="text-xs text-slate-500 mt-0.5 font-medium">
                      How this section works and simple example commands you can try.
                    </p>
                  </div>
                </div>
              </div>

              {/* How It Works Bullet Points Box */}
              <div className="bg-slate-900 text-slate-200 p-5 rounded-2xl border border-slate-800 space-y-3">
                <div className="flex items-center gap-2 text-xs font-bold text-cyan-400 uppercase tracking-wider">
                  <Info className="w-4 h-4" />
                  <span>How This Module Works &amp; Guidelines:</span>
                </div>
                <ul className="space-y-2 text-xs text-slate-300">
                  {topic.howItWorks.map((bullet, idx) => (
                    <li key={idx} className="flex items-start gap-2.5">
                      <span className="w-4 h-4 rounded-full bg-indigo-500/20 text-indigo-400 font-bold text-[10px] flex items-center justify-center shrink-0 mt-0.5 border border-indigo-500/30">
                        ✓
                      </span>
                      <span className="leading-relaxed">{bullet}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* 3 to 4 Example Commands */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-slate-700 uppercase tracking-wider flex items-center gap-1.5">
                    <Terminal className="w-4 h-4 text-indigo-600" />
                    <span>Example Commands (Click "Try in AI Assistant" to run):</span>
                  </span>
                  <span className="text-[10px] text-slate-400 font-mono font-bold">{topic.commands.length} Commands</span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {topic.commands.map(cmd => (
                    <div
                      key={cmd.id}
                      className="bg-slate-50 hover:bg-white border border-slate-200/90 hover:border-indigo-300 p-4 rounded-2xl shadow-2xs hover:shadow-md transition-all flex flex-col justify-between space-y-3 group"
                    >
                      <div className="space-y-2">
                        <p className="text-xs text-slate-600 font-medium">
                          {cmd.desc}
                        </p>
                        <div className="bg-slate-900 text-slate-100 p-3 rounded-xl font-mono text-xs leading-relaxed border border-slate-800 break-words group-hover:border-indigo-500/50 transition">
                          {cmd.text}
                        </div>
                      </div>

                      <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-200/60">
                        <button
                          onClick={() => handleCopy(cmd.text, cmd.id)}
                          className="px-3 py-1.5 bg-white hover:bg-slate-100 border border-slate-200 text-slate-700 font-semibold rounded-lg text-xs flex items-center gap-1.5 transition cursor-pointer"
                          title="Copy command to clipboard"
                        >
                          {copiedId === cmd.id ? (
                            <>
                              <Check className="w-3.5 h-3.5 text-emerald-600" />
                              <span className="text-emerald-700 font-bold">Copied!</span>
                            </>
                          ) : (
                            <>
                              <Copy className="w-3.5 h-3.5" />
                              <span>Copy</span>
                            </>
                          )}
                        </button>

                        <button
                          onClick={() => handleTryInConsole(cmd.text)}
                          className="px-3.5 py-1.5 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold rounded-lg text-xs flex items-center gap-1.5 shadow-xs transition cursor-pointer"
                        >
                          <Bot className="w-3.5 h-3.5 text-cyan-300" />
                          <span>Try in AI Assistant</span>
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
