import StatusBadge from './StatusBadge';
import { formatDate } from '../utils/formatDate';
import type { InquiryResponse } from '../types/inquiry';

interface Props {
    inquiries: InquiryResponse[];
    emptyMessage?: string;
    onSelect: (id: number) => void;
  }

  export default function InquiryTable({ inquiries, emptyMessage = 'No inquiries yet.', onSelect }: Props) {
    if (inquiries.length === 0) {
      return <p className="empty">{emptyMessage}</p>;
    }

  return (
    <div className="table-wrap">
      <table className="inquiry-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Name</th>
            <th>Email</th>
            <th>Course</th>
            <th>Location</th>
            <th>Status</th>
            <th>Submitted</th>
          </tr>
        </thead>
        <th></th>
        <tbody>
          {inquiries.map((i) => (
          <tr key={i.id} className="clickable" onClick={() => onSelect(i.id)}>
          <td>{i.id}</td>
          <td>{i.firstName} {i.lastName}</td>
          <td>{i.email}</td>
          <td>{i.courseName}</td>
          <td>{i.preferredLocation ?? '—'}</td>
          <td><StatusBadge status={i.status} /></td>
          <td>{formatDate(i.createdDate)}</td>
          <td>
            <button
              type="button"
              className="link-button"
              onClick={(e) => { e.stopPropagation(); onSelect(i.id); }}
            >
              View
            </button>
          </td>
        </tr>
        
          ))}
        </tbody>
      </table>
    </div>
  );
}
