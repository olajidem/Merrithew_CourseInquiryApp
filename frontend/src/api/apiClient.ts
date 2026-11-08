const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5012/api';

export class ApiError extends Error {
  status: number;
  fieldErrors: Record<string, string>;

  constructor(message: string, status: number, fieldErrors: Record<string, string> = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

// ASP.NET may return keys as "Email" or "$.email" – normalise to "email"
function normaliseKey(key: string): string {
  const k = key.replace(/^\$\./, '');
  return k.charAt(0).toLowerCase() + k.slice(1);
}

async function request<TResponse>(path: string, init?: RequestInit): Promise<TResponse> {
  let res: Response;
  try {
    res = await fetch(`${BASE_URL}${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    });
  } catch {
    throw new ApiError('Could not reach the server. Is the API running?', 0);
  }

  if (res.ok) {
    return res.status === 204 ? (undefined as TResponse) : ((await res.json()) as TResponse);
  }

  // ProblemDetails / ValidationProblemDetails from ASP.NET
  const problem = await res.json().catch(() => null);

  const fieldErrors: Record<string, string> = {};
  if (problem?.errors) {
    for (const [key, messages] of Object.entries(problem.errors as Record<string, string[]>)) {
      fieldErrors[normaliseKey(key)] = messages[0];
    }
  }

  // Most specific message first: detail → first field error → title → generic
  const message =
    problem?.detail ??
    Object.values(fieldErrors)[0] ??
    problem?.title ??
    `Request failed (${res.status})`;

  throw new ApiError(message, res.status, fieldErrors);
}

export const apiGet = <T>(path: string) => request<T>(path);

export const apiPost = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'POST', body: JSON.stringify(body) });

export const apiPut = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'PATCH', body: JSON.stringify(body) });
