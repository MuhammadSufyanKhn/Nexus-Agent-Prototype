import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { CandidateApplicationPortal } from './components/CandidateApplicationPortal';
import './index.css';

const params = new URLSearchParams(window.location.search);
const jobId = params.get('jobId') ? Number(params.get('jobId')) : undefined;

createRoot(document.getElementById('portal-root')!).render(
  <StrictMode>
    <CandidateApplicationPortal initialJobId={jobId} />
  </StrictMode>,
);
