import React from 'react';
import { Cpu, Globe, Database, Mail, CheckCircle2 } from 'lucide-react';

export const AutomationView: React.FC = () => {
  const subsystems = [
    {
      name: 'Legacy HR Portal Automation',
      type: 'Playwright Browser Automation',
      status: 'ONLINE & READY',
      url: 'http://127.0.0.1:8088/index.html',
      detail: 'Automated Playwright form submission for legacy enterprise employee directory sync.',
      icon: Globe,
      color: 'blue'
    },
    {
      name: 'Mock SAP ERP HCM System',
      type: 'MOCK SAP CONNECTOR',
      status: 'ONLINE & READY',
      url: 'SAP NCo / OData Protocol (Simulated)',
      detail: 'Enterprise SAP Personnel Master Data provisioning (SAP-EMP-2026-XXXX).',
      icon: Database,
      color: 'amber'
    },
    {
      name: 'Welcome Email Subsystem',
      type: 'Python Automation',
      status: 'ONLINE & READY',
      url: 'automation/email_services/',
      detail: 'Generates official IT onboarding welcome emails for new workforce members.',
      icon: Mail,
      color: 'emerald'
    },
    {
      name: 'IT Service Ticket Creator',
      type: 'Python Automation',
      status: 'ONLINE & READY',
      url: 'automation/tickets/',
      detail: 'Creates automated hardware and access request tickets for new employees.',
      icon: Cpu,
      color: 'indigo'
    }
  ];

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <Cpu className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">System Automation & Integrations</h3>
            <p className="text-xs text-slate-500">Subsystem status of registered automation tools and connector drivers.</p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {subsystems.map((sub, idx) => {
          const Icon = sub.icon;
          return (
            <div key={idx} className="bg-white rounded-xl border border-slate-200 p-5 shadow-2xs space-y-3">
              <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                <div className="flex items-center gap-2.5">
                  <div className="p-2 bg-slate-100 rounded-lg text-slate-700">
                    <Icon className="w-4 h-4" />
                  </div>
                  <div>
                    <h4 className="font-bold text-slate-900 text-sm">{sub.name}</h4>
                    <span className="text-[10px] font-bold text-slate-500 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                      {sub.type}
                    </span>
                  </div>
                </div>

                <span className="text-[10px] bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-2 py-0.5 rounded flex items-center gap-1">
                  <CheckCircle2 className="w-3 h-3 text-emerald-600" />
                  <span>{sub.status}</span>
                </span>
              </div>

              <p className="text-xs text-slate-600 leading-relaxed font-normal">{sub.detail}</p>
              <div className="text-[11px] text-slate-400 font-medium font-mono pt-1">Target: {sub.url}</div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
