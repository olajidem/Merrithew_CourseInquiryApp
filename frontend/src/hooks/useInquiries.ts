import { useCallback, useEffect, useState } from 'react';
import { getInquiries } from '../api/inquiriesApi';
import type { InquiryResponse, InquiryStatus } from '../types/inquiry';

export function useInquiries(status?: InquiryStatus) {
  const [inquiries, setInquiries] = useState<InquiryResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setInquiries(await getInquiries(status));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load inquiries.');
    } finally {
      setLoading(false);
    }
  }, [status]);   // re-fetch whenever the filter changes

  useEffect(() => {
    load();
  }, [load]);

  return { inquiries, loading, error, reload: load };
}
