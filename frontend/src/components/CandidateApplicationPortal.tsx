import React, { useState, useEffect } from 'react';
import { 
  MapPin, 
  DollarSign, 
  Upload, 
  CheckCircle2, 
  AlertCircle, 
  ArrowLeft,
  FileCheck,
  Building,
  Check,
  X
} from 'lucide-react';
import { fetchJobOpenings, submitCandidateApplication } from '../services/api';
import type { JobOpening } from '../services/api';

interface CandidateApplicationPortalProps {
  initialJobId?: number;
  onBackToApp?: () => void;
  onBackToPortal?: () => void;
}

export const CandidateApplicationPortal: React.FC<CandidateApplicationPortalProps> = ({
  initialJobId,
  onBackToApp,
  onBackToPortal
}) => {
  const handleBack = onBackToPortal || onBackToApp;

  const [jobOpenings, setJobOpenings] = useState<JobOpening[]>([]);
  const [selectedJobId, setSelectedJobId] = useState<number | null>(initialJobId || null);
  const [selectedJob, setSelectedJob] = useState<JobOpening | null>(null);
  const [selectedDeptFilter, setSelectedDeptFilter] = useState<string>('ALL');

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [submittedSuccess, setSubmittedSuccess] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isApplyModalOpen, setIsApplyModalOpen] = useState(false);

  // Form fields — ZERO DEFAULT PRE-FILLED VALUES
  const [candidateName, setCandidateName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [experienceYears, setExperienceYears] = useState<number | ''>('');
  const [coverNote, setCoverNote] = useState('');

  // PDF-ONLY Upload states (NO paste text)
  const [cvPdfFile, setCvPdfFile] = useState<File | null>(null);
  const [cvPdfDataUrl, setCvPdfDataUrl] = useState<string>('');
  const [cvFileName, setCvFileName] = useState('');

  useEffect(() => {
    loadJobs();
  }, []);

  useEffect(() => {
    if (selectedJobId && jobOpenings.length > 0) {
      const found = jobOpenings.find(j => j.id === selectedJobId) || null;
      setSelectedJob(found);
    }
  }, [selectedJobId, jobOpenings]);

  const loadJobs = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchJobOpenings();
      setJobOpenings(data);
      if (initialJobId) {
        const found = data.find(j => j.id === initialJobId);
        if (found) {
          setSelectedJob(found);
          setSelectedJobId(found.id);
        }
      }
    } catch (err: any) {
      setError(err.message || 'Failed to load available career openings.');
    } finally {
      setLoading(false);
    }
  };

  // PDF-ONLY Handler
  const handlePdfUpload = (file: File) => {
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.pdf') && file.type !== 'application/pdf') {
      setError('Only PDF documents (.pdf) are accepted. Please attach a valid PDF file.');
      return;
    }

    setError(null);
    setCvPdfFile(file);
    setCvFileName(file.name);

    const reader = new FileReader();
    reader.onload = (event) => {
      const result = event.target?.result as string;
      setCvPdfDataUrl(result);
    };
    reader.readAsDataURL(file);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      handlePdfUpload(file);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    const file = e.dataTransfer.files?.[0];
    if (file) {
      handlePdfUpload(file);
    }
  };

  const handleRemovePdf = () => {
    setCvPdfFile(null);
    setCvPdfDataUrl('');
    setCvFileName('');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedJob) {
      setError('Please select a job opening first.');
      return;
    }
    if (!candidateName.trim() || !email.trim()) {
      setError('Full Legal Name and Email Address are required.');
      return;
    }
    if (!cvPdfDataUrl) {
      setError('Please attach your CV in PDF format (.pdf). Only PDF documents are accepted.');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const res = await submitCandidateApplication(selectedJob.id, {
        candidateName: candidateName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        experienceYears: typeof experienceYears === 'number' ? experienceYears : 0,
        coverNote: coverNote.trim(),
        cvFileName: cvFileName || `${candidateName.replace(/\s+/g, '_')}_Resume.pdf`,
        cvPdfData: cvPdfDataUrl
      });

      setIsApplyModalOpen(false);
      setSubmittedSuccess(res);
    } catch (err: any) {
      setError(err.message || 'Submission failed. Please verify your details.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleResetForm = () => {
    setCandidateName('');
    setEmail('');
    setPhone('');
    setExperienceYears('');
    setCoverNote('');
    handleRemovePdf();
    setSubmittedSuccess(null);
    setError(null);
  };

  // Department Filters: Extract unique departments
  const departments = ['ALL', ...Array.from(new Set(jobOpenings.map(j => j.department?.trim()).filter(Boolean)))];

  // Filtered job list
  const filteredJobs = jobOpenings.filter(job => {
    if (selectedDeptFilter === 'ALL') return true;
    return job.department?.toLowerCase() === selectedDeptFilter.toLowerCase();
  });

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-50 flex items-center justify-center p-6 text-slate-800">
        <div className="text-center space-y-3">
          <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto" />
          <p className="text-sm font-semibold text-slate-600">Connecting to Nexus Career Portal...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 font-sans pb-16">
      {/* Top Navigation Bar (HR Portal Theme) */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30 shadow-2xs">
        <div className="max-w-5xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-blue-600 text-white flex items-center justify-center font-black text-base shadow-xs">
              N
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-bold text-slate-900 tracking-tight text-sm">NEXUS HR</span>
                <span className="text-[10px] font-bold text-blue-700 bg-blue-50 border border-blue-200 px-2 py-0.5 rounded">
                  CAREERS
                </span>
              </div>
              <p className="text-[11px] text-slate-500">Official Candidate Application Portal</p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {handleBack && (
              <button
                onClick={handleBack}
                className="flex items-center gap-1.5 text-xs font-semibold text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 px-3 py-1.5 rounded-lg transition-colors cursor-pointer"
              >
                <ArrowLeft className="w-3.5 h-3.5" />
                <span>Return to Internal Portal</span>
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="max-w-5xl mx-auto px-6 pt-8 space-y-8">
        
        {/* SUCCESS SCREEN */}
        {submittedSuccess ? (
          <div className="bg-white rounded-xl border border-slate-200 p-8 sm:p-12 shadow-2xs text-center max-w-2xl mx-auto space-y-6 animate-fade-in">
            <div className="w-16 h-16 rounded-full bg-emerald-50 text-emerald-600 border border-emerald-200 flex items-center justify-center mx-auto">
              <CheckCircle2 className="w-9 h-9" />
            </div>

            <div className="space-y-2">
              <h2 className="text-2xl font-bold text-slate-900 tracking-tight">Application Successfully Submitted!</h2>
              <p className="text-xs text-slate-500 max-w-md mx-auto leading-relaxed">
                Thank you, <strong>{candidateName}</strong>. Your PDF curriculum vitae has been registered into the Nexus HR database for the <strong>{selectedJob?.title}</strong> requisition.
              </p>
            </div>

            <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 text-xs text-slate-600 text-left space-y-2">
              <div className="flex justify-between">
                <span className="text-slate-400">Position Target:</span>
                <span className="font-bold text-slate-800">{selectedJob?.title}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Department:</span>
                <span className="font-bold text-slate-800">{selectedJob?.department}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Attached Resume:</span>
                <span className="font-bold text-blue-600 flex items-center gap-1">
                  <FileCheck className="w-3.5 h-3.5" />
                  {cvFileName}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-slate-400">Status:</span>
                <span className="font-bold text-amber-600 bg-amber-50 px-2.5 py-0.5 rounded-full border border-amber-200">In Progress</span>
              </div>
            </div>

            {/* Automated Email Confirmation Banner */}
            <div className="p-3.5 bg-blue-50 border border-blue-200 rounded-xl text-left space-y-1">
              <div className="flex items-center gap-2 text-xs font-bold text-blue-900">
                <Check className="w-4 h-4 text-blue-600 shrink-0" />
                <span>Confirmation Email Dispatched</span>
              </div>
              <p className="text-[11px] text-blue-800 leading-relaxed pl-6">
                An official acknowledgment notification has been sent to <strong>{email}</strong> from <code>nexusagent.notifications@gmail.com</code> outlining the 7-day talent acquisition review timeline.
              </p>
            </div>

            <div className="pt-2 flex justify-center gap-3">
              <button
                onClick={handleResetForm}
                className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-6 py-2.5 rounded-lg shadow-xs transition-all cursor-pointer"
              >
                Submit Another Application
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-8 animate-fade-in">
            
            {/* DEPARTMENT FILTER BAR (e.g. ALL, IT, Marketing, HR) */}
            <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-2xs space-y-3">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-100 pb-3">
                <div className="flex items-center gap-2.5">
                  <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
                    <Building className="w-4 h-4" />
                  </div>
                  <div>
                    <h3 className="text-xs font-bold uppercase tracking-wider text-slate-700">Filter Job Openings by Department</h3>
                    <p className="text-[11px] text-slate-400">Browse open positions across corporate organizational units</p>
                  </div>
                </div>
                <div className="text-xs text-slate-500">
                  Showing <strong>{filteredJobs.length}</strong> of <strong>{jobOpenings.length}</strong> active positions
                </div>
              </div>

              {/* Department Pills */}
              <div className="flex flex-wrap items-center gap-2 pt-1">
                {departments.map((dept) => {
                  const isSelected = selectedDeptFilter.toLowerCase() === (dept || '').toLowerCase();
                  const count = dept === 'ALL' 
                    ? jobOpenings.length 
                    : jobOpenings.filter(j => j.department?.toLowerCase() === (dept || '').toLowerCase()).length;

                  return (
                    <button
                      key={dept}
                      onClick={() => {
                        setSelectedDeptFilter(dept || 'ALL');
                        // If current selected job is filtered out, select the first visible job
                        const remaining = jobOpenings.filter(j => dept === 'ALL' || j.department?.toLowerCase() === (dept || '').toLowerCase());
                        if (remaining.length > 0 && (!selectedJob || !remaining.some(r => r.id === selectedJob.id))) {
                          setSelectedJob(remaining[0]);
                          setSelectedJobId(remaining[0].id);
                        }
                      }}
                      className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer flex items-center gap-1.5 ${
                        isSelected
                          ? 'bg-blue-600 text-white shadow-xs'
                          : 'bg-slate-100 hover:bg-slate-200 text-slate-700'
                      }`}
                    >
                      <span>{dept === 'ALL' ? 'All Departments' : dept}</span>
                      <span className={`text-[10px] px-1.5 py-0.2 rounded-full font-bold ${
                        isSelected ? 'bg-blue-700 text-white' : 'bg-slate-200 text-slate-600'
                      }`}>
                        {count}
                      </span>
                    </button>
                  );
                })}
              </div>

              {/* Job Selector Cards / List if multiple openings in filtered department */}
              {filteredJobs.length > 1 && (
                <div className="pt-3 border-t border-slate-100">
                  <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-2">
                    Available Roles in {selectedDeptFilter === 'ALL' ? 'Company' : selectedDeptFilter}:
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2.5">
                    {filteredJobs.map((job) => {
                      const isCurrent = selectedJob?.id === job.id;
                      return (
                        <div
                          key={job.id}
                          onClick={() => {
                            setSelectedJob(job);
                            setSelectedJobId(job.id);
                          }}
                          className={`p-3 rounded-lg border text-left cursor-pointer transition-all ${
                            isCurrent
                              ? 'bg-blue-50/70 border-blue-500 ring-1 ring-blue-500 shadow-2xs'
                              : 'bg-slate-50 hover:bg-slate-100 border-slate-200'
                          }`}
                        >
                          <div className="flex items-center justify-between mb-1">
                            <span className="text-[10px] font-bold text-blue-700 bg-white px-1.5 py-0.5 rounded border border-blue-200">
                              {job.department}
                            </span>
                            {isCurrent && <Check className="w-3.5 h-3.5 text-blue-600" />}
                          </div>
                          <h4 className="text-xs font-bold text-slate-900 truncate">{job.title}</h4>
                          <div className="text-[11px] text-slate-500 mt-1 flex items-center gap-1">
                            <MapPin className="w-3 h-3 text-slate-400" />
                            <span className="truncate">{job.location || 'Remote'}</span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>

            {/* SECTION 1 (TOP): Full Job Details, Responsibilities & Perks */}
            {selectedJob ? (() => {
              const responsibilitiesList = (selectedJob.responsibilities || '')
                .split(/[\n•;]+/)
                .map(r => r.trim())
                .filter(Boolean);

              const displayResponsibilities = responsibilitiesList.length > 0
                ? responsibilitiesList
                : [
                    `Design, build, and maintain production-grade scalable systems adhering to Clean Architecture principles.`,
                    `Collaborate across multidisciplinary engineering, UX, and AI agent automation pods.`,
                    `Optimize query execution, conduct peer code reviews, and champion continuous automated testing.`
                  ];

              return (
                <div className="bg-white rounded-xl border border-slate-200 p-6 sm:p-8 shadow-2xs space-y-6">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 pb-5">
                    <div className="space-y-1.5">
                      <div className="flex items-center gap-2">
                        <span className="text-[11px] font-bold text-blue-700 bg-blue-50 border border-blue-200 px-2.5 py-0.5 rounded">
                          {selectedJob.department} Department
                        </span>
                        <span className="text-[11px] font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2.5 py-0.5 rounded flex items-center gap-1.5">
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                          Active Opening
                        </span>
                      </div>
                      <h1 className="text-2xl font-extrabold text-slate-900 tracking-tight">{selectedJob.title}</h1>
                    </div>

                    <div className="flex flex-wrap items-center gap-3">
                      <div className="flex flex-wrap items-center gap-2 text-xs font-semibold">
                        {selectedJob.location && (
                          <span className="flex items-center gap-1.5 text-slate-600 bg-slate-50 border border-slate-200 px-2.5 py-1.5 rounded-lg">
                            <MapPin className="w-3.5 h-3.5 text-slate-400" />
                            {selectedJob.location}
                          </span>
                        )}
                        {selectedJob.salaryRange && (
                          <span className="flex items-center gap-1.5 text-emerald-700 bg-emerald-50 border border-emerald-200 px-3 py-1.5 rounded-lg">
                            <DollarSign className="w-3.5 h-3.5" />
                            {selectedJob.salaryRange}
                          </span>
                        )}
                      </div>

                      <button
                        type="button"
                        onClick={() => {
                          setError(null);
                          setIsApplyModalOpen(true);
                        }}
                        className="px-5 py-2 bg-blue-600 hover:bg-blue-700 active:scale-[0.98] text-white rounded-lg font-bold text-xs shadow-xs flex items-center gap-2 cursor-pointer transition-all shrink-0"
                      >
                        <Upload className="w-3.5 h-3.5" />
                        <span>Apply Now</span>
                      </button>
                    </div>
                  </div>

                  {/* Role Description / Overview */}
                  <div>
                    <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider mb-2">Role Overview</h3>
                    <p className="text-xs text-slate-600 leading-relaxed">{selectedJob.description}</p>
                  </div>

                  {/* Key Technical Requirements */}
                  {selectedJob.requirements && (
                    <div>
                      <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider mb-2.5">Key Technical Requirements</h3>
                      <div className="flex flex-wrap gap-1.5">
                        {selectedJob.requirements.split(/[,;|]/).map((req, idx) => {
                          const trimmed = req.trim();
                          if (!trimmed) return null;
                          return (
                            <span 
                              key={idx}
                              className="text-xs bg-slate-100 text-slate-800 border border-slate-200 px-2.5 py-1 rounded font-medium"
                            >
                              {trimmed}
                            </span>
                          );
                        })}
                      </div>
                    </div>
                  )}

                  {/* Core Responsibilities (Dynamic per Job Opening) */}
                  <div className="space-y-2 pt-2 border-t border-slate-100">
                    <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider">Core Responsibilities</h3>
                    <ul className="space-y-1.5 text-xs text-slate-600">
                      {displayResponsibilities.map((resp, idx) => (
                        <li key={idx} className="flex items-start gap-2">
                          <span className="text-blue-600 font-bold">•</span>
                          <span>{resp}</span>
                        </li>
                      ))}
                    </ul>
                  </div>

                  {/* Perks & Benefits Grid (Why Join Nexus Enterprise) */}
                  <div className="space-y-2.5 pt-2 border-t border-slate-100">
                    <h3 className="text-xs font-bold text-slate-700 uppercase tracking-wider">Why Join Nexus Enterprise</h3>
                    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3">
                      <div className="bg-slate-50 border border-slate-200 p-3.5 rounded-xl">
                        <div className="text-xs font-bold text-emerald-700 flex items-center gap-1.5 mb-1">
                          💰 Top Compensation
                        </div>
                        <p className="text-[11px] text-slate-500">Competitive salary benchmarked against top tech tiers.</p>
                      </div>

                      <div className="bg-slate-50 border border-slate-200 p-3.5 rounded-xl">
                        <div className="text-xs font-bold text-blue-700 flex items-center gap-1.5 mb-1">
                          🌐 Remote / Hybrid
                        </div>
                        <p className="text-[11px] text-slate-500">Up to 2 days/week remote + $500 home office stipend.</p>
                      </div>

                      <div className="bg-slate-50 border border-slate-200 p-3.5 rounded-xl">
                        <div className="text-xs font-bold text-purple-700 flex items-center gap-1.5 mb-1">
                          🩺 Health &amp; Wellness
                        </div>
                        <p className="text-[11px] text-slate-500">Comprehensive health, vision, and mental wellness coverage.</p>
                      </div>

                      <div className="bg-slate-50 border border-slate-200 p-3.5 rounded-xl">
                        <div className="text-xs font-bold text-amber-700 flex items-center gap-1.5 mb-1">
                          🚀 Learning &amp; Growth
                        </div>
                        <p className="text-[11px] text-slate-500">$2,500 annual budget for cloud certifications and conferences.</p>
                      </div>
                    </div>
                  </div>

                  {/* Apply Callout Card at Bottom of Job Details */}
                  <div className="pt-4 border-t border-slate-100 flex flex-col sm:flex-row items-center justify-between gap-4 bg-gradient-to-r from-blue-50/80 to-indigo-50/80 p-5 rounded-xl border border-blue-200/80">
                    <div>
                      <h4 className="text-sm font-bold text-slate-900">Ready to join Nexus Enterprise?</h4>
                      <p className="text-xs text-slate-600 mt-0.5">Submit your resume for the {selectedJob.title} position. An official acknowledgment email will be dispatched upon receipt.</p>
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        setError(null);
                        setIsApplyModalOpen(true);
                      }}
                      className="px-6 py-2.5 bg-blue-600 hover:bg-blue-700 active:scale-[0.98] text-white rounded-xl font-bold text-xs shadow-md shadow-blue-500/20 flex items-center gap-2 cursor-pointer transition-all shrink-0"
                    >
                      <Upload className="w-4 h-4" />
                      <span>Apply for this Position</span>
                    </button>
                  </div>
                </div>
              );
            })() : (
              <div className="bg-white rounded-xl border border-slate-200 p-8 text-center text-slate-500">
                No job selected. Please select a job opening from the list above.
              </div>
            )}

            {/* APPLICATION MODAL POPUP */}
            {isApplyModalOpen && selectedJob && (
              <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150">
                <div className="bg-white w-full max-w-2xl rounded-2xl shadow-2xl border border-slate-200 overflow-hidden max-h-[92vh] flex flex-col animate-in zoom-in-95 duration-150">
                  {/* Modal Header */}
                  <div className="px-6 py-4 bg-slate-50 border-b border-slate-200 flex items-center justify-between">
                    <div>
                      <h3 className="text-sm font-bold text-slate-900 flex items-center gap-2">
                        <span>Apply for</span>
                        <span className="text-blue-600">{selectedJob.title}</span>
                      </h3>
                      <p className="text-[11px] text-slate-500">{selectedJob.department} Department • {selectedJob.location || 'Remote / Hybrid'}</p>
                    </div>
                    <button
                      onClick={() => setIsApplyModalOpen(false)}
                      className="p-1.5 text-slate-400 hover:text-slate-700 rounded-lg hover:bg-slate-200/60 transition-colors cursor-pointer"
                    >
                      <X className="w-4 h-4" />
                    </button>
                  </div>

                  {/* Modal Body / Form */}
                  <div className="p-6 overflow-y-auto space-y-5">
                    {error && (
                      <div className="bg-rose-50 border border-rose-200 rounded-xl p-3.5 flex items-center gap-2.5 text-rose-700 text-xs">
                        <AlertCircle className="w-4 h-4 shrink-0 text-rose-600" />
                        <span>{error}</span>
                      </div>
                    )}

                    <form onSubmit={handleSubmit} className="space-y-5">
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <div>
                          <label className="block text-xs font-bold text-slate-700 mb-1.5">
                            Full Legal Name <span className="text-rose-600">*</span>
                          </label>
                          <input
                            type="text"
                            required
                            placeholder="e.g. Ali Raza"
                            value={candidateName}
                            onChange={(e) => setCandidateName(e.target.value)}
                            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2.5 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                          />
                        </div>

                        <div>
                          <label className="block text-xs font-bold text-slate-700 mb-1.5">
                            Email Address <span className="text-rose-600">*</span>
                          </label>
                          <input
                            type="email"
                            required
                            placeholder="e.g. ali.raza@example.com"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2.5 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                          />
                        </div>
                      </div>

                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <div>
                          <label className="block text-xs font-bold text-slate-700 mb-1.5">
                            Phone Number
                          </label>
                          <input
                            type="tel"
                            placeholder="e.g. +92-300-1234567"
                            value={phone}
                            onChange={(e) => setPhone(e.target.value)}
                            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2.5 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                          />
                        </div>

                        <div>
                          <label className="block text-xs font-bold text-slate-700 mb-1.5">
                            Relevant Experience (Years) <span className="text-rose-600">*</span>
                          </label>
                          <input
                            type="number"
                            min="0"
                            max="40"
                            placeholder="e.g. 4"
                            value={experienceYears}
                            onChange={(e) => setExperienceYears(e.target.value === '' ? '' : Number(e.target.value))}
                            className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2.5 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                          />
                        </div>
                      </div>

                      <div>
                        <label className="block text-xs font-bold text-slate-700 mb-1.5">
                          Cover Note / Brief Statement
                        </label>
                        <textarea
                          rows={2}
                          placeholder="Briefly state your accomplishments and why you are interested in this position..."
                          value={coverNote}
                          onChange={(e) => setCoverNote(e.target.value)}
                          className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors resize-none"
                        />
                      </div>

                      {/* PDF-ONLY ATTACHMENT (NO PASTE TEXT) */}
                      <div>
                        <label className="block text-xs font-bold text-slate-700 mb-1.5">
                          Attach Curriculum Vitae / Resume (PDF Document Only) <span className="text-rose-600">*</span>
                        </label>

                        {cvPdfDataUrl ? (
                          <div className="bg-emerald-50/70 border border-emerald-300 rounded-xl p-4 flex items-center justify-between gap-4">
                            <div className="flex items-center gap-3">
                              <div className="w-10 h-10 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold text-xs">
                                PDF
                              </div>
                              <div>
                                <div className="text-xs font-bold text-slate-900 flex items-center gap-1.5">
                                  <span>{cvFileName}</span>
                                  <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                                </div>
                                <p className="text-[11px] text-emerald-800">
                                  PDF attached and ready for neural extraction ({cvPdfFile ? (cvPdfFile.size / 1024).toFixed(1) + ' KB' : 'Loaded'})
                                </p>
                              </div>
                            </div>

                            <button
                              type="button"
                              onClick={handleRemovePdf}
                              className="text-xs text-rose-600 hover:text-rose-700 hover:bg-rose-100/60 px-3 py-1.5 rounded-lg font-semibold transition-colors cursor-pointer flex items-center gap-1"
                            >
                              <X className="w-3.5 h-3.5" />
                              <span>Remove</span>
                            </button>
                          </div>
                        ) : (
                          <div 
                            onDragOver={handleDragOver}
                            onDrop={handleDrop}
                            className="border-2 border-dashed border-slate-300 hover:border-blue-500 rounded-xl p-8 text-center bg-slate-50 hover:bg-white transition-all cursor-pointer"
                          >
                            <input
                              type="file"
                              id="portalPdfInput"
                              onChange={handleFileChange}
                              accept=".pdf,application/pdf"
                              className="hidden"
                            />
                            <label htmlFor="portalPdfInput" className="cursor-pointer block space-y-2">
                              <div className="w-12 h-12 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center mx-auto">
                                <Upload className="w-6 h-6" />
                              </div>
                              <div className="text-xs font-bold text-slate-800">
                                Click to attach or drag &amp; drop candidate PDF document
                              </div>
                              <p className="text-[11px] text-slate-500">
                                Accepted format: <strong>.pdf only</strong> (Max 15MB). The AI agent will automatically extract all words.
                              </p>
                            </label>
                          </div>
                        )}
                      </div>

                      <div className="pt-3 flex justify-end gap-3 border-t border-slate-100">
                        <button
                          type="button"
                          onClick={() => setIsApplyModalOpen(false)}
                          className="px-4 py-2.5 rounded-lg border border-slate-200 text-slate-700 hover:bg-slate-100 text-xs font-semibold cursor-pointer"
                        >
                          Cancel
                        </button>
                        <button
                          type="submit"
                          disabled={submitting || !cvPdfDataUrl}
                          className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-7 py-2.5 rounded-lg shadow-xs transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          {submitting ? 'Submitting Application...' : 'Submit Application'}
                        </button>
                      </div>
                    </form>
                  </div>
                </div>
              </div>
            )}

          </div>
        )}
      </main>
    </div>
  );
};
