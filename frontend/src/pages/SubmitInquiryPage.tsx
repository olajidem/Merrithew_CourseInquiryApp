import { useState } from 'react';
import InquiryForm from '../components/InquiryForm';
import { createInquiry } from '../api/inquiriesApi';
import { ApiError } from '../api/apiClient';
import { formatDate } from '../utils/formatDate';
import type { CreateInquiryRequest, FieldErrors, InquiryResponse } from '../types/inquiry';

export default function SubmitInquiryPage() {
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<InquiryResponse | null>(null);
  const [serverErrors, setServerErrors] = useState<FieldErrors>({});
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  async function handleSubmit(request: CreateInquiryRequest) {
    setSubmitting(true);
    setServerErrors({});
    setErrorMessage(null);
    try {
      const saved = await createInquiry(request);
      setResult(saved);
    } catch (err) {
      if (err instanceof ApiError) {
        setServerErrors(err.fieldErrors as FieldErrors);
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Something went wrong. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <section className="card success">
        <h2>Thanks, {result.firstName}! Your inquiry was received.</h2>
        <dl>
          <dt>Reference #</dt><dd>{result.id}</dd>
          <dt>Name</dt><dd>{result.firstName} {result.lastName}</dd>
          <dt>Email</dt><dd>{result.email}</dd>
          <dt>Phone</dt><dd>{result.phone ?? '—'}</dd>
          <dt>Course</dt><dd>{result.courseName}</dd>
          <dt>Location</dt><dd>{result.preferredLocation ?? '—'}</dd>
          <dt>Message</dt><dd>{result.message ?? '—'}</dd>
          <dt>Status</dt><dd><span className="badge">{result.status}</span></dd>
          <dt>Submitted</dt><dd>{formatDate(result.createdDate)}</dd>
        </dl>
        <button onClick={() => setResult(null)}>Submit another inquiry</button>
      </section>
    );
  }

  return (
    <section className="card">
      <h2>Course inquiry</h2>
      <p>Fill in your details and we'll get back to you.</p>
      {errorMessage && <div className="alert" role="alert">{errorMessage}</div>}
      <InquiryForm onSubmit={handleSubmit} serverErrors={serverErrors} submitting={submitting} />
    </section>
  );
}
