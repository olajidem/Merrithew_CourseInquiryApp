import { useState } from 'react';
import type { CreateInquiryRequest, FieldErrors } from '../types/inquiry';

interface Props {
  onSubmit: (request: CreateInquiryRequest) => Promise<void>;
  serverErrors: FieldErrors;
  submitting: boolean;
}

const EMPTY: CreateInquiryRequest = {
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  courseName: '',
  preferredLocation: '',
  message: '',
};

const EMAIL_REGEX = /^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/;

function validate(v: CreateInquiryRequest): FieldErrors {
  const e: FieldErrors = {};
  if (!v.firstName.trim()) e.firstName = 'First name is required.';
  if (!v.lastName.trim()) e.lastName = 'Last name is required.';
  if (!v.email.trim()) e.email = 'Email is required.';
  else if (!EMAIL_REGEX.test(v.email.trim()))
    e.email = 'Please enter a valid email address (e.g. name@example.com).';
  if (!v.courseName.trim()) e.courseName = 'Course name is required.';
  if (v.message && v.message.length > 2000) e.message = 'Message cannot exceed 2000 characters.';
  return e;
}

export default function InquiryForm({ onSubmit, serverErrors, submitting }: Props) {
  const [values, setValues] = useState<CreateInquiryRequest>(EMPTY);
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  const errors = { ...serverErrors, ...clientErrors };

  function handleChange(e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) {
    const { name, value } = e.target;
    setValues((prev) => ({ ...prev, [name]: value }));
    setClientErrors((prev) => ({ ...prev, [name]: undefined }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const found = validate(values);
    setClientErrors(found);
    if (Object.keys(found).length > 0) return;

    await onSubmit({
      ...values,
      phone: values.phone?.trim() || undefined,
      preferredLocation: values.preferredLocation?.trim() || undefined,
      message: values.message?.trim() || undefined,
    });
  }

  function field(name: keyof CreateInquiryRequest, label: string, opts: { type?: string; required?: boolean; textarea?: boolean } = {}) {
    const error = errors[name];
    const props = {
      id: name,
      name,
      value: values[name] ?? '',
      onChange: handleChange,
      'aria-invalid': !!error,
      'aria-describedby': error ? `${name}-error` : undefined,
    };
    return (
      <div className={`field ${error ? 'has-error' : ''}`}>
        <label htmlFor={name}>
          {label} {opts.required && <span className="req">*</span>}
        </label>
        {opts.textarea ? <textarea rows={4} {...props} /> : <input type={opts.type ?? 'text'} {...props} />}
        {error && <span id={`${name}-error`} className="error">{error}</span>}
      </div>
    );
  }

  return (
    <form className="inquiry-form" onSubmit={handleSubmit} noValidate>
      <div className="row">
        {field('firstName', 'First name', { required: true })}
        {field('lastName', 'Last name', { required: true })}
      </div>
      <div className="row">
        {field('email', 'Email', { type: 'email', required: true })}
        {field('phone', 'Phone', { type: 'tel' })}
      </div>
      <div className="row">
        {field('courseName', 'Course', { required: true })}
        {field('preferredLocation', 'Preferred location')}
      </div>
      {field('message', 'Message', { textarea: true })}

      <button type="submit" disabled={submitting}>
        {submitting ? 'Submitting…' : 'Submit inquiry'}
      </button>
    </form>
  );
}
