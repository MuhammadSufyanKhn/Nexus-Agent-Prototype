import React from 'react';
import { Sparkles, BookOpen, ArrowRight, X, HelpCircle } from 'lucide-react';

interface WelcomeModalProps {
  isOpen: boolean;
  onClose: () => void;
  onGoToInstructions: () => void;
}

export const WelcomeModal: React.FC<WelcomeModalProps> = ({
  isOpen,
  onClose,
  onGoToInstructions,
}) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-800 rounded-3xl max-w-xl w-full text-white shadow-2xl overflow-hidden relative transform animate-in zoom-in-95 duration-200">
        {/* Top Gradient Banner */}
        <div className="bg-gradient-to-r from-blue-600 via-indigo-600 to-cyan-600 p-6 relative">
          <button
            onClick={onClose}
            className="absolute top-4 right-4 p-1.5 rounded-full bg-black/20 hover:bg-black/40 text-white/80 hover:text-white transition-all cursor-pointer"
            title="Dismiss popup"
          >
            <X className="w-5 h-5" />
          </button>
          
          <div className="flex items-center gap-2 text-cyan-200 text-xs font-bold font-mono uppercase tracking-wider mb-2">
            <Sparkles className="w-4 h-4 text-cyan-300 animate-pulse" />
            <span>Welcome to Nexus HR Assistant</span>
          </div>
          
          <h2 className="text-2xl font-black text-white tracking-tight leading-tight">
            New to this project?
          </h2>
          <p className="text-xs text-blue-100 mt-1 max-w-md font-medium leading-relaxed">
            Want to know how everything works? Read our easy system guide and try example commands.
          </p>
        </div>

        {/* Modal Content */}
        <div className="p-6 space-y-5">
          <div className="bg-slate-800/80 border border-slate-700/70 rounded-2xl p-5 space-y-3">
            <div className="flex items-center gap-2.5 text-indigo-400 font-bold text-sm">
              <HelpCircle className="w-5 h-5 text-cyan-400" />
              <span>Getting Started with Nexus HR Assistant</span>
            </div>
            <p className="text-xs text-slate-300 leading-relaxed font-medium">
              New to this? Want to know how to give prompts, how CV screening works, how to create job openings, or how expense reviews operate? Check out our step-by-step instructions hub for detailed visual guides and simple commands you can run with 1-click.
            </p>
          </div>

          {/* Action Buttons */}
          <div className="flex flex-col sm:flex-row items-center justify-end gap-3 pt-2 border-t border-slate-800">
            <button
              onClick={onClose}
              className="w-full sm:w-auto px-5 py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-300 font-semibold rounded-xl text-xs transition cursor-pointer"
            >
              Dismiss
            </button>
            <button
              onClick={() => {
                onClose();
                onGoToInstructions();
              }}
              className="w-full sm:w-auto px-5 py-2.5 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-bold rounded-xl text-xs shadow-lg shadow-blue-500/20 flex items-center justify-center gap-2 transition cursor-pointer"
            >
              <BookOpen className="w-4 h-4" />
              <span>Go to Instructions</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
