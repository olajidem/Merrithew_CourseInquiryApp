import { INQUIRY_STATUSES, type InquiryStatus } from '../types/inquiry';

interface Props {
  value: InquiryStatus;
  onChange: (status: InquiryStatus) => void;
  disabled?: boolean;
}

export default function StatusSelect({ value, onChange, disabled }: Props) {
  return (
    <select
      className="status-select"
      value={value}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value as InquiryStatus)}
      aria-label="Inquiry status"
    >
      {INQUIRY_STATUSES.map((s) => (
        <option key={s} value={s}>{s}</option>
      ))}
    </select>
  );
}
