import { INQUIRY_STATUSES, type InquiryStatus } from '../types/inquiry';

interface Props {
  value: InquiryStatus | undefined;
  onChange: (status: InquiryStatus | undefined) => void;
}

export default function StatusFilter({ value, onChange }: Props) {
  return (
    <div className="status-filter" role="group" aria-label="Filter by status">
      <button
        type="button"
        className={value === undefined ? 'active' : ''}
        aria-pressed={value === undefined}
        onClick={() => onChange(undefined)}
      >
        All
      </button>
      {INQUIRY_STATUSES.map((s) => (
        <button
          key={s}
          type="button"
          className={value === s ? 'active' : ''}
          aria-pressed={value === s}
          onClick={() => onChange(s)}
        >
          {s}
        </button>
      ))}
    </div>
  );
}
