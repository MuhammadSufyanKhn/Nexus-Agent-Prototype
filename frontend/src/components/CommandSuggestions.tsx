import React from 'react';
import type { CommandSuggestion } from '../utils/commandEngine';
import { Sparkles, CornerDownLeft, ArrowUpDown, X } from 'lucide-react';

interface CommandSuggestionsProps {
  suggestions: CommandSuggestion[];
  selectedIndex: number;
  onSelectSuggestion: (suggestion: CommandSuggestion) => void;
  onClose: () => void;
}

export const CommandSuggestions: React.FC<CommandSuggestionsProps> = ({
  suggestions,
  selectedIndex,
  onSelectSuggestion,
  onClose,
}) => {
  if (!suggestions || suggestions.length === 0) return null;

  return (
    <div className="absolute left-0 right-0 top-full mt-2 bg-slate-900 text-white rounded-xl shadow-2xl border border-indigo-500/30 overflow-hidden z-50 animate-in fade-in slide-in-from-top-2 duration-150 backdrop-blur-md">
      {/* Header bar */}
      <div className="flex items-center justify-between px-3.5 py-2 bg-indigo-950/60 border-b border-indigo-500/20 text-[11px] text-indigo-300">
        <div className="flex items-center gap-1.5 font-bold">
          <Sparkles className="w-3.5 h-3.5 text-indigo-400 animate-pulse" />
          <span>Nexus HR Command Autocomplete</span>
        </div>
        <div className="flex items-center gap-2 text-[10px] text-slate-400">
          <span className="bg-indigo-900/80 text-indigo-200 px-1.5 py-0.5 rounded font-mono border border-indigo-700/50">TAB to complete</span>
          <span className="flex items-center gap-0.5"><ArrowUpDown className="w-3 h-3" /> Navigate</span>
          <button onClick={onClose} className="hover:text-white p-0.5"><X className="w-3 h-3" /></button>
        </div>
      </div>

      {/* Suggestions List */}
      <div className="p-1.5 space-y-1 max-h-64 overflow-y-auto">
        {suggestions.map((sug, idx) => {
          const isSelected = idx === selectedIndex;
          const categoryColor =
            sug.command.category === 'Budget Management'
              ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30'
              : sug.command.category === 'Employee Management'
              ? 'bg-blue-500/20 text-blue-300 border-blue-500/30'
              : sug.command.category === 'Payroll'
              ? 'bg-amber-500/20 text-amber-300 border-amber-500/30'
              : 'bg-purple-500/20 text-purple-300 border-purple-500/30';

          return (
            <div
              key={sug.command.id + idx}
              onClick={() => onSelectSuggestion(sug)}
              className={`p-2.5 rounded-lg transition-all cursor-pointer flex items-center justify-between border ${
                isSelected
                  ? 'bg-indigo-600/30 border-indigo-500/80 text-white shadow-inner'
                  : 'bg-slate-800/40 hover:bg-slate-800/80 border-transparent text-slate-200'
              }`}
            >
              <div className="space-y-1 min-w-0 pr-2">
                <div className="flex items-center gap-2">
                  <span className="font-bold text-xs tracking-wide">{sug.command.label}</span>
                  <span className={`text-[9px] px-1.5 py-0.2 rounded font-semibold uppercase tracking-wider border ${categoryColor}`}>
                    {sug.command.category}
                  </span>
                </div>
                <div className="text-[11px] text-slate-300 font-mono truncate">
                  {sug.completedText}
                </div>
              </div>

              <div className="shrink-0 flex items-center gap-1">
                {isSelected && (
                  <span className="text-[10px] bg-indigo-500 text-white font-bold px-2 py-0.5 rounded flex items-center gap-1 shadow-xs">
                    Press TAB <CornerDownLeft className="w-3 h-3" />
                  </span>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
