export interface CreateInquiryRequest {
    firstName: string;
    lastName: string;
    email: string;
    phone?: string;
    courseName: string;
    preferredLocation?: string;
    message?: string;
  }
  
  export interface InquiryResponse {
    id: number;
    firstName: string;
    lastName: string;
    email: string;
    phone: string | null;
    courseName: string;
    preferredLocation: string | null;
    message: string | null;
    status: string;
    createdDate: string;
    updatedDate: string | null;
  }
  
  export type FieldErrors = Partial<Record<keyof CreateInquiryRequest, string>>;
  export const INQUIRY_STATUSES = ['New', 'Contacted', 'Pending', 'Registered', 'Closed'] as const;
export type InquiryStatus = (typeof INQUIRY_STATUSES)[number];
