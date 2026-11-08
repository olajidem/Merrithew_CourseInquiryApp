import { useState } from 'react';
import SubmitInquiryPage from './pages/SubmitInquiryPage';
import StaffDashboardPage from './pages/StaffDashboardPage';
import './App.css';

type Page = 'submit' | 'dashboard';

export default function App() {
  const [page, setPage] = useState<Page>('submit');

  return (
    <>
      <nav className="nav">
        <span className="brand">Merrithew Course Inquiries</span>
        <button className={page === 'submit' ? 'active' : ''} onClick={() => setPage('submit')}>
          Submit inquiry
        </button>
        <button className={page === 'dashboard' ? 'active' : ''} onClick={() => setPage('dashboard')}>
          View inquiries
        </button>
      </nav>

      <main className={`container ${page === 'dashboard' ? 'wide' : ''}`}>
        {page === 'submit' ? <SubmitInquiryPage /> : <StaffDashboardPage />}
      </main>
    </>
  );
}
