import React from 'react';
import type { CommandSuggestion } from '../utils/commandEngine';
import { Sparkles, CornerDownLeft, ArrowUpDown, X, Zap } from 'lucide-react';

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
    <div className="absolute left-0 right-0 top-full mt-2 bg-slate-950/95 text-white rounded-2xl shadow-2xl border border-indigo-500/40 overflow-hidden z-50 animate-in fade-in slide-in-from-top-2 duration-150 backdrop-blur-xl">
      {/* Gemini AI Header bar */}
      <div className="flex items-center justify-between px-4 py-2.5 bg-gradient-to-r from-blue-950 via-slate-900 to-indigo-950 border-b border-indigo-500/30 text-[11px]">
        <div className="flex items-center gap-2 font-bold">
          <div className="p-1 bg-gradient-to-r from-blue-500 to-cyan-400 rounded-md text-slate-950 font-black">
            <Sparkles className="w-3.5 h-3.5 animate-spin duration-3000" />
          </div>
          <span className="text-white font-extrabold tracking-wide">Gemini AI Suggestions</span>
          <span className="text-[9px] bg-gradient-to-r from-blue-500/20 to-cyan-500/20 text-cyan-300 border border-cyan-400/40 px-2 py-0.5 rounded-full font-mono font-bold">
            GEMINI 1.5 FLASH POWERED
          </span>
        </div>

        <div className="flex items-center gap-3 text-[10px] text-slate-300 font-medium">
          <span className="bg-indigo-900/90 text-cyan-200 px-2 py-0.5 rounded-md font-mono border border-cyan-500/40 shadow-xs flex items-center gap-1 font-bold">
            <Zap className="w-3 h-3 text-cyan-300" />
            TAB to complete
          </span>
          <span className="flex items-center gap-1 text-slate-400">
            <ArrowUpDown className="w-3 h-3 text-slate-400" /> Navigate
          </span>
          <button
            onClick={onClose}
            className="hover:text-white p-1 text-slate-400 hover:bg-slate-800 rounded transition cursor-pointer"
            title="Close suggestions"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Suggestions List */}
      <div className="p-2 space-y-1.5 max-h-72 overflow-y-auto custom-scrollbar">
        {suggestions.map((sug, idx) => {
          const isSelected = idx === selectedIndex;
          const isGeminiSuggestion = sug.matchedBy === 'prefix' || sug.command.category.includes('Gemini');
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
              className={`p-3 rounded-xl transition-all cursor-pointer flex items-center justify-between border ${
                isSelected
                  ? 'bg-gradient-to-r from-blue-600/40 via-indigo-600/40 to-cyan-600/30 border-cyan-400 text-white shadow-md'
                  : 'bg-slate-900/60 hover:bg-slate-800/80 border-slate-800 text-slate-200'
              }`}
            >
              <div className="space-y-1 min-w-0 pr-3">
                <div className="flex items-center gap-2">
                  <span className="font-bold text-xs tracking-wide flex items-center gap-1.5">
                    {sug.command.label}
                  </span>
                  <span
                    className={`text-[9px] px-2 py-0.2 rounded font-semibold uppercase tracking-wider border ${categoryColor}`}
                  >
                    {sug.command.category}
                  </span>
                  {isGeminiSuggestion && (
                    <span className="text-[9px] bg-cyan-950 text-cyan-300 border border-cyan-500/40 font-mono px-1.5 py-0.2 rounded flex items-center gap-1">
                      <Sparkles className="w-2.5 h-2.5 text-cyan-300" />
                      Gemini AI
                    </span>
                  )}
                </div>
                <div className="text-[11px] text-slate-300 font-mono truncate max-w-xl">
                  {sug.completedText}
                </div>
              </div>

              <div className="shrink-0 flex items-center gap-1">
                {isSelected && (
                  <span className="text-[10px] bg-gradient-to-r from-blue-600 to-indigo-600 text-white font-bold px-2.5 py-1 rounded-lg flex items-center gap-1 shadow-sm border border-blue-400/40">
                    <Sparkles className="w-3 h-3 text-cyan-300" />
                    Press TAB <CornerDownLeft className="w-3 h-3 ml-0.5" />
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
