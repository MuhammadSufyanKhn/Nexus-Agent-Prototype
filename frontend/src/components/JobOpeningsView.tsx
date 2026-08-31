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
  Star
} from 'lucide-react';
import { 
  fetchJobOpenings, 
  fetchCandidateApplications,
  createJobOpening, 
  deleteJobOpening 
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
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    loadOpenings();
  }, []);

  const loadOpenings = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchJobOpenings();
      setJobOpenings(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load job openings.');
    } finally {
      setLoading(false);
    }
  };

  const loadApplicationsForJob = async (jobId: number) => {
    if (jobApplications[jobId]) return;
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
        salaryRange: newSalary.trim() || undefined,
        location: newLocation.trim() || undefined
      });

      setShowCreateModal(false);
      setNewTitle('');
      setNewDepartment('');
      setNewRequirements('');
      setNewDescription('');
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
            const appCount = job.applicationsCount || 0;
            const applicants = jobApplications[job.id] || [];
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
                                <div className="flex items-center gap-2">
                                  <span className="text-xs font-bold text-slate-900">{app.candidateName}</span>
                                  {app.fitScore && app.fitScore >= 80 && (
                                    <span className="text-[10px] bg-emerald-100 text-emerald-800 border border-emerald-200 px-1.5 py-0.2 rounded font-bold flex items-center gap-1">
                                      <Star className="w-2.5 h-2.5 fill-current text-amber-500" />
                                      {app.fitScore}% Best Fit
                                    </span>
                                  )}
                                </div>
                                <div className="text-[11px] text-slate-500">
                                  {app.email} • {app.experienceYears} Years Exp • {new Date(app.submittedAt).toLocaleDateString()}
                                </div>
                              </div>
                            </div>

                            <button
                              onClick={() => onScreenCandidate ? onScreenCandidate(job.id, app.id) : null}
                              className="text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 px-3 py-1.5 rounded-lg flex items-center gap-1.5 shadow-2xs transition-colors cursor-pointer self-end sm:self-auto"
                            >
                              <Sparkles className="w-3.5 h-3.5 text-amber-300" />
                              <span>Screen in CV Tab</span>
                            </button>
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
                  Role Description
                </label>
                <textarea
                  rows={2}
                  placeholder="Describe role scope and responsibilities..."
                  value={newDescription}
                  onChange={(e) => setNewDescription(e.target.value)}
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
    </div>
  );
};
