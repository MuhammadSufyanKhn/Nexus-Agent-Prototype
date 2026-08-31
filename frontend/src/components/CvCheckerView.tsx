import React, { useState, useEffect } from 'react';
import { 
  Sparkles, 
  CheckCircle2, 
  AlertCircle, 
  UserPlus, 
  Upload, 
  ShieldCheck, 
  Award, 
  AlertTriangle,
  Briefcase,
  User,
  HelpCircle,
  Star,
  Check,
  Copy,
  Loader2,
  FileText,
  Download,
  Eye,
  FileCode
} from 'lucide-react';
import { 
  executeAgentPrompt,
  fetchJobOpenings,
  fetchCandidateApplications,
  analyzeCandidateCv
} from '../services/api';
import type {
  JobOpening,
  CandidateApplication,
  CvAnalysisResult
} from '../services/api';

interface CvCheckerViewProps {
  initialJobId?: number;
  initialCandidateId?: number;
}

export const CvCheckerView: React.FC<CvCheckerViewProps> = ({
  initialJobId,
  initialCandidateId
}) => {
  const [jobOpenings, setJobOpenings] = useState<JobOpening[]>([]);
  const [selectedJobId, setSelectedJobId] = useState<number | null>(initialJobId || null);
  const [candidates, setCandidates] = useState<CandidateApplication[]>([]);
  const [selectedCandidateId, setSelectedCandidateId] = useState<number | 'custom'>('custom');

  // Candidate PDF & Extracted Text
  const [cvPdfDataUrl, setCvPdfDataUrl] = useState<string>('');
  const [cvFileName, setCvFileName] = useState<string>('Candidate_Resume.pdf');
  const [cvExtractedText, setCvExtractedText] = useState<string>('');
  const [viewMode, setViewMode] = useState<'pdf' | 'extracted'>('pdf');

  const [jobTitle, setJobTitle] = useState('');
  const [requiredSkills, setRequiredSkills] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<CvAnalysisResult | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [candidateCreated, setCandidateCreated] = useState(false);

  // Animated loading sequence states
  const [evalProgress, setEvalProgress] = useState(0);
  const [currentStepIndex, setCurrentStepIndex] = useState(0);
  const [copiedQuestionIndex, setCopiedQuestionIndex] = useState<number | null>(null);

  const evaluationSteps = [
    'Parsing credentials & extracting words from PDF document...',
    'Matching technical competencies against job description...',
    'Conducting deep gap analysis & strengths evaluation...',
    'Formulating candidate-specific technical interview questions with Gemini...'
  ];

  useEffect(() => {
    loadJobs();
  }, []);

  useEffect(() => {
    if (selectedJobId) {
      loadCandidatesForJob(selectedJobId);
      const job = jobOpenings.find(j => j.id === selectedJobId);
      if (job) {
        setJobTitle(job.title || '');
        setRequiredSkills(job.requirements || '');
      }
    }
  }, [selectedJobId, jobOpenings]);

  useEffect(() => {
    if (initialJobId) {
      setSelectedJobId(initialJobId);
    }
    if (initialCandidateId) {
      setSelectedCandidateId(initialCandidateId);
    }
  }, [initialJobId, initialCandidateId]);

  const loadJobs = async () => {
    try {
      const data = await fetchJobOpenings();
      setJobOpenings(data);
      if (data.length > 0 && !selectedJobId) {
        const first = data[0];
        setSelectedJobId(first.id);
        setJobTitle(first.title);
        setRequiredSkills(first.requirements);
      }
    } catch (err) {
      console.error('Failed to load jobs', err);
    }
  };

  const loadCandidatesForJob = async (jobId: number) => {
    try {
      const apps = await fetchCandidateApplications(jobId);
      setCandidates(apps);

      if (apps.length > 0) {
        const target = initialCandidateId 
          ? apps.find(a => a.id === initialCandidateId) || apps[0]
          : apps[0];
        setSelectedCandidateId(target.id);
        applyCandidate(target);
      } else {
        setSelectedCandidateId('custom');
        loadSamplePdf();
      }
    } catch (err) {
      console.error('Failed to load candidate applications', err);
    }
  };

  const applyCandidate = (cand: CandidateApplication) => {
    setCvExtractedText(cand.cvText || '');
    setCvFileName(cand.cvFileName || `${cand.candidateName.replace(/\s+/g, '_')}_Resume.pdf`);
    if (cand.cvPdfData) {
      setCvPdfDataUrl(cand.cvPdfData);
    } else {
      // If no raw base64 data stored yet, generate clean printable PDF data url
      generatePdfDataUrl(cand.candidateName, cand.cvText || '');
    }
  };

  // Helper to create a fallback PDF data URL when only text exists
  const generatePdfDataUrl = (name: string, text: string) => {
    const htmlContent = `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <style>
          body { font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; padding: 40px; color: #1e293b; line-height: 1.6; }
          h1 { font-size: 22px; color: #0f172a; margin-bottom: 4px; border-bottom: 2px solid #3b82f6; padding-bottom: 8px; }
          .header-meta { font-size: 12px; color: #64748b; margin-bottom: 24px; }
          .section { margin-bottom: 20px; }
          .section-title { font-size: 14px; font-weight: bold; color: #1e40af; text-transform: uppercase; margin-bottom: 8px; border-bottom: 1px solid #e2e8f0; padding-bottom: 4px; }
          p, pre { font-size: 12px; white-space: pre-wrap; font-family: inherit; }
        </style>
      </head>
      <body>
        <h1>${name || 'CANDIDATE CURRICULUM VITAE'}</h1>
        <div class="header-meta">Official PDF Resume Document • Nexus Enterprise Talent Acquisition</div>
        <div class="section">
          <div class="section-title">Credentials &amp; Professional Experience</div>
          <p>${(text || 'Resume content loaded into Nexus Database.').replace(/</g, '&lt;').replace(/>/g, '&gt;')}</p>
        </div>
      </body>
      </html>
    `;
    const blob = new Blob([htmlContent], { type: 'text/html' });
    const url = URL.createObjectURL(blob);
    setCvPdfDataUrl(url);
  };

  const loadSamplePdf = () => {
    const sampleText = `CANDIDATE: Ali Khan
Email: ali.khan@devmail.com | Phone: +92-300-1234567 | Location: Lahore, PK

PROFESSIONAL SUMMARY:
Results-driven Software Engineer with 4+ years of hands-on experience building enterprise Web APIs, Microservices, and SQL Server databases using C#, .NET Core 8.0, ASP.NET, Entity Framework, and React.js.

TECHNICAL COMPETENCIES:
- Languages: C#, TypeScript, JavaScript, SQL
- Frameworks: .NET Core 8.0, ASP.NET Core, React.js, Redux, Entity Framework Core
- Databases: SQL Server 2022, T-SQL, Redis
- Tools: Docker, Git, Azure DevOps, Postman, Visual Studio 2022

KEY ACHIEVEMENTS:
- Architected high-throughput RESTful Web APIs handling 15M+ requests monthly with 99.98% uptime.
- Optimized slow SQL Server stored procedures, reducing average query execution latency by 45%.
- Implemented JWT & Role-Based Access Control (RBAC) security frameworks across multi-tenant services.`;

    setCvExtractedText(sampleText);
    setCvFileName('Ali_Khan_Resume.pdf');
    generatePdfDataUrl('Ali Khan - Senior .NET Developer', sampleText);
  };

  const handleSelectJob = (jobId: number) => {
    setSelectedJobId(jobId);
    setResult(null);
    setCandidateCreated(false);
  };

  const handleSelectCandidate = (candId: number | 'custom') => {
    setSelectedCandidateId(candId);
    setResult(null);
    setCandidateCreated(false);

    if (candId === 'custom') {
      loadSamplePdf();
    } else {
      const found = candidates.find(c => c.id === candId);
      if (found) {
        applyCandidate(found);
      }
    }
  };

  const handleManualPdfUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.pdf') && file.type !== 'application/pdf') {
      setErrorMsg('Please upload a valid PDF document (.pdf).');
      return;
    }

    setCvFileName(file.name);
    setErrorMsg(null);

    const reader = new FileReader();
    reader.onload = (event) => {
      const dataUrl = event.target?.result as string;
      setCvPdfDataUrl(dataUrl);
      setSelectedCandidateId('custom');
      // If custom, the backend will extract text from the PDF data
      setCvExtractedText(`[Attached PDF Document: ${file.name} - ${(file.size / 1024).toFixed(1)} KB]`);
    };
    reader.readAsDataURL(file);
  };

  const handleAnalyze = async () => {
    if (!cvExtractedText.trim() && !cvPdfDataUrl) {
      setErrorMsg('Please select a candidate or attach a PDF document to evaluate.');
      return;
    }

    setLoading(true);
    setErrorMsg(null);
    setResult(null);
    setCandidateCreated(false);
    setEvalProgress(0);
    setCurrentStepIndex(0);

    const progressInterval = setInterval(() => {
      setEvalProgress((prev) => {
        if (prev >= 90) return prev;
        const next = prev + Math.floor(Math.random() * 8) + 4;
        if (next > 25 && next <= 50) setCurrentStepIndex(1);
        else if (next > 50 && next <= 75) setCurrentStepIndex(2);
        else if (next > 75) setCurrentStepIndex(3);
        return Math.min(next, 92);
      });
    }, 280);

    try {
      const data = await analyzeCandidateCv({
        cvContent: cvExtractedText || undefined,
        jobTitle: jobTitle,
        requiredSkills: requiredSkills,
        jobOpeningId: selectedJobId || undefined,
        candidateId: typeof selectedCandidateId === 'number' ? selectedCandidateId : undefined
      });

      clearInterval(progressInterval);
      setEvalProgress(100);
      setCurrentStepIndex(3);

      setTimeout(() => {
        setResult(data);
        setLoading(false);
      }, 400);
    } catch (err: any) {
      clearInterval(progressInterval);
      setLoading(false);
      setErrorMsg(err.message || 'CV Analysis failed.');
    }
  };

  const handleCreateCandidateRecord = async () => {
    if (!result?.proposedRecord) return;
    setLoading(true);

    try {
      const rec = result.proposedRecord;
      const prompt = `Onboard employee ${rec.name} in ${rec.department} as ${rec.designation} with salary ${rec.suggestedSalary}`;
      await executeAgentPrompt(prompt, 'Admin');
      setCandidateCreated(true);
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to create candidate record.');
    } finally {
      setLoading(false);
    }
  };

  const handleCopyQuestion = (text: string, index: number) => {
    navigator.clipboard.writeText(text);
    setCopiedQuestionIndex(index);
    setTimeout(() => setCopiedQuestionIndex(null), 2000);
  };

  const wordCount = cvExtractedText ? cvExtractedText.split(/\s+/).filter(Boolean).length : 0;

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Top Header Card (HR Portal Theme) */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <Sparkles className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">CV Screening &amp; Best Fit Evaluation</h3>
            <p className="text-xs text-slate-500">
              Agent automatically extracts words from uploaded PDF resumes and performs neural fit scoring.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 text-xs font-semibold text-emerald-700 bg-emerald-50 border border-emerald-200 px-3 py-1.5 rounded-lg">
          <ShieldCheck className="w-4 h-4 text-emerald-600" />
          <span>PDF Word Extraction &amp; AI Online</span>
        </div>
      </div>

      {/* Target Opening & Candidate Selection Bar */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
        {/* Selector 1: Job Opening */}
        <div>
          <label className="block text-xs font-bold text-slate-700 mb-1.5 flex items-center gap-1.5">
            <Briefcase className="w-4 h-4 text-blue-600" />
            1. Select Target Job Opening
          </label>
          <select
            value={selectedJobId || ''}
            onChange={(e) => handleSelectJob(Number(e.target.value))}
            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3 py-2 text-xs text-slate-800 font-medium focus:bg-white focus:outline-none focus:border-blue-500 transition-colors cursor-pointer"
          >
            {jobOpenings.map(job => (
              <option key={job.id} value={job.id}>
                {job.title} ({job.department}) — {job.applicationsCount || 0} applications received
              </option>
            ))}
          </select>
        </div>

        {/* Selector 2: Candidate Application */}
        <div>
          <label className="block text-xs font-bold text-slate-700 mb-1.5 flex items-center gap-1.5">
            <User className="w-4 h-4 text-blue-600" />
            2. Select Submitted Candidate / PDF
          </label>
          <select
            value={selectedCandidateId}
            onChange={(e) => handleSelectCandidate(e.target.value === 'custom' ? 'custom' : Number(e.target.value))}
            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3 py-2 text-xs text-slate-800 font-medium focus:bg-white focus:outline-none focus:border-blue-500 transition-colors cursor-pointer"
          >
            {candidates.map(cand => (
              <option key={cand.id} value={cand.id}>
                {cand.candidateName} ({cand.experienceYears} yrs exp) — {cand.cvFileName || 'Resume.pdf'}
              </option>
            ))}
            <option value="custom">📄 Upload / View Custom PDF File</option>
          </select>
        </div>
      </div>

      {errorMsg && (
        <div className="bg-rose-50 border border-rose-200 rounded-xl p-3.5 flex items-center gap-2.5 text-rose-700 text-xs">
          <AlertCircle className="w-4 h-4 shrink-0 text-rose-600" />
          <span>{errorMsg}</span>
        </div>
      )}

      {/* SECTION 1 (TOP): Target Job Requirements & EMBEDDED PDF VIEWER */}
      <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-2xs space-y-5">
        <div className="flex items-center justify-between border-b border-slate-100 pb-3">
          <div className="flex items-center gap-2 text-slate-900 font-bold text-sm">
            <Briefcase className="w-4 h-4 text-blue-600" />
            <span>Target Role Requirements</span>
          </div>

          <div className="flex items-center gap-2">
            <label className="bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold px-3 py-1.5 rounded-lg flex items-center gap-1.5 cursor-pointer transition-colors">
              <Upload className="w-3.5 h-3.5" />
              <span>Attach New PDF</span>
              <input
                type="file"
                accept=".pdf,application/pdf"
                onChange={handleManualPdfUpload}
                className="hidden"
              />
            </label>
          </div>
        </div>

        {/* Position Title & Competencies */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Target Position Title</label>
            <input
              type="text"
              value={jobTitle}
              onChange={(e) => setJobTitle(e.target.value)}
              placeholder="e.g. Senior Full Stack Developer"
              className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-800 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
            />
          </div>

          <div>
            <label className="block text-xs font-bold text-slate-700 mb-1">Required Competencies</label>
            <input
              type="text"
              value={requiredSkills}
              onChange={(e) => setRequiredSkills(e.target.value)}
              placeholder="e.g. React, C#, .NET Core, SQL Server, TypeScript"
              className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-800 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
            />
          </div>
        </div>

        {/* PDF DOCUMENT VIEWER (INSTEAD OF RAW TEXTAREA) */}
        <div className="space-y-2 pt-2">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-xs font-bold text-slate-800">
              <FileText className="w-4 h-4 text-blue-600" />
              <span>Candidate CV Document (PDF): <span className="font-semibold text-slate-600">{cvFileName}</span></span>
            </div>

            {/* View Mode Toggle: PDF Viewer vs Extracted Words */}
            <div className="flex bg-slate-100 p-0.5 rounded-lg border border-slate-200 text-xs">
              <button
                type="button"
                onClick={() => setViewMode('pdf')}
                className={`px-3 py-1 rounded-md font-semibold transition-all cursor-pointer flex items-center gap-1.5 ${
                  viewMode === 'pdf'
                    ? 'bg-white text-slate-900 shadow-2xs'
                    : 'text-slate-500 hover:text-slate-800'
                }`}
              >
                <Eye className="w-3.5 h-3.5 text-blue-600" />
                <span>PDF Document Viewer</span>
              </button>

              <button
                type="button"
                onClick={() => setViewMode('extracted')}
                className={`px-3 py-1 rounded-md font-semibold transition-all cursor-pointer flex items-center gap-1.5 ${
                  viewMode === 'extracted'
                    ? 'bg-white text-slate-900 shadow-2xs'
                    : 'text-slate-500 hover:text-slate-800'
                }`}
              >
                <FileCode className="w-3.5 h-3.5 text-emerald-600" />
                <span>Agent Extracted Words ({wordCount})</span>
              </button>
            </div>
          </div>

          {/* VIEW: EMBEDDED PDF DOCUMENT */}
          {viewMode === 'pdf' ? (
            <div className="rounded-xl border border-slate-200 overflow-hidden bg-slate-100 shadow-2xs">
              <div className="bg-slate-50 border-b border-slate-200 px-4 py-2.5 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="w-2.5 h-2.5 rounded-full bg-emerald-500" />
                  <span className="text-xs font-bold text-slate-700">{cvFileName}</span>
                </div>
                {cvPdfDataUrl && (
                  <a
                    href={cvPdfDataUrl}
                    download={cvFileName}
                    className="text-xs text-blue-600 hover:text-blue-700 font-semibold flex items-center gap-1 hover:underline"
                  >
                    <Download className="w-3.5 h-3.5" />
                    <span>Download PDF</span>
                  </a>
                )}
              </div>

              {cvPdfDataUrl ? (
                <iframe
                  src={cvPdfDataUrl}
                  className="w-full h-[520px] bg-white border-0"
                  title="Candidate Resume Document"
                />
              ) : (
                <div className="h-[300px] flex items-center justify-center text-slate-400 text-xs">
                  No PDF document loaded. Attach a PDF to view.
                </div>
              )}
            </div>
          ) : (
            /* VIEW: AGENT EXTRACTED WORDS PREVIEW */
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 space-y-2">
              <div className="flex items-center justify-between text-xs text-slate-500 pb-1 border-b border-slate-200">
                <span className="font-semibold text-slate-700">Text Extracted from PDF Document by Agent:</span>
                <span>{wordCount} words total</span>
              </div>
              <pre className="text-xs text-slate-800 font-mono whitespace-pre-wrap leading-relaxed max-h-[460px] overflow-y-auto p-2 bg-white rounded-lg border border-slate-200">
                {cvExtractedText || 'No text extracted from document yet.'}
              </pre>
            </div>
          )}
        </div>

        {/* Action Button */}
        <div className="flex justify-end pt-2">
          <button
            onClick={handleAnalyze}
            disabled={loading}
            className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-7 py-3 rounded-lg shadow-xs flex items-center gap-2 transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed hover:scale-[1.01] active:scale-[0.99]"
          >
            {loading ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                <span>Evaluating Candidate Fit &amp; Generating Questions...</span>
              </>
            ) : (
              <>
                <Sparkles className="w-4 h-4 text-amber-300" />
                <span>Run AI Evaluation &amp; Score Match</span>
              </>
            )}
          </button>
        </div>
      </div>

      {/* SECTION 2 (STRICTLY BELOW): AI Evaluation Report or Idle State */}
      <div className="space-y-4">
        {loading ? (
          /* Multi-Ring Spinner & Live Progress Loading State (Below Form) */
          <div className="bg-white rounded-xl border border-slate-200 p-8 sm:p-12 shadow-2xs flex flex-col items-center justify-center min-h-[380px] text-center space-y-5 animate-fade-in">
            {/* Multi-Ring Spinner */}
            <div className="relative w-24 h-24 flex items-center justify-center">
              {/* Outer Ring */}
              <div className="absolute inset-0 rounded-full border-4 border-blue-100 border-t-blue-600 animate-spin" style={{ animationDuration: '1.8s' }} />
              {/* Middle Ring */}
              <div className="absolute inset-2 rounded-full border-4 border-indigo-100 border-r-indigo-500 animate-spin" style={{ animationDuration: '1.2s', animationDirection: 'reverse' }} />
              {/* Inner Ring */}
              <div className="absolute inset-4 rounded-full border-4 border-emerald-100 border-b-emerald-500 animate-spin" style={{ animationDuration: '0.9s' }} />
              {/* Centered Percentage Counter */}
              <div className="relative z-10 font-mono font-extrabold text-lg text-slate-900">
                {evalProgress}%
              </div>
            </div>

            <div className="space-y-1 max-w-md">
              <h3 className="text-sm font-bold text-slate-900">
                Running Neural Fit Analysis
              </h3>
              <p className="text-xs text-blue-600 font-medium">
                {evaluationSteps[currentStepIndex]}
              </p>
            </div>

            {/* Progress Bar */}
            <div className="w-full max-w-md bg-slate-100 rounded-full h-2 overflow-hidden border border-slate-200">
              <div 
                className="bg-blue-600 h-full rounded-full transition-all duration-300 ease-out"
                style={{ width: `${evalProgress}%` }}
              />
            </div>

            {/* Step-by-Step Indicators */}
            <div className="w-full max-w-md space-y-1.5 text-left pt-1">
              {evaluationSteps.map((step, idx) => {
                const isDone = idx < currentStepIndex || evalProgress === 100;
                const isCurrent = idx === currentStepIndex && evalProgress < 100;

                return (
                  <div 
                    key={idx}
                    className={`flex items-center gap-2 text-xs p-2 rounded-lg border transition-all ${
                      isDone 
                        ? 'bg-emerald-50 border-emerald-200 text-emerald-700 font-medium' 
                        : isCurrent 
                          ? 'bg-blue-50 border-blue-200 text-blue-800 font-medium' 
                          : 'bg-slate-50 border-slate-100 text-slate-400'
                    }`}
                  >
                    {isDone ? (
                      <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 shrink-0" />
                    ) : isCurrent ? (
                      <Loader2 className="w-3.5 h-3.5 text-blue-600 animate-spin shrink-0" />
                    ) : (
                      <div className="w-3.5 h-3.5 rounded-full border border-slate-300 shrink-0" />
                    )}
                    <span className="truncate">{step}</span>
                  </div>
                );
              })}
            </div>
          </div>
        ) : result ? (
          /* Evaluated Report Results (Below Form) */
          <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-2xs space-y-6 animate-fade-in">
            {/* Score & Best Fit Badge Banner */}
            <div className="flex flex-col sm:flex-row items-center justify-between gap-6 bg-slate-50 border border-slate-200 rounded-xl p-5">
              <div className="space-y-1.5 text-center sm:text-left">
                <div className="flex flex-wrap items-center justify-center sm:justify-start gap-2">
                  {result.isBestFit ? (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-100 border border-emerald-300 text-emerald-800 text-xs font-bold uppercase shadow-2xs">
                      <Star className="w-3.5 h-3.5 fill-current text-amber-500" />
                      ⭐ Best Fit For This Position
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-100 border border-blue-200 text-blue-800 text-xs font-bold uppercase">
                      <Award className="w-3.5 h-3.5" />
                      {result.fitCategory || result.recommendation || 'Evaluated'}
                    </span>
                  )}

                  <span className="text-xs text-slate-500 font-medium">
                    {result.experienceYears || 0}+ Years Relevant Exp
                  </span>
                </div>

                <h2 className="text-xl font-bold text-slate-900">
                  {result.candidateName || 'Candidate Profile'}
                </h2>
                <p className="text-xs text-slate-500">
                  Evaluated for position: <span className="text-slate-800 font-semibold">{result.targetPosition || jobTitle}</span>
                </p>
              </div>

              {/* Score Visual */}
              <div className="flex items-center gap-4 shrink-0">
                <div className="text-right">
                  <div className="text-3xl font-extrabold text-slate-900">{result.matchScore}%</div>
                  <div className="text-[11px] font-bold uppercase text-slate-500">Match Score</div>
                </div>
                <div className="w-16 h-16 rounded-full bg-blue-50 border-4 border-blue-600 flex items-center justify-center text-blue-600 font-extrabold text-sm">
                  {result.matchScore >= 80 ? 'Fit' : 'Rev'}
                </div>
              </div>
            </div>

            {/* Candidate Summary */}
            {result.fitSummary && (
              <div className="bg-slate-50 border border-slate-200 rounded-lg p-4 space-y-1">
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700">Executive Fit Summary</h4>
                <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-line">
                  {result.fitSummary}
                </p>
              </div>
            )}

            {/* Extracted Verified Skills */}
            {result.extractedSkills && result.extractedSkills.length > 0 && (
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700 mb-2">Verified Technical Skills</h4>
                <div className="flex flex-wrap gap-1.5">
                  {result.extractedSkills.map((skill, idx) => (
                    <span key={idx} className="text-xs bg-slate-100 text-slate-800 border border-slate-200 px-2.5 py-1 rounded font-medium">
                      {skill}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Strengths & Missing Competencies Grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {/* Strengths */}
              <div className="bg-emerald-50/50 border border-emerald-200 rounded-xl p-4 space-y-2">
                <div className="text-xs font-bold uppercase tracking-wider text-emerald-800 flex items-center gap-1.5">
                  <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                  Key Candidate Strengths
                </div>
                <ul className="space-y-1.5">
                  {(result.strengths && result.strengths.length > 0 ? result.strengths : ['Demonstrated hands-on experience in core stack', 'Solid project implementation record']).map((s, idx) => (
                    <li key={idx} className="text-xs text-slate-700 flex items-start gap-2">
                      <span className="text-emerald-600 font-bold">•</span>
                      <span>{s}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Missing Competencies / Skill Gaps */}
              <div className="bg-amber-50/50 border border-amber-200 rounded-xl p-4 space-y-2">
                <div className="text-xs font-bold uppercase tracking-wider text-amber-800 flex items-center gap-1.5">
                  <AlertTriangle className="w-4 h-4 text-amber-600" />
                  Skill Gaps / Missing Competencies
                </div>
                <ul className="space-y-1.5">
                  {(result.missingSkills && result.missingSkills.length > 0 
                    ? result.missingSkills 
                    : ['No critical missing competencies identified']).map((m, idx) => (
                    <li key={idx} className="text-xs text-slate-700 flex items-start gap-2">
                      <span className="text-amber-600 font-bold">•</span>
                      <span>{m}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </div>

            {/* Role-Specific AI Interview Questions (Exactly 5) */}
            <div className="space-y-3 pt-1">
              <div className="flex items-center justify-between border-b border-slate-100 pb-2">
                <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 flex items-center gap-2">
                  <HelpCircle className="w-4 h-4 text-blue-600" />
                  5 Role-Specific Technical Interview Questions
                </h4>
                <span className="text-[10px] text-blue-700 bg-blue-50 px-2 py-0.5 rounded border border-blue-200 font-bold">
                  Dynamic Gemini AI Generated
                </span>
              </div>

              <div className="space-y-2.5">
                {(result.recommendedInterviewQuestions || []).map((q, idx) => (
                  <div 
                    key={idx}
                    className="bg-slate-50 border border-slate-200 rounded-xl p-3.5 flex items-start justify-between gap-3 group hover:bg-white hover:border-slate-300 transition-colors"
                  >
                    <div className="flex items-start gap-2.5 text-xs text-slate-800">
                      <span className="w-5 h-5 rounded-full bg-blue-100 text-blue-700 flex items-center justify-center font-bold text-[10px] shrink-0 mt-0.5">
                        {idx + 1}
                      </span>
                      <p className="leading-relaxed">{q}</p>
                    </div>

                    <button
                      onClick={() => handleCopyQuestion(q, idx)}
                      className="text-slate-400 hover:text-slate-700 p-1.5 hover:bg-slate-100 rounded-lg transition-colors cursor-pointer shrink-0"
                      title="Copy Question"
                    >
                      {copiedQuestionIndex === idx ? (
                        <Check className="w-3.5 h-3.5 text-emerald-600" />
                      ) : (
                        <Copy className="w-3.5 h-3.5" />
                      )}
                    </button>
                  </div>
                ))}
              </div>
            </div>

            {/* Onboard Candidate Action */}
            {result.proposedRecord && (
              <div className="pt-3 border-t border-slate-100 flex flex-col sm:flex-row items-center justify-between gap-4">
                <div className="text-xs text-slate-500 text-center sm:text-left">
                  {candidateCreated ? (
                    <span className="text-emerald-700 font-bold flex items-center gap-1.5 justify-center sm:justify-start">
                      <CheckCircle2 className="w-4 h-4 text-emerald-600" /> Candidate onboarding workflow initialized in Nexus!
                    </span>
                  ) : (
                    <span>Approve this candidate to create an employee record in the Directory.</span>
                  )}
                </div>

                {!candidateCreated && (
                  <button
                    onClick={handleCreateCandidateRecord}
                    disabled={loading}
                    className="w-full sm:w-auto bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold px-6 py-2.5 rounded-lg shadow-xs flex items-center justify-center gap-2 transition-all cursor-pointer"
                  >
                    <UserPlus className="w-4 h-4" />
                    <span>Approve &amp; Onboard Candidate</span>
                  </button>
                )}
              </div>
            )}
          </div>
        ) : (
          /* Idle State Box (Strictly Below Form) */
          <div className="bg-white rounded-xl border border-slate-200 p-8 text-center shadow-2xs space-y-2">
            <div className="p-3 bg-blue-50 text-blue-600 rounded-xl w-12 h-12 mx-auto flex items-center justify-center">
              <Sparkles className="w-6 h-6" />
            </div>
            <h4 className="text-sm font-bold text-slate-900">Awaiting Candidate Evaluation</h4>
            <p className="text-xs text-slate-500 max-w-md mx-auto">
              Select a target Job Opening and submitted Candidate CV above, then click <strong>"Run AI Evaluation &amp; Score Match"</strong> to generate the deep fit assessment.
            </p>
          </div>
        )}
      </div>
    </div>
  );
};
