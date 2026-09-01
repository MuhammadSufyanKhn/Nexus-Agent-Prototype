import React, { useState, useEffect } from 'react';
import { 
  Briefcase, 
  Plus, 
  FileText, 
  ExternalLink, 
  Copy, 
  Check, 
  Sparkles, 
  MapPin, 
  DollarSign, 
  ChevronDown, 
  ChevronUp, 
  Trash2, 
  Search, 
  Filter, 
  AlertCircle, 
  TrendingUp, 
  RefreshCw, 
  Star,
  Calendar,
  Mail,
  BookmarkCheck,
  CheckCircle2
} from 'lucide-react';
import { 
  fetchJobOpenings, 
  fetchCandidateApplications,
  createJobOpening, 
  deleteJobOpening,
  shortlistCandidate,
  sendInterviewInvitation
} from '../services/api';
import type { JobOpening, CandidateApplication } from '../services/api';

interface JobOpeningsViewProps {
  onScreenCandidate?: (jobId: number, candidateId?: number) => void;
  onOpenCandidatePortal?: (jobId: number) => void;
}

export const JobOpeningsView: React.FC<JobOpeningsViewProps> = ({
  onScreenCandidate,
  onOpenCandidatePortal
}) => {
  const [jobOpenings, setJobOpenings] = useState<JobOpening[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [deptFilter, setDeptFilter] = useState('ALL');
  const [sortBy, setSortBy] = useState<'newest' | 'applicants' | 'title'>('newest');
  
  // Expanded job applicants accordion
  const [expandedJobId, setExpandedJobId] = useState<number | null>(null);
  const [jobApplications, setJobApplications] = useState<Record<number, CandidateApplication[]>>({});
  const [loadingApps, setLoadingApps] = useState<Record<number, boolean>>({});
  const [copiedJobId, setCopiedJobId] = useState<number | null>(null);

  // Create Job Modal state — ZERO DEFAULT PRE-FILLED VALUES
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newTitle, setNewTitle] = useState('');
  const [newDepartment, setNewDepartment] = useState('');
  const [newRequirements, setNewRequirements] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [newSalary, setNewSalary] = useState('');
  const [newLocation, setNewLocation] = useState('');
  const [newResponsibilities, setNewResponsibilities] = useState('');
  const [creating, setCreating] = useState(false);

  // Interview Invitation Modal state
  const [interviewModalOpen, setInterviewModalOpen] = useState(false);
  const [selectedInterviewJob, setSelectedInterviewJob] = useState<JobOpening | null>(null);
  const [selectedInterviewCandidate, setSelectedInterviewCandidate] = useState<CandidateApplication | null>(null);
  const [interviewDate, setInterviewDate] = useState('');
  const [interviewTime, setInterviewTime] = useState('11:00 AM PKT');
  const [interviewMode, setInterviewMode] = useState<'Online' | 'Onsite'>('Online');
  const [interviewLocationOrLink, setInterviewLocationOrLink] = useState('https://meet.google.com/nex-us-rec');
  const [interviewNotes, setInterviewNotes] = useState('');
  const [sendingInvitation, setSendingInvitation] = useState(false);
  const [invitationSuccessMsg, setInvitationSuccessMsg] = useState<string | null>(null);

  useEffect(() => {
    loadOpenings();
  }, []);

  const loadOpenings = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchJobOpenings();
      setJobOpenings(data);
      const appMap: Record<number, CandidateApplication[]> = {};
      data.forEach(j => {
        if (j.applications) {
          appMap[j.id] = j.applications;
        }
      });
      setJobApplications(prev => ({ ...appMap, ...prev }));
    } catch (err: any) {
      setError(err.message || 'Failed to load job openings.');
    } finally {
      setLoading(false);
    }
  };

  const loadApplicationsForJob = async (jobId: number) => {
    if (jobApplications[jobId] && jobApplications[jobId].length > 0) return;
    setLoadingApps(prev => ({ ...prev, [jobId]: true }));
    try {
      const apps = await fetchCandidateApplications(jobId);
      setJobApplications(prev => ({ ...prev, [jobId]: apps }));
    } catch (err) {
      console.error('Failed to load applications for job', jobId, err);
    } finally {
      setLoadingApps(prev => ({ ...prev, [jobId]: false }));
    }
  };

  const handleToggleApplicants = (jobId: number) => {
    if (expandedJobId === jobId) {
      setExpandedJobId(null);
    } else {
      setExpandedJobId(jobId);
      loadApplicationsForJob(jobId);
    }
  };

  const handleOpenInterviewModal = (job: JobOpening, candidate: CandidateApplication) => {
    setSelectedInterviewJob(job);
    setSelectedInterviewCandidate(candidate);
    const d = new Date();
    d.setDate(d.getDate() + 3);
    const dateStr = d.toLocaleDateString('en-US', { month: 'long', day: 'numeric', year: 'numeric' });
    setInterviewDate(dateStr);
    setInterviewTime('11:00 AM PKT');
    setInterviewMode('Online');
    setInterviewLocationOrLink('https://meet.google.com/nex-us-rec');
    setInterviewNotes('');
    setInvitationSuccessMsg(null);
    setInterviewModalOpen(true);
  };

  const handleSendInterviewInvitation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedInterviewCandidate) return;

    setSendingInvitation(true);
    setError(null);
    try {
      const res = await sendInterviewInvitation(selectedInterviewCandidate.id, {
        interviewDate,
        interviewTime,
        mode: interviewMode,
        locationOrLink: interviewLocationOrLink,
        notes: interviewNotes
      });

      setInvitationSuccessMsg(res.message);
      await loadOpenings();
      setTimeout(() => {
        setInterviewModalOpen(false);
        setInvitationSuccessMsg(null);
      }, 1800);
    } catch (err: any) {
      setError(err.message || 'Failed to dispatch interview invitation.');
    } finally {
      setSendingInvitation(false);
    }
  };

  const handleShortlistDirect = async (applicationId: number, jobId: number) => {
    try {
      await shortlistCandidate(applicationId);
      await loadOpenings();
      const freshApps = await fetchCandidateApplications(jobId);
      setJobApplications(prev => ({ ...prev, [jobId]: freshApps }));
    } catch (err: any) {
      alert(err.message || 'Failed to shortlist candidate.');
    }
  };

  const handleCreateJob = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim()) return;

    setCreating(true);
    try {
      await createJobOpening({
        title: newTitle.trim(),
        department: newDepartment.trim() || undefined,
        requirements: newRequirements.trim() || undefined,
        description: newDescription.trim() || undefined,
        responsibilities: newResponsibilities.trim() || undefined,
        salaryRange: newSalary.trim() || undefined,
        location: newLocation.trim() || undefined
      });

      setShowCreateModal(false);
      setNewTitle('');
      setNewDepartment('');
      setNewRequirements('');
      setNewDescription('');
      setNewResponsibilities('');
      setNewSalary('');
      setNewLocation('');
      await loadOpenings();
    } catch (err: any) {
      alert(err.message || 'Failed to create job opening.');
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id: number, title: string) => {
    if (!window.confirm(`Are you sure you want to close and remove the job opening '${title}'?`)) return;
    try {
      await deleteJobOpening(id);
      await loadOpenings();
    } catch (err: any) {
      alert('Failed to delete job opening.');
    }
  };

  const getApplicationLink = (jobId: number) => {
    const isStandardPort = window.location.port === '3000' || window.location.port === '5173';
    if (isStandardPort) {
      return `http://localhost:3001/?jobId=${jobId}`;
    }
    return `${window.location.origin}/?portal=candidate&jobId=${jobId}`;
  };

  const handleCopyLink = (jobId: number) => {
    const link = getApplicationLink(jobId);
    navigator.clipboard.writeText(link);
    setCopiedJobId(jobId);
    setTimeout(() => setCopiedJobId(null), 2500);
  };

  const handleOpenCandidatePortal = (jobId: number) => {
    if (onOpenCandidatePortal) {
      onOpenCandidatePortal(jobId);
    } else {
      const link = getApplicationLink(jobId);
      window.open(link, '_blank');
    }
  };

  // Filter and sort jobs
  const filteredJobs = jobOpenings.filter(j => {
    const matchesSearch = j.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          (j.department && j.department.toLowerCase().includes(searchQuery.toLowerCase())) ||
                          (j.requirements && j.requirements.toLowerCase().includes(searchQuery.toLowerCase()));
    const matchesDept = deptFilter === 'ALL' || (j.department && j.department.toLowerCase() === deptFilter.toLowerCase());
    return matchesSearch && matchesDept;
  }).sort((a, b) => {
    if (sortBy === 'applicants') return (b.applicationsCount || 0) - (a.applicationsCount || 0);
    if (sortBy === 'title') return a.title.localeCompare(b.title);
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
  });

  const totalApplications = jobOpenings.reduce((acc, curr) => acc + (curr.applicationsCount || 0), 0);
  const departments = Array.from(new Set(jobOpenings.map(j => j.department).filter(Boolean)));

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6">
      {/* Top Header Card (HR Portal Theme) */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 bg-white p-4 rounded-xl border border-slate-200 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
            <Briefcase className="w-5 h-5" />
          </div>
          <div>
            <h3 className="text-sm font-bold text-slate-900">Job Requisitions &amp; Pipeline ({jobOpenings.length})</h3>
            <p className="text-xs text-slate-500">Corporate job postings, public candidate application links, and resume counters.</p>
          </div>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={loadOpenings}
            className="p-2 text-slate-500 hover:text-slate-700 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg transition-colors cursor-pointer"
            title="Refresh Openings"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>

          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-4 py-2 rounded-lg shadow-xs transition-colors cursor-pointer"
          >
            <Plus className="w-4 h-4" />
            <span>Create Job Opening</span>
          </button>
        </div>
      </div>

      {error && (
        <div className="bg-rose-50 border border-rose-200 rounded-xl p-3.5 flex items-center gap-2.5 text-rose-700 text-xs">
          <AlertCircle className="w-4 h-4 shrink-0 text-rose-600" />
          <span>{error}</span>
        </div>
      )}

      {/* KPI Stats Grid (HR Portal Theme) */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
          <div className="text-xs text-slate-500 font-medium mb-1">Active Roles</div>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900">{jobOpenings.length}</span>
            <span className="text-[11px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
              Live
            </span>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
          <div className="text-xs text-slate-500 font-medium mb-1">Submitted Resumes</div>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold text-blue-600">{totalApplications}</span>
            <span className="text-[11px] text-slate-400">Stored in SQL DB</span>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
          <div className="text-xs text-slate-500 font-medium mb-1">Hiring Units</div>
          <div className="flex items-baseline gap-2">
            <span className="text-2xl font-bold text-slate-900">{departments.length}</span>
            <span className="text-[11px] text-slate-400">Departments</span>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
          <div className="text-xs text-slate-500 font-medium mb-1">AI Match Engine</div>
          <div className="flex items-baseline gap-2">
            <span className="text-base font-bold text-emerald-600">Online</span>
            <span className="text-[11px] text-slate-400">Gemini 2.5</span>
          </div>
        </div>
      </div>

      {/* Search & Filter Controls */}
      <div className="flex flex-col sm:flex-row items-center gap-3 bg-white border border-slate-200 rounded-xl p-3 shadow-2xs">
        <div className="relative flex-1 w-full">
          <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Search openings by title, department, or required skills..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full bg-slate-50 border border-slate-200 rounded-lg pl-9 pr-8 py-2 text-xs text-slate-900 placeholder-slate-400 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
          />
          {searchQuery && (
            <button
              onClick={() => setSearchQuery('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs p-1"
            >
              ✕
            </button>
          )}
        </div>

        <div className="flex items-center gap-2.5 w-full sm:w-auto">
          <div className="flex items-center gap-1.5 bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1.5">
            <Filter className="w-3.5 h-3.5 text-slate-400 shrink-0" />
            <select
              value={deptFilter}
              onChange={(e) => setDeptFilter(e.target.value)}
              className="bg-transparent text-xs text-slate-700 focus:outline-none cursor-pointer pr-1"
            >
              <option value="ALL">All Departments</option>
              {departments.map(d => (
                <option key={d} value={d}>{d}</option>
              ))}
            </select>
          </div>

          <div className="flex items-center gap-1.5 bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1.5">
            <TrendingUp className="w-3.5 h-3.5 text-slate-400 shrink-0" />
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as any)}
              className="bg-transparent text-xs text-slate-700 focus:outline-none cursor-pointer pr-1"
            >
              <option value="newest">Newest</option>
              <option value="applicants">Most Resumes</option>
              <option value="title">Title A-Z</option>
            </select>
          </div>
        </div>
      </div>

      {/* Job Openings Cards List */}
      {loading ? (
        <div className="bg-white rounded-xl border border-slate-200 p-8 text-center text-xs text-slate-400 shadow-2xs">
          Loading job openings from database...
        </div>
      ) : filteredJobs.length === 0 ? (
        <div className="bg-white rounded-xl border border-slate-200 p-8 text-center shadow-2xs space-y-3">
          <Briefcase className="w-10 h-10 text-slate-300 mx-auto" />
          <h4 className="text-sm font-bold text-slate-800">No Job Openings Found</h4>
          <p className="text-xs text-slate-500 max-w-sm mx-auto">
            {searchQuery || deptFilter !== 'ALL'
              ? 'Try clearing your search filters to view all openings.'
              : 'No job requisitions are active yet. Click below to create one.'}
          </p>
          <button
            onClick={() => setShowCreateModal(true)}
            className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-4 py-2 rounded-lg"
          >
            Create Job Opening
          </button>
        </div>
      ) : (
        <div className="space-y-4">
          {filteredJobs.map((job) => {
            const isExpanded = expandedJobId === job.id;
            const applicants = jobApplications[job.id] || job.applications || [];
            const appCount = job.applicationsCount || applicants.length || 0;
            const shortlistedApplicants = applicants.filter(a => a.status === 'Shortlisted' || a.status === 'Interview Scheduled');
            const isLoadingApplicants = loadingApps[job.id] || false;
            const link = getApplicationLink(job.id);

            return (
              <div 
                key={job.id}
                className="bg-white rounded-xl border border-slate-200 p-5 shadow-2xs space-y-4"
              >
                {/* Header Row */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] font-bold text-blue-700 bg-blue-50 border border-blue-200 px-2 py-0.5 rounded">
                        {job.department || 'General'}
                      </span>
                      <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 px-2 py-0.5 rounded flex items-center gap-1">
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
                        {job.status || 'Active'}
                      </span>
                      <span className="text-[11px] text-slate-400">
                        {new Date(job.createdAt).toLocaleDateString()}
                      </span>
                    </div>

                    <h4 className="font-bold text-slate-900 text-base">{job.title}</h4>

                    <div className="flex flex-wrap items-center gap-3 text-xs text-slate-500">
                      {job.location && (
                        <span className="flex items-center gap-1">
                          <MapPin className="w-3.5 h-3.5 text-slate-400" />
                          {job.location}
                        </span>
                      )}
                      {job.salaryRange && (
                        <span className="flex items-center gap-1 text-emerald-700 font-semibold">
                          <DollarSign className="w-3.5 h-3.5" />
                          {job.salaryRange}
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Actions & CV Counter Badge */}
                  <div className="flex items-center gap-3 shrink-0">
                    <div className="bg-slate-50 border border-slate-200 px-3 py-1.5 rounded-lg flex items-center gap-2">
                      <FileText className="w-4 h-4 text-blue-600" />
                      <span className="text-xs font-bold text-slate-800">
                        {appCount} {appCount === 1 ? 'Resume' : 'Resumes'}
                      </span>
                    </div>

                    <button
                      onClick={() => onScreenCandidate ? onScreenCandidate(job.id) : null}
                      className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-3.5 py-1.5 rounded-lg flex items-center gap-1.5 shadow-2xs transition-colors cursor-pointer"
                    >
                      <Sparkles className="w-3.5 h-3.5 text-amber-300" />
                      <span>Screen in CV Tab</span>
                    </button>

                    <button
                      onClick={() => handleDelete(job.id, job.title)}
                      className="p-1.5 text-slate-400 hover:text-rose-600 bg-slate-50 hover:bg-rose-50 border border-slate-200 rounded-lg transition-colors cursor-pointer"
                      title="Delete Requisition"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                </div>

                {/* Description */}
                {job.description && (
                  <p className="text-xs text-slate-600 leading-relaxed">
                    {job.description}
                  </p>
                )}

                {/* Core Responsibilities */}
                {job.responsibilities && (
                  <div className="bg-slate-50/80 border border-slate-200/80 rounded-lg p-2.5 space-y-1">
                    <span className="text-[10px] font-bold text-slate-500 uppercase tracking-wider block">Core Responsibilities</span>
                    <ul className="text-xs text-slate-600 space-y-0.5 pl-1">
                      {job.responsibilities.split(/[\n•;]+/).map(r => r.trim()).filter(Boolean).map((resp, idx) => (
                        <li key={idx} className="flex items-start gap-1.5">
                          <span className="text-blue-600 font-bold">•</span>
                          <span>{resp}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {/* Requirements Pills */}
                {job.requirements && (
                  <div className="flex flex-wrap items-center gap-1.5">
                    {job.requirements.split(/[,;|]/).map((req, idx) => {
                      const trimmed = req.trim();
                      if (!trimmed) return null;
                      return (
                        <span 
                          key={idx}
                          className="text-[11px] bg-slate-100 text-slate-700 border border-slate-200 px-2 py-0.5 rounded font-medium"
                        >
                          {trimmed}
                        </span>
                      );
                    })}
                  </div>
                )}

                {/* Public Candidate Portal Link Bar */}
                <div className="bg-slate-50 border border-slate-200 rounded-lg p-2.5 flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
                  <div className="flex items-center gap-2 overflow-hidden text-xs text-slate-600">
                    <span className="font-semibold text-slate-800 shrink-0">Candidate Link:</span>
                    <span className="font-mono text-blue-700 text-[11px] truncate select-all bg-white px-2 py-0.5 rounded border border-slate-200">
                      {link}
                    </span>
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    <button
                      onClick={() => handleCopyLink(job.id)}
                      className="flex items-center gap-1 text-xs font-semibold text-slate-700 bg-white hover:bg-slate-100 px-2.5 py-1 rounded border border-slate-200 transition-colors cursor-pointer"
                    >
                      {copiedJobId === job.id ? (
                        <>
                          <Check className="w-3.5 h-3.5 text-emerald-600" />
                          <span className="text-emerald-700 font-bold">Copied!</span>
                        </>
                      ) : (
                        <>
                          <Copy className="w-3.5 h-3.5" />
                          <span>Copy Link</span>
                        </>
                      )}
                    </button>

                    <button
                      onClick={() => handleOpenCandidatePortal(job.id)}
                      className="flex items-center gap-1 text-xs font-bold text-white bg-slate-800 hover:bg-slate-900 px-3 py-1 rounded transition-colors cursor-pointer"
                    >
                      <ExternalLink className="w-3.5 h-3.5" />
                      <span>Open Candidate Portal</span>
                    </button>
                  </div>
                </div>

                {/* Shortlisted Candidates for Interview */}
                {shortlistedApplicants.length > 0 && (
                  <div className="bg-emerald-50/70 border border-emerald-200/90 rounded-xl p-4 space-y-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <BookmarkCheck className="w-4 h-4 text-emerald-700" />
                        <span className="text-xs font-bold text-emerald-950">
                          Shortlisted Candidates for Interview ({shortlistedApplicants.length})
                        </span>
                      </div>
                      <span className="text-[10px] font-bold bg-emerald-100 text-emerald-800 px-2.5 py-0.5 rounded-full border border-emerald-300">
                        Interview Pipeline
                      </span>
                    </div>

                    <div className="space-y-2">
                      {shortlistedApplicants.map((cand) => (
                        <div 
                          key={cand.id} 
                          className="bg-white border border-emerald-200 rounded-lg p-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3 shadow-2xs"
                        >
                          <div className="flex items-center gap-2.5">
                            <div className="w-8 h-8 rounded-full bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-xs">
                              {cand.candidateName ? cand.candidateName.charAt(0).toUpperCase() : 'C'}
                            </div>
                            <div>
                              <div className="flex items-center gap-2 flex-wrap">
                                <span className="text-xs font-bold text-slate-900">{cand.candidateName}</span>
                                <span className={`text-[10px] px-2 py-0.5 rounded-full font-bold border ${cand.status === 'Interview Scheduled' ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-emerald-50 text-emerald-800 border-emerald-200'}`}>
                                  {cand.status === 'Interview Scheduled' ? '🗓️ Interview Scheduled' : '⭐ Shortlisted'}
                                </span>
                                {cand.fitScore && (
                                  <span className="text-[10px] bg-amber-50 text-amber-800 border border-amber-200 px-1.5 py-0.5 rounded font-bold">
                                    {cand.fitScore}% Fit
                                  </span>
                                )}
                              </div>
                              <div className="text-[11px] text-slate-500">
                                {cand.email} • {cand.experienceYears} Years Exp • Submitted {new Date(cand.submittedAt).toLocaleDateString()}
                              </div>
                            </div>
                          </div>

                          <button
                            onClick={() => handleOpenInterviewModal(job, cand)}
                            className="text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 active:scale-[0.98] px-3.5 py-1.5 rounded-lg flex items-center gap-1.5 shadow-2xs transition-all cursor-pointer self-end sm:self-auto"
                          >
                            <Calendar className="w-3.5 h-3.5" />
                            <span>{cand.status === 'Interview Scheduled' ? 'Reschedule / Resend Email' : 'Send Interview Invitation'}</span>
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Toggle Applicants Drawer */}
                <div className="flex justify-between items-center pt-2 border-t border-slate-100 text-xs">
                  <span className="text-slate-500">
                    {appCount === 0 ? 'No candidates have submitted yet' : `${appCount} applicant(s) stored in database`}
                  </span>

                  <button
                    onClick={() => handleToggleApplicants(job.id)}
                    className="font-bold text-blue-600 hover:text-blue-700 flex items-center gap-1 transition-colors cursor-pointer"
                  >
                    <span>{isExpanded ? 'Hide' : 'View'} Submitted Candidates ({appCount})</span>
                    {isExpanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
                  </button>
                </div>

                {/* Applicants Drawer Content */}
                {isExpanded && (
                  <div className="pt-2 space-y-2 animate-fade-in">
                    {isLoadingApplicants ? (
                      <div className="text-center py-4 text-slate-400 text-xs flex items-center justify-center gap-2">
                        <RefreshCw className="w-3.5 h-3.5 animate-spin text-blue-600" />
                        <span>Loading applicant profiles...</span>
                      </div>
                    ) : applicants.length === 0 ? (
                      <div className="text-center py-4 bg-slate-50 rounded-lg border border-slate-200 text-xs text-slate-500">
                        No applications submitted for this role yet.
                      </div>
                    ) : (
                      <div className="space-y-2">
                        {applicants.map((app) => (
                          <div 
                            key={app.id}
                            className="bg-slate-50 border border-slate-200 rounded-lg p-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3"
                          >
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-700 flex items-center justify-center font-bold text-xs">
                                {app.candidateName ? app.candidateName.charAt(0).toUpperCase() : 'C'}
                              </div>
                              <div>
                                <div className="flex items-center gap-2 flex-wrap">
                                  <span className="text-xs font-bold text-slate-900">{app.candidateName}</span>
                                  <span className={`text-[10px] px-2 py-0.5 rounded-full font-bold border ${app.status === 'Interview Scheduled' ? 'bg-blue-50 text-blue-700 border-blue-200' : app.status === 'Shortlisted' ? 'bg-emerald-50 text-emerald-800 border-emerald-200' : 'bg-slate-100 text-slate-600 border-slate-200'}`}>
                                    {app.status === 'Interview Scheduled' ? '🗓️ Interview Scheduled' : app.status === 'Shortlisted' ? '⭐ Shortlisted' : app.status || 'In Progress'}
                                  </span>
                                  {app.fitScore && (
                                    <span className="text-[10px] bg-emerald-50 text-emerald-800 border border-emerald-200 px-1.5 py-0.2 rounded font-bold flex items-center gap-1">
                                      <Star className="w-2.5 h-2.5 fill-current text-amber-500" />
                                      {app.fitScore}% Fit
                                    </span>
                                  )}
                                </div>
                                <div className="text-[11px] text-slate-500">
                                  {app.email} • {app.experienceYears} Years Exp • {new Date(app.submittedAt).toLocaleDateString()}
                                </div>
                              </div>
                            </div>

                            <div className="flex items-center gap-2 self-end sm:self-auto flex-wrap">
                              {app.status !== 'Shortlisted' && app.status !== 'Interview Scheduled' && (
                                <button
                                  onClick={() => handleShortlistDirect(app.id, job.id)}
                                  className="text-xs font-semibold text-emerald-700 bg-emerald-50 hover:bg-emerald-100 border border-emerald-200 px-2.5 py-1.5 rounded-lg flex items-center gap-1 transition-colors cursor-pointer"
                                  title="Shortlist Candidate for Interview"
                                >
                                  <BookmarkCheck className="w-3.5 h-3.5 text-emerald-600" />
                                  <span>Shortlist</span>
                                </button>
                              )}

                              {(app.status === 'Shortlisted' || app.status === 'Interview Scheduled') && (
                                <button
                                  onClick={() => handleOpenInterviewModal(job, app)}
                                  className="text-xs font-bold text-white bg-indigo-600 hover:bg-indigo-700 px-3 py-1.5 rounded-lg flex items-center gap-1.5 shadow-2xs transition-colors cursor-pointer"
                                >
                                  <Calendar className="w-3.5 h-3.5" />
                                  <span>{app.status === 'Interview Scheduled' ? 'Reschedule' : 'Invite'}</span>
                                </button>
                              )}

                              <button
                                onClick={() => onScreenCandidate ? onScreenCandidate(job.id, app.id) : null}
                                className="text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 px-3 py-1.5 rounded-lg flex items-center gap-1.5 shadow-2xs transition-colors cursor-pointer"
                              >
                                <Sparkles className="w-3.5 h-3.5 text-amber-300" />
                                <span>Screen in CV Tab</span>
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Create Job Opening Modal (HR Portal Theme) */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-xl border border-slate-200 w-full max-w-lg p-6 shadow-xl space-y-4 animate-scale-up">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
                  <Briefcase className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900">Create New Job Opening</h3>
                  <p className="text-xs text-slate-500">Post a new position into SQL Server database</p>
                </div>
              </div>
              <button
                onClick={() => setShowCreateModal(false)}
                className="w-7 h-7 rounded-lg bg-slate-100 text-slate-500 hover:text-slate-900 flex items-center justify-center text-xs cursor-pointer"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleCreateJob} className="space-y-3.5">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Job Title <span className="text-rose-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Senior Full Stack Developer"
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                />
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Department
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. IT, DevOps, Engineering, Finance"
                    value={newDepartment}
                    onChange={(e) => setNewDepartment(e.target.value)}
                    className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Salary Range
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. $80,000 - $110,000"
                    value={newSalary}
                    onChange={(e) => setNewSalary(e.target.value)}
                    className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Location
                </label>
                <input
                  type="text"
                  placeholder="e.g. Remote / Hybrid, San Francisco, London"
                  value={newLocation}
                  onChange={(e) => setNewLocation(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Technical Requirements (comma-separated) <span className="text-rose-600">*</span>
                </label>
                <textarea
                  rows={2}
                  required
                  placeholder="e.g. React, C#, .NET Core, SQL Server, TypeScript, REST APIs"
                  value={newRequirements}
                  onChange={(e) => setNewRequirements(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors resize-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Role Overview
                </label>
                <textarea
                  rows={2}
                  placeholder="Describe role scope and mission overview..."
                  value={newDescription}
                  onChange={(e) => setNewDescription(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors resize-none"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Core Responsibilities (bullet or newline separated)
                </label>
                <textarea
                  rows={3}
                  placeholder="e.g. Design scalable systems. • Collaborate across engineering pods. • Champion automated testing."
                  value={newResponsibilities}
                  onChange={(e) => setNewResponsibilities(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg px-3.5 py-2 text-xs text-slate-900 focus:bg-white focus:outline-none focus:border-blue-500 transition-colors resize-none"
                />
              </div>

              <div className="flex justify-end gap-2.5 pt-2 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 rounded-lg text-xs font-semibold text-slate-600 hover:text-slate-900 bg-slate-100 hover:bg-slate-200 transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={creating}
                  className="px-5 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer disabled:opacity-50"
                >
                  {creating ? 'Saving...' : 'Save & Publish Opening'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Schedule Interview Modal */}
      {interviewModalOpen && selectedInterviewCandidate && selectedInterviewJob && (
        <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-xl border border-slate-200 w-full max-w-lg p-6 shadow-2xl space-y-4 animate-scale-up">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="p-2 bg-indigo-50 text-indigo-600 rounded-lg">
                  <Calendar className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900">Schedule Candidate Interview</h3>
                  <p className="text-xs text-slate-500">Dispatch official invitation email from nexusagent.notifications@gmail.com</p>
                </div>
              </div>
              <button
                onClick={() => setInterviewModalOpen(false)}
                className="w-7 h-7 rounded-lg bg-slate-100 text-slate-500 hover:text-slate-900 flex items-center justify-center text-xs cursor-pointer"
              >
                ✕
              </button>
            </div>

            {invitationSuccessMsg && (
              <div className="p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg text-xs font-semibold flex items-center gap-2">
                <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                <span>{invitationSuccessMsg}</span>
              </div>
            )}

            <form onSubmit={handleSendInterviewInvitation} className="space-y-3.5 text-xs text-slate-700">
              {/* Candidate & Position Banner */}
              <div className="p-3 bg-slate-50 border border-slate-200 rounded-lg space-y-1 text-slate-600">
                <div className="flex justify-between">
                  <span className="text-slate-400">Candidate:</span>
                  <span className="font-bold text-slate-900">{selectedInterviewCandidate.candidateName}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Recipient Email:</span>
                  <span className="font-mono text-blue-700">{selectedInterviewCandidate.email}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-400">Target Requisition:</span>
                  <span className="font-bold text-slate-800">{selectedInterviewJob.title} ({selectedInterviewJob.department})</span>
                </div>
              </div>

              {/* Date & Time */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div className="space-y-1">
                  <label className="font-semibold text-slate-800">Interview Date</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. September 5, 2026"
                    value={interviewDate}
                    onChange={(e) => setInterviewDate(e.target.value)}
                    className="w-full bg-slate-50 border border-slate-200 rounded-lg p-2.5 text-xs focus:bg-white focus:outline-none focus:border-indigo-500"
                  />
                </div>
                <div className="space-y-1">
                  <label className="font-semibold text-slate-800">Interview Time</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. 11:00 AM PKT"
                    value={interviewTime}
                    onChange={(e) => setInterviewTime(e.target.value)}
                    className="w-full bg-slate-50 border border-slate-200 rounded-lg p-2.5 text-xs focus:bg-white focus:outline-none focus:border-indigo-500"
                  />
                </div>
              </div>

              {/* Mode: Online / Onsite */}
              <div className="space-y-1.5">
                <label className="font-semibold text-slate-800">Interview Mode</label>
                <div className="grid grid-cols-2 gap-2.5">
                  <label className={`flex items-center gap-2 p-2.5 rounded-lg border cursor-pointer transition-all ${interviewMode === 'Online' ? 'bg-indigo-50/70 border-indigo-300 text-indigo-900 font-bold' : 'bg-slate-50 border-slate-200 text-slate-600'}`}>
                    <input
                      type="radio"
                      name="interviewMode"
                      value="Online"
                      checked={interviewMode === 'Online'}
                      onChange={() => {
                        setInterviewMode('Online');
                        setInterviewLocationOrLink('https://meet.google.com/nex-us-rec');
                      }}
                      className="accent-indigo-600"
                    />
                    <span>Online (Virtual Video)</span>
                  </label>
                  <label className={`flex items-center gap-2 p-2.5 rounded-lg border cursor-pointer transition-all ${interviewMode === 'Onsite' ? 'bg-indigo-50/70 border-indigo-300 text-indigo-900 font-bold' : 'bg-slate-50 border-slate-200 text-slate-600'}`}>
                    <input
                      type="radio"
                      name="interviewMode"
                      value="Onsite"
                      checked={interviewMode === 'Onsite'}
                      onChange={() => {
                        setInterviewMode('Onsite');
                        setInterviewLocationOrLink('Nexus Enterprise Tech Tower, Level 4, IT Wing');
                      }}
                      className="accent-indigo-600"
                    />
                    <span>Onsite (In-Person Office)</span>
                  </label>
                </div>
              </div>

              {/* Location or Link */}
              <div className="space-y-1">
                <label className="font-semibold text-slate-800">
                  {interviewMode === 'Online' ? 'Google Meet / Video Conference Link' : 'Office Location Address'}
                </label>
                <input
                  type="text"
                  required
                  value={interviewLocationOrLink}
                  onChange={(e) => setInterviewLocationOrLink(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg p-2.5 text-xs focus:bg-white focus:outline-none focus:border-indigo-500 font-mono"
                />
              </div>

              {/* Additional Notes */}
              <div className="space-y-1">
                <label className="font-semibold text-slate-800">Instructions / Notes to Candidate (Optional)</label>
                <textarea
                  rows={2}
                  placeholder="e.g. Please be prepared to discuss recent architecture decisions and code samples."
                  value={interviewNotes}
                  onChange={(e) => setInterviewNotes(e.target.value)}
                  className="w-full bg-slate-50 border border-slate-200 rounded-lg p-2.5 text-xs focus:bg-white focus:outline-none focus:border-indigo-500"
                />
              </div>

              {/* Dispatch Alert */}
              <div className="p-2.5 bg-blue-50 border border-blue-200 rounded-lg text-[11px] text-blue-900 flex items-center gap-2">
                <Mail className="w-4 h-4 text-blue-600 shrink-0" />
                <span>Automatic official invitation will be sent to <strong>{selectedInterviewCandidate.email}</strong> from <strong>nexusagent.notifications@gmail.com</strong>.</span>
              </div>

              {/* Action Buttons */}
              <div className="flex justify-end gap-2 pt-2 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setInterviewModalOpen(false)}
                  className="px-4 py-2 border border-slate-200 rounded-lg text-slate-600 hover:bg-slate-50 font-medium cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={sendingInvitation}
                  className="px-5 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-bold rounded-lg shadow-xs flex items-center gap-2 disabled:opacity-50 transition-all cursor-pointer"
                >
                  {sendingInvitation ? (
                    <>
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      <span>Sending Invitation Email...</span>
                    </>
                  ) : (
                    <>
                      <Mail className="w-3.5 h-3.5" />
                      <span>Send Official Invitation</span>
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
