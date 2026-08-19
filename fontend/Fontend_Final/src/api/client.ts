// Thin fetch wrapper matching the backend's one consistent response/error shape
// (see README-SETUP.md "Ràng buộc & lưu ý quan trọng" -> "Format lỗi API"):
//   success response: whatever the endpoint returns
//   error response:   { success: false, message: string, errors: Record<string, string[]> | null }

export interface ApiFieldErrors {
  [field: string]: string[];
}

export class ApiError extends Error {
  fieldErrors: ApiFieldErrors | null;
  status: number;

  constructor(message: string, status: number, fieldErrors: ApiFieldErrors | null) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

const API_BASE = "/api/v1";
const TOKEN_KEY = "musiclounge_token";
const USER_KEY = "musiclounge_user";

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearStoredToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

// Small subset of AuthResultDto worth persisting across a page refresh (e.g. showing the
// logged-in owner's name without a round-trip to GET /me) -- not a substitute for /me, just
// enough to render a profile chip immediately after login.
export interface StoredUser {
  fullName: string;
  role: string;
  // Only meaningful for Staff -- AuthResultDto.LoungeId, the one venue a Staff account is scoped
  // to (VenueOperatorAccess.CanOperate checks this JWT-carried claim server-side on every
  // RequireVenueOperator endpoint). null/undefined for every other role.
  loungeId?: number | null;
}

export function setStoredUser(user: StoredUser): void {
  localStorage.setItem(USER_KEY, JSON.stringify(user));
}

export function getStoredUser(): StoredUser | null {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StoredUser;
  } catch {
    return null;
  }
}

export function clearSession(): void {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

function authHeaders(): HeadersInit {
  const token = getStoredToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function handleResponse<TResponse>(res: Response): Promise<TResponse> {
  const data = await res.json().catch(() => null);

  if (!res.ok || (data && data.success === false)) {
    const message = data?.message ?? `Request failed (${res.status})`;
    throw new ApiError(message, res.status, data?.errors ?? null);
  }

  // `data?.data ?? data` looks equivalent but isn't: when an endpoint's own payload is
  // legitimately null (e.g. GET /subscriptions/my with no active subscription), `??` treats that
  // null as "absent" and falls through to returning the *whole envelope* instead of null. Check
  // key presence, not truthiness, so a real null payload comes through as null.
  if (data && typeof data === "object" && "data" in data) {
    return data.data as TResponse;
  }
  return data as TResponse;
}

export async function apiPost<TResponse>(path: string, body: unknown): Promise<TResponse> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(body),
  });
  return handleResponse<TResponse>(res);
}

// Query params are appended by the caller (URLSearchParams) since each endpoint's filter set
// differs — keeping this generic rather than typing every possible query shape here.
export async function apiGet<TResponse>(path: string): Promise<TResponse> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "GET",
    headers: { ...authHeaders() },
  });
  return handleResponse<TResponse>(res);
}

export async function apiPut<TResponse>(path: string, body: unknown): Promise<TResponse> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(body),
  });
  return handleResponse<TResponse>(res);
}

// No body, and none of the DELETE endpoints return one either -- they all answer 204, which
// handleResponse already copes with (res.json() throws on an empty body, the .catch turns it into
// null, and 204 is res.ok so it falls through to returning null).
export async function apiDelete<TResponse>(path: string): Promise<TResponse> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "DELETE",
    headers: { ...authHeaders() },
  });
  return handleResponse<TResponse>(res);
}

// No Content-Type header on purpose -- the browser sets the multipart boundary itself when the
// body is a FormData; setting it manually breaks the boundary.
export async function apiUpload<TResponse>(path: string, file: File): Promise<TResponse> {
  const formData = new FormData();
  formData.append("file", file);
  const res = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: { ...authHeaders() },
    body: formData,
  });
  return handleResponse<TResponse>(res);
}
