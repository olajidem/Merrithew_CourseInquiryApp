import { useState } from 'react';
import Alert from '../components/Alert';
import InquiryTable from '../components/InquiryTable';
import StatusFilter from '../components/StatusFilter';
import InquiryDetailPage from './InquiryDetailPage';
import { useInquiries } from '../hooks/useInquiries';
import type { InquiryStatus } from '../types/inquiry';

export default function StaffDashboardPage() {
  const [status, setStatus] = useState<InquiryStatus | undefined>(undefined);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const { inquiries, loading, error, reload } = useInquiries(status);

  if (selectedId !== null) {
    return (
      <InquiryDetailPage
        id={selectedId}
        onBack={() => {
          setSelectedId(null);
          reload();            // pick up any status changes
        }}
      />
    );
  }

  const summary = loading ? 'Loading…' : `${inquiries.length} ${status ?? 'total'}`;

  return (
    <section className="card">
      <div className="card-header">
        <div>
          <h2>Inquiries</h2>
          <p>{summary}</p>
        </div>
        <button onClick={reload} disabled={loading}>Refresh</button>
      </div>

      <StatusFilter value={status} onChange={setStatus} />

      {error && <Alert type="error" message={`Could not load inquiries: ${error}`} />}
      {!loading && !error && (
        <InquiryTable
          inquiries={inquiries}
          onSelect={setSelectedId}
          emptyMessage={status ? `No inquiries with status "${status}".` : 'No inquiries yet.'}
        />
      )}
    </section>
  );
}
