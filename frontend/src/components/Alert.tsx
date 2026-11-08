interface Props {
    type: 'success' | 'error';
    message: string;
    onClose?: () => void;
  }
  
  export default function Alert({ type, message, onClose }: Props) {
    return (
      <div className={`alert alert-${type}`} role={type === 'error' ? 'alert' : 'status'}>
        <span>{type === 'success' ? '✓' : '⚠'} {message}</span>
        {onClose && (
          <button type="button" className="alert-close" onClick={onClose} aria-label="Dismiss">
            ×
          </button>
        )}
      </div>
    );
  }
  