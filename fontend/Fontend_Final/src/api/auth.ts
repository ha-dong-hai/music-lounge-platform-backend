import { apiPost } from "./client";

export type AccountRole = "Audience" | "Owner";

export interface RegisterPayload {
  email: string;
  password: string;
  fullName: string;
  phone: string | null;
  acceptTerms: boolean;
  role: AccountRole;
}

// Matches RegisterResultDto exactly (Register does not log the account in -- no token/userId
// yet, the account isn't verified). Field names are the C# record's properties camelCased, which
// is how System.Text.Json serializes them by default (confirmed against a real response).
export interface RegisterResult {
  email: string;
  fullName: string;
  verificationCodeExpiresAt: string;
}

// Matches AuthResultDto exactly -- VerifyEmail succeeding logs the account in for the first time.
export interface AuthResult {
  token: string;
  expiresAt: string;
  userId: number;
  email: string;
  fullName: string;
  role: string;
  loungeId: number | null;
}

// POST /api/v1/auth/register — see README-SETUP.md Bước 6.1.
// Self-registration only ever accepts "Audience" or "Owner" (Staff is Owner-assigned,
// Admin is SQL-only) — enforced server-side too, this is just matching UI to reality.
export function registerAccount(payload: RegisterPayload): Promise<RegisterResult> {
  return apiPost<RegisterResult>("/auth/register", payload);
}

export function verifyEmail(email: string, code: string): Promise<AuthResult> {
  return apiPost<AuthResult>("/auth/verify-email", { email, code });
}

// Matches ResendVerificationCodeResultDto. Always resolves with a plausible-looking expiry
// regardless of whether the email matches a real account (anti-enumeration) -- callers must not
// branch UI on success/failure of this call, only use the returned expiry to restart a countdown.
export interface ResendVerificationCodeResult {
  verificationCodeExpiresAt: string;
}

export function resendVerificationCode(email: string): Promise<ResendVerificationCodeResult> {
  return apiPost<ResendVerificationCodeResult>("/auth/resend-verification-code", { email });
}

// POST /api/v1/auth/login. Matches LoginRequest(Email, Password) -- IP is captured server-side
// from the connection itself, never client-submitted (LoginSpikeDetectionJob keys on it; a
// client-supplied value would let an attacker spoof/rotate it to dodge detection).
export function login(email: string, password: string): Promise<AuthResult> {
  return apiPost<AuthResult>("/auth/login", { email, password });
}

// POST /api/v1/auth/forgot-password -- always resolves (204), regardless of whether the email
// matches a real account (ForgotPasswordCommandHandler: anti-enumeration, same reasoning as
// resendVerificationCode above). Callers must not branch UI on success vs "email not found" --
// there is no such distinction to branch on.
export function requestPasswordReset(email: string): Promise<void> {
  return apiPost<void>("/auth/forgot-password", { email });
}

// POST /api/v1/auth/reset-password -- token comes from the emailed reset link's `?token=` query
// param, 30 min lifetime, single-use. Throws ApiError(status: 401) if the token is missing,
// already used, or expired -- ResetPasswordCommandHandler intentionally doesn't distinguish those
// cases in the message either, same anti-enumeration-adjacent reasoning.
export function resetPassword(token: string, newPassword: string): Promise<void> {
  return apiPost<void>("/auth/reset-password", { token, newPassword });
}
