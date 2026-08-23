import React, { useState } from 'react';
import { FileCheck, Sparkles, CheckCircle2, AlertCircle, UserPlus, Upload, ShieldCheck, Award, AlertTriangle } from 'lucide-react';
import { executeAgentPrompt } from '../services/api';

export const CvCheckerView: React.FC = () => {
  const [cvText, setCvText] = useState('');
  const [jobTitle, setJobTitle] = useState('.NET Developer');
  const [requiredSkills, setRequiredSkills] = useState('C#, .NET Core, SQL Server, Entity Framework, REST API');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<any>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [candidateCreated, setCandidateCreated] = useState(false);

  const sampleCvText = `CANDIDATE RESUME: Ali Khan
Email: ali.khan@devmail.com | Phone: +92-300-1234567 | Location: Lahore, PK

SUMMARY:
Results-driven Software Engineer with 4+ years of hands-on experience building enterprise Web APIs, Microservices, and SQL Server databases using C#, .NET Core, ASP.NET, Entity Framework, and React.js.

TECHNICAL SKILLS:
- Languages: C#, JavaScript, TypeScript, SQL
- Frameworks: .NET Core 8.0, ASP.NET Core, Entity Framework Core, React, Redux
- Databases: SQL Server 2022, T-SQL, Redis
- Tools: Git, Docker, Azure DevOps, Postman, Visual Studio 2022

EXPERIENCE:
Senior Software Developer — TechCorp Solutions (2022 - Present)
- Designed and delivered high-performance RESTful Web APIs serving 50k+ daily active users.
- Optimized EF Core queries and SQL Server indexes, reducing DB query latency by 45%.
- Implemented JWT authentication and Role-Based Access Control (RBAC) security matrix.

WORK EXPERIENCE:
Software Engineer — SoftCode Systems (2020 - 2022)
- Built responsive React dashboards integrated with C# backend services.
- Participated in CI/CD pipeline automation and unit testing using xUnit.

EDUCATION:
BS Computer Science — Fast University (2020)`;

  const handleAnalyze = async () => {
    if (!cvText.trim()) return;

    setLoading(true);
    setErrorMsg(null);
    setResult(null);
    setCandidateCreated(false);

    try {
      const res = await fetch('/api/cv/analyze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          cvContent: cvText,
          jobTitle: jobTitle,
          requiredSkills: requiredSkills
        })
      });

      if (!res.ok) {
        throw new Error('Failed to analyze CV text.');
      }

      const data = await res.json();
      setResult(data);
    } catch (err: any) {
      setErrorMsg(err.message || 'CV Analysis failed.');
    } finally {
      setLoading(false);
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

  return (
    <div className="max-w-6xl mx-auto px-6 py-6 space-y-6 animate-in fade-in duration-200">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950 to-slate-900 text-white rounded-2xl p-6 shadow-lg flex items-center justify-between">
        <div className="space-y-2 max-w-2xl">
          <div className="flex items-center gap-2 text-indigo-400 text-xs font-semibold uppercase tracking-wider">
            <Sparkles className="w-4 h-4" />
            <span>AI Talent Evaluation &amp; CV Matcher</span>
          </div>
          <h2 className="text-2xl font-bold tracking-tight">CV Checker &amp; Scoring Engine</h2>
          <p className="text-xs text-slate-300 leading-relaxed">
            Upload or paste candidate CV text to extract technical skills, evaluate job description fit, score candidate suitability, and initiate automated onboarding.
          </p>
        </div>
        <div className="p-4 bg-white/5 border border-white/10 rounded-2xl backdrop-blur-xs text-right space-y-1 hidden md:block">
          <div className="text-xs text-indigo-300 font-semibold">Gemini AI Parser</div>
          <div className="text-lg font-bold text-emerald-400 flex items-center gap-1.5 justify-end">
            <ShieldCheck className="w-5 h-5" /> 100% Automated
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Input Panel */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-md p-5 space-y-4 flex flex-col justify-between">
          <div className="space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-700 flex items-center gap-2">
                <Upload className="w-4 h-4 text-blue-600" />
                Candidate CV / Resume Input
              </span>
              <button
                onClick={() => setCvText(sampleCvText)}
                className="text-[11px] font-bold text-blue-600 hover:text-blue-800 hover:underline bg-blue-50 px-2.5 py-1 rounded border border-blue-200"
              >
                Load Sample CV
              </button>
            </div>

            <div className="grid grid-cols-2 gap-3 text-xs">
              <div>
                <label className="font-bold text-slate-700 block mb-1">Target Position</label>
                <input
                  type="text"
                  value={jobTitle}
                  onChange={(e) => setJobTitle(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg font-semibold text-slate-800"
                />
              </div>
              <div>
                <label className="font-bold text-slate-700 block mb-1">Required Skills</label>
                <input
                  type="text"
                  value={requiredSkills}
                  onChange={(e) => setRequiredSkills(e.target.value)}
                  className="w-full p-2.5 border border-slate-300 rounded-lg font-semibold text-slate-800"
                />
              </div>
            </div>

            <div>
              <label className="font-bold text-xs text-slate-700 block mb-1">Raw CV Content</label>
              <textarea
                value={cvText}
                onChange={(e) => setCvText(e.target.value)}
                placeholder="Paste candidate resume/CV text here..."
                rows={10}
                className="w-full p-3 bg-slate-50 border border-slate-300 rounded-xl text-xs font-mono text-slate-800 focus:bg-white focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 outline-hidden transition-all resize-none"
              />
            </div>
          </div>

          <button
            onClick={handleAnalyze}
            disabled={loading || !cvText.trim()}
            className="w-full py-3 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white font-bold text-xs rounded-xl shadow-sm transition-all flex items-center justify-center gap-2"
          >
            {loading ? (
              <>
                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                <span>Evaluating Candidate Fit...</span>
              </>
            ) : (
              <>
                <FileCheck className="w-4 h-4" />
                <span>Analyze &amp; Score Candidate CV</span>
              </>
            )}
          </button>
        </div>

        {/* Results Panel */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-md p-5 space-y-4">
          <div className="flex items-center justify-between border-b border-slate-100 pb-3">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-700 flex items-center gap-2">
              <Award className="w-4 h-4 text-purple-600" />
              AI Evaluation Report
            </span>
            {result && (
              <span className={`px-2.5 py-0.5 rounded-full text-xs font-bold ${
                result.recommendation === 'RECOMMENDED' ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' : 'bg-amber-100 text-amber-800'
              }`}>
                {result.recommendation}
              </span>
            )}
          </div>

          {errorMsg && (
            <div className="p-4 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 font-semibold flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
              <span>{errorMsg}</span>
            </div>
          )}

          {!result && !loading && (
            <div className="h-80 flex flex-col items-center justify-center text-slate-400 space-y-2 text-center">
              <FileCheck className="w-12 h-12 stroke-1 text-slate-300" />
              <p className="text-xs font-medium">No CV analyzed yet.</p>
              <p className="text-[11px] text-slate-400 max-w-xs">Paste resume text on the left and click "Analyze &amp; Score Candidate CV" to view structured evaluation.</p>
            </div>
          )}

          {result && (
            <div className="space-y-4 animate-in fade-in duration-300 text-xs">
              {/* Score Header */}
              <div className="p-4 bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200 rounded-xl flex items-center justify-between">
                <div>
                  <h3 className="font-extrabold text-sm text-slate-900">{result.candidateName}</h3>
                  <div className="text-[11px] text-slate-500">{result.targetPosition} • {result.experienceYears} Years Experience</div>
                </div>
                <div className="text-right">
                  <div className="text-2xl font-black text-blue-700">{result.matchScore}%</div>
                  <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">Match Score</div>
                </div>
              </div>

              {/* Extracted Skills Badges */}
              <div className="space-y-1.5">
                <span className="font-bold text-slate-700 block text-[11px]">Extracted Technical Skills:</span>
                <div className="flex flex-wrap gap-1.5">
                  {result.extractedSkills?.map((skill: string, i: number) => (
                    <span key={i} className="px-2 py-0.5 bg-blue-50 text-blue-700 border border-blue-200 font-semibold text-[11px] rounded">
                      ✓ {skill}
                    </span>
                  ))}
                </div>
              </div>

              {/* Key Strengths */}
              {result.strengths && result.strengths.length > 0 && (
                <div className="space-y-1">
                  <span className="font-bold text-emerald-800 block text-[11px] flex items-center gap-1">
                    <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" /> Key Candidate Strengths:
                  </span>
                  <ul className="space-y-1 pl-4">
                    {result.strengths.map((s: string, idx: number) => (
                      <li key={idx} className="text-slate-700 text-[11px] list-disc">{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Missing Skills */}
              {result.missingSkills && result.missingSkills.length > 0 && (
                <div className="space-y-1">
                  <span className="font-bold text-amber-800 block text-[11px] flex items-center gap-1">
                    <AlertTriangle className="w-3.5 h-3.5 text-amber-600" /> Missing / Gap Requirements:
                  </span>
                  <ul className="space-y-1 pl-4">
                    {result.missingSkills.map((m: string, idx: number) => (
                      <li key={idx} className="text-amber-900 text-[11px] list-disc">{m}</li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Candidate Creation Draft */}
              {result.proposedRecord && (
                <div className="p-3.5 bg-slate-50 border border-slate-200 rounded-xl space-y-2">
                  <div className="flex items-center justify-between font-bold text-slate-800 text-[11px]">
                    <span>Proposed Candidate Employee Record</span>
                    <span className="text-emerald-600 font-bold">${result.proposedRecord.suggestedSalary?.toLocaleString()}/yr</span>
                  </div>
                  <div className="text-[11px] text-slate-600">
                    Dept: <span className="font-semibold text-slate-900">{result.proposedRecord.department}</span> • Position: <span className="font-semibold text-slate-900">{result.proposedRecord.designation}</span>
                  </div>
                  <button
                    onClick={handleCreateCandidateRecord}
                    disabled={candidateCreated}
                    className="w-full py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300 text-white font-bold text-xs rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-xs"
                  >
                    <UserPlus className="w-3.5 h-3.5" />
                    {candidateCreated ? 'Candidate Record Created!' : 'Initiate Employee Onboarding Plan'}
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
