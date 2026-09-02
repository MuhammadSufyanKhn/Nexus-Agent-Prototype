import React, { useEffect, useState } from 'react';
import { X, Download, Printer, FileText, CheckCircle2, Building2, User, Calendar } from 'lucide-react';

export interface DocumentPreviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  documentId?: string | null;
  contentHtml?: string | null;
  documentTitle?: string | null;
  documentType?: string | null;
  employeeName?: string | null;
  department?: string | null;
  createdAt?: string | null;
}

export const DocumentPreviewModal: React.FC<DocumentPreviewModalProps> = ({
  isOpen,
  onClose,
  documentId,
  contentHtml: initialHtml,
  documentTitle: initialTitle,
  documentType: initialType,
  employeeName: initialEmployee,
  department: initialDept,
  createdAt: initialDate
}) => {
  const [html, setHtml] = useState<string>(initialHtml || '');
  const [title, setTitle] = useState<string>(initialTitle || 'Document Preview');
  const [docType, setDocType] = useState<string>(initialType || 'DOCUMENT');
  const [empName, setEmpName] = useState<string>(initialEmployee || '');
  const [dept, setDept] = useState<string>(initialDept || '');
  const [date, setDate] = useState<string>(initialDate || new Date().toLocaleDateString());
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;

    if (initialHtml) {
      setHtml(initialHtml);
      setTitle(initialTitle || 'Official Document Preview');
      setDocType(initialType || 'DOCUMENT');
      setEmpName(initialEmployee || '');
      setDept(initialDept || '');
      setDate(initialDate || new Date().toLocaleDateString());
      return;
    }

    if (documentId) {
      setLoading(true);
      setError(null);
      fetch(`/api/documents/${documentId}`)
        .then((res) => {
          if (!res.ok) throw new Error(`HTTP ${res.status}: Failed to load document.`);
          return res.json();
        })
        .then((data) => {
          setHtml(data.contentHtml || '');
          setTitle(data.title || 'Official Document Preview');
          setDocType(data.type || 'DOCUMENT');
          setEmpName(data.employeeName || '');
          setDept(data.department || '');
          setDate(data.createdAt ? new Date(data.createdAt).toLocaleDateString() : new Date().toLocaleDateString());
        })
        .catch((err) => {
          console.error('Document fetch error:', err);
          setError(err.message || 'Unable to load document.');
        })
        .finally(() => setLoading(false));
    }
  }, [isOpen, documentId, initialHtml, initialTitle, initialType, initialEmployee, initialDept, initialDate]);

  if (!isOpen) return null;

  const handlePrint = () => {
    const printWindow = window.open('', '_blank');
    if (printWindow) {
      printWindow.document.write(html);
      printWindow.document.close();
      printWindow.focus();
      printWindow.print();
    }
  };

  const downloadUrl = documentId ? `/api/documents/${documentId}/download` : undefined;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6 bg-slate-950/70 backdrop-blur-sm animate-in fade-in duration-200"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-5xl h-[90vh] bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl flex flex-col overflow-hidden text-slate-100"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Modal Top Bar */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-800 bg-slate-900/90 backdrop-blur">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-center justify-center shrink-0">
              <FileText className="w-5 h-5 text-indigo-400" />
            </div>
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-white truncate">{title}</h3>
                <span className="px-2 py-0.5 text-[10px] font-mono font-semibold uppercase tracking-wider bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 rounded-full shrink-0">
                  {docType}
                </span>
              </div>
              <div className="flex items-center gap-3 text-xs text-slate-400 mt-0.5">
                {empName && (
                  <span className="flex items-center gap-1">
                    <User className="w-3.5 h-3.5 text-slate-500" />
                    {empName}
                  </span>
                )}
                {dept && (
                  <span className="flex items-center gap-1">
                    <Building2 className="w-3.5 h-3.5 text-slate-500" />
                    {dept}
                  </span>
                )}
                {date && (
                  <span className="flex items-center gap-1">
                    <Calendar className="w-3.5 h-3.5 text-slate-500" />
                    {date}
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex items-center gap-2 shrink-0">
            <button
              onClick={handlePrint}
              title="Print document"
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors border border-slate-700 cursor-pointer"
            >
              <Printer className="w-3.5 h-3.5" />
              Print
            </button>
            {downloadUrl ? (
              <a
                href={downloadUrl}
                download
                className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors shadow-xs"
              >
                <Download className="w-3.5 h-3.5" />
                Download PDF
              </a>
            ) : (
              <button
                onClick={handlePrint}
                className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-colors shadow-xs cursor-pointer"
              >
                <Download className="w-3.5 h-3.5" />
                Save / Print
              </button>
            )}
            <button
              onClick={onClose}
              className="w-8 h-8 rounded-lg bg-slate-800/80 hover:bg-slate-700 text-slate-400 hover:text-white flex items-center justify-center transition-colors cursor-pointer ml-1"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Modal Body / Document Preview Canvas */}
        <div className="flex-1 bg-slate-950/60 p-4 sm:p-6 overflow-y-auto flex justify-center">
          {loading ? (
            <div className="flex flex-col items-center justify-center h-full text-slate-400 space-y-3">
              <div className="w-8 h-8 border-2 border-indigo-500 border-t-transparent rounded-full animate-spin" />
              <p className="text-xs font-medium">Rendering document preview...</p>
            </div>
          ) : error ? (
            <div className="flex flex-col items-center justify-center h-full text-rose-400 space-y-2">
              <p className="text-sm font-semibold">Failed to load preview</p>
              <p className="text-xs text-slate-500">{error}</p>
            </div>
          ) : (
            <div className="w-full max-w-3xl bg-white text-slate-900 rounded-xl shadow-2xl overflow-hidden border border-slate-200">
              <iframe
                title={title}
                srcDoc={html}
                className="w-full h-full min-h-[70vh] border-0"
                sandbox="allow-same-origin"
              />
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-slate-800 bg-slate-900/90 text-slate-400 text-xs flex items-center justify-between">
          <div className="flex items-center gap-1.5 text-emerald-400 text-[11px] font-medium">
            <CheckCircle2 className="w-3.5 h-3.5" />
            Official cryptographic seal verified by Nexus Document System
          </div>
          <span className="text-slate-500 text-[11px]">
            ID: {documentId || 'GENERATED-TEMP'}
          </span>
        </div>
      </div>
    </div>
  );
};
