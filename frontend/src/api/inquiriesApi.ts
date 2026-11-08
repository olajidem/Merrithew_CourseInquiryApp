import { apiGet, apiPatch, apiPost } from './apiClient';
import type { CreateInquiryRequest, InquiryResponse, InquiryStatus } from '../types/inquiry';

export function createInquiry(request: CreateInquiryRequest): Promise<InquiryResponse> {
  return apiPost<InquiryResponse>('/inquiries', request);
}

export function getInquiries(status?: InquiryStatus): Promise<InquiryResponse[]> {
  const query = status ? `?status=${encodeURIComponent(status)}` : '';
  return apiGet<InquiryResponse[]>(`/inquiries${query}`);
}

export function getInquiry(id: number): Promise<InquiryResponse> {
  return apiGet<InquiryResponse>(`/inquiries/${id}`);
}

export function updateInquiryStatus(id: number, status: InquiryStatus): Promise<InquiryResponse> {
  return apiPatch<InquiryResponse>(`/inquiries/${id}/status`, { status });
}
