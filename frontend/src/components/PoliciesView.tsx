import React, { useState } from 'react';
import { FileText, Search, Upload, BookOpen } from 'lucide-react';

export const PoliciesView: React.FC = () => {
  const [search, setSearch] = useState('');

  const policies = [
    {
      id: 'POL-HR-001',
      name: 'HR Compensation & Salary Bands Policy',
      category: 'Compensation',
      version: 'v2.4',
      updated: 'Jan 2026',
      status: 'ACTIVE',
      summary: 'Establishes approved salary ranges per designation. Junior: $45k-$55k, Mid-Level .NET Developer: $68,000.00 base, Senior: $85k-$110k.'
    },
    {
      id: 'POL-FIN-002',
      name: 'Corporate Expense & Meal Limit Policy',
      category: 'Finance',
      version: 'v3.1',
      updated: 'Feb 2026',
      status: 'ACTIVE',
      summary: 'Per-diem business expense limits. Meal allowance capped at $50.00 per individual claim. Claims over $50 require HR VP signoff.'
    },
    {
      id: 'POL-HR-003',
      name: 'Enterprise Employee Handbook & Conduct',
      category: 'General Governance',
      version: 'v1.8',
      updated: 'Nov 2025',
      status: 'ACTIVE',
      summary: 'Standard workplace guidelines, remote work policy, equipment allocation, and data privacy protocols.'
    },
    {
      id: 'POL-HR-004',
      name: 'Annual Paid Leave & Absence Policy',
      category: 'Benefits',
      version: 'v2.0',
      updated: 'Dec 2025',
      status: 'ACTIVE',
      summary: '24 days annual paid time off, 10 days sick leave, and parental leave entitlement.'
    }
  ];

  const filtered = policies.filter(p => p.name.toLowerCase().includes(search.toLowerCase()) || p.category.toLowerCase().includes(search.toLowerCase()));

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <FileText className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Corporate Policy Center ({policies.length})</h3>
            <p className="text-xs text-slate-500">Official HR compensation guidelines, per-diem limits, and governance documents.</p>
          </div>
        </div>

        <div className="flex items-center gap-3 w-full sm:w-auto">
          <div className="relative flex-1 sm:w-64">
            <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search policy name or rule..."
              className="w-full pl-9 pr-4 py-1.5 bg-slate-50 border border-slate-200 rounded-lg text-xs text-slate-800 placeholder-slate-400 focus:outline-hidden"
            />
          </div>

          <button className="px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-bold transition-colors flex items-center gap-1.5 shadow-xs">
            <Upload className="w-3.5 h-3.5" />
            <span>Upload Policy</span>
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {filtered.map((pol) => (
          <div key={pol.id} className="bg-white rounded-xl border border-slate-200 p-5 shadow-2xs space-y-3">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-bold text-blue-700 bg-blue-50 border border-blue-200 px-2 py-0.5 rounded">
                  {pol.id}
                </span>
                <span className="text-xs font-semibold text-slate-500">{pol.category}</span>
              </div>
              <span className="text-[10px] bg-emerald-50 text-emerald-700 border border-emerald-200 font-bold px-2 py-0.5 rounded">
                {pol.status} ({pol.version})
              </span>
            </div>

            <h4 className="font-bold text-slate-900 text-sm leading-snug">{pol.name}</h4>
            <p className="text-xs text-slate-600 font-normal leading-relaxed">{pol.summary}</p>

            <div className="pt-2 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-400">
              <span>Last Updated: {pol.updated}</span>
              <button className="text-blue-600 hover:text-blue-800 font-bold flex items-center gap-1">
                <BookOpen className="w-3.5 h-3.5" /> View Policy Rules
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
