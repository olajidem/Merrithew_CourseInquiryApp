import { useEffect, useState } from 'react';
import Alert from '../components/Alert';
import StatusBadge from '../components/StatusBadge';
import StatusSelect from '../components/StatusSelect';
import { getInquiry, updateInquiryStatus } from '../api/inquiriesApi';
import { formatDate } from '../utils/formatDate';
import type { InquiryResponse, InquiryStatus } from '../types/inquiry';

interface Props {
  id: number;
  onBack: () => void;
}

type Notice = { type: 'success' | 'error'; message: string } | null;

const messageOf = (err: unknown) =>
  err instanceof Error ? err.message : 'Something went wrong. Please try again.';

export default function InquiryDetailPage({ id, onBack }: Props) {
  const [inquiry, setInquiry] = useState<InquiryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [selected, setSelected] = useState<InquiryStatus>('New');
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  // Load the inquiry
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(null);

    getInquiry(id)
      .then((data) => {
        if (cancelled) return;
        setInquiry(data);
        setSelected(data.status as InquiryStatus);
      })
      .catch((err) => !cancelled && setLoadError(messageOf(err)))
      .finally(() => !cancelled && setLoading(false));

    return () => { cancelled = true; };
  }, [id]);

  // Auto-hide success messages after 4s (errors stay until dismissed)
  useEffect(() => {
    if (notice?.type !== 'success') return;
    const timer = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(timer);
  }, [notice]);

  async function handleSave() {
    if (!inquiry) return;
    setSaving(true);
    setNotice(null);
    try {
      const updated = await updateInquiryStatus(inquiry.id, selected);
      setNotice({
        type: 'success',
        message: `Status updated from "${inquiry.status}" to "${updated.status}".`,
      });
      setInquiry(updated);
    } catch (err) {
      setNotice({ type: 'error', message: `Could not update status: ${messageOf(err)}` });
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="card">
      <button type="button" className="link-button" onClick={onBack}>← Back to inquiries</button>

      {loading && <p>Loading inquiry…</p>}
      {loadError && <Alert type="error" message={loadError} />}

      {inquiry && (
        <>
          <div className="card-header">
            <div>
              <h2>{inquiry.firstName} {inquiry.lastName}</h2>
              <p>Inquiry #{inquiry.id}</p>
            </div>
            <StatusBadge status={inquiry.status} />
          </div>

          {notice && <Alert type={notice.type} message={notice.message} onClose={() => setNotice(null)} />}

          <dl className="details">
            <dt>Email</dt><dd><a href={`mailto:${inquiry.email}`}>{inquiry.email}</a></dd>
            <dt>Phone</dt><dd>{inquiry.phone ?? '—'}</dd>
            <dt>Course</dt><dd>{inquiry.courseName}</dd>
            <dt>Location</dt><dd>{inquiry.preferredLocation ?? '—'}</dd>
            <dt>Message</dt><dd className="message">{inquiry.message ?? '—'}</dd>
            <dt>Submitted</dt><dd>{formatDate(inquiry.createdDate)}</dd>
            <dt>Last updated</dt><dd>{formatDate(inquiry.updatedDate)}</dd>
          </dl>

          <div className="status-update">
            <label htmlFor="status">Update status</label>
            <StatusSelect value={selected} onChange={setSelected} disabled={saving} />
            <button
              type="button"
              onClick={handleSave}
              disabled={saving || selected === inquiry.status}
            >
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </>
      )}
    </section>
  );
}
