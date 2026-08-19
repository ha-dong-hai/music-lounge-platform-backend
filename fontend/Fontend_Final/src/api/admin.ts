import { apiGet, apiPost } from "./client";

// ---------------------------------------------------------------------------
// Bank account verification
// ---------------------------------------------------------------------------

// Matches PendingBankAccountDto (src/MusicLounge.Application/BankAccounts/DTOs/).
//
// `accountNumber` arrives DECRYPTED (it is PII-encrypted at rest and decrypted only at Admin-gated
// read boundaries) because the account number is the thing being verified against the business
// licence -- a masked value would make the screen useless. Treat it as sensitive: never log it,
// never put it in a URL.
export interface PendingBankAccount {
  id: number;
  loungeId: number;
  loungeName: string;
  ownerId: number;
  ownerName: string;
  ownerEmail: string;
  businessLicenseUrl: string | null;
  bankName: string;
  accountNumber: string;
  accountHolder: string;
  isDefault: boolean;
  createdAt: string;
}

// GET /admin/bank-accounts/pending -- Lounge accounts only, oldest first. Performer accounts are
// deliberately excluded: the platform never pays a performer directly (donation chặng 2 is the
// Owner paying them, evidenced by an uploaded receipt), so verifying one would gate nothing.
export function getPendingBankAccounts(page = 1, pageSize = 20): Promise<PaginatedResult<PendingBankAccount>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<PendingBankAccount>>(`/admin/bank-accounts/pending?${params.toString()}`);
}

// POST /admin/bank-accounts/{id}/verify. Verifying a venue's DEFAULT account also retries every
// settlement that was previously blocked on it (VerifyBankAccountCommandHandler ->
// ISettlementSchedulingService.RetryBlockedForLoungeAsync), so this button releases real money.
// Answers 409 if the account was already verified.
export function verifyBankAccount(id: number): Promise<void> {
  return apiPost<void>(`/admin/bank-accounts/${id}/verify`, {});
}

// ---------------------------------------------------------------------------
// Complaints
// ---------------------------------------------------------------------------

// All three match their Domain/Enums counterparts exactly.
export type ComplaintCategory =
  | "EventMisrepresentation"
  | "RefundDispute"
  | "DonationNotPaid"
  | "TechnicalIssue"
  | "VenueConduct"
  | "PenaltyAppeal"
  | "Other";

export type ComplaintStatus = "Open" | "Investigating" | "Resolved" | "Rejected";

export type ComplaintResolvedAction = "Refund" | "IssueWarning" | "Dismiss" | "Compensate" | "TakeDownContent";

// Matches ComplaintDto (src/MusicLounge.Application/Complaints/DTOs/ComplaintDto.cs) exactly.
// `evidenceUrls` really is a single string on the wire, not an array -- it is stored as one
// delimited field, so the UI splits it rather than assuming the API did.
export interface Complaint {
  id: number;
  targetType: string;
  targetId: number;
  targetGuid: string | null;
  category: ComplaintCategory;
  description: string;
  evidenceUrls: string | null;
  contactPhone: string | null;
  status: ComplaintStatus;
  complainantName: string | null;
  adminName: string | null;
  resolution: string | null;
  resolvedAction: ComplaintResolvedAction | null;
  resolvedAt: string | null;
  createdAt: string;
}

// GET /admin/complaints -- GetPendingComplaintsQuery, so this is the unresolved queue.
export function getPendingComplaints(page = 1, pageSize = 20): Promise<PaginatedResult<Complaint>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<Complaint>>(`/admin/complaints?${params.toString()}`);
}

// POST /admin/complaints/{id}/resolve. Mirrors ResolveComplaintCommandValidator exactly:
//   * status is limited to Investigating | Resolved | Rejected (never back to Open)
//   * resolvedAction is REQUIRED when status is "Resolved"
//   * refundAmount is REQUIRED when resolvedAction is "Compensate", and must be > 0 when sent
//   * TakeDownContent is only accepted when the complaint targets a show (handler-enforced)
export interface ResolveComplaintBody {
  status: "Investigating" | "Resolved" | "Rejected";
  resolution: string | null;
  resolvedAction: ComplaintResolvedAction | null;
  refundAmount: number | null;
}

export function resolveComplaint(id: number, body: ResolveComplaintBody): Promise<void> {
  return apiPost<void>(`/admin/complaints/${id}/resolve`, body);
}

// ---------------------------------------------------------------------------
// Users
// ---------------------------------------------------------------------------

// Matches UserRole (Domain/Enums) exactly, in declaration order.
export type UserRole = "Audience" | "Staff" | "Owner" | "Admin";

// Matches UserAdminDto (src/MusicLounge.Application/Users/DTOs/UserAdminDto.cs) exactly.
export interface AdminUser {
  id: number;
  email: string;
  fullName: string;
  phone: string | null;
  avatarUrl: string | null;
  role: string;
  isActive: boolean;
  isEmailVerified: boolean;
  createdAt: string;
}

export interface UserQuery {
  searchText?: string;
  role?: UserRole;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}

// GET /admin/users -- all three filters are optional and independent; omitting one means "any".
export function getUsers(query: UserQuery = {}): Promise<PaginatedResult<AdminUser>> {
  const params = new URLSearchParams();
  if (query.searchText) params.set("searchText", query.searchText);
  if (query.role) params.set("role", query.role);
  if (query.isActive !== undefined) params.set("isActive", String(query.isActive));
  params.set("page", String(query.page ?? 1));
  params.set("pageSize", String(query.pageSize ?? 20));
  return apiGet<PaginatedResult<AdminUser>>(`/admin/users?${params.toString()}`);
}

export function deactivateUser(id: number): Promise<void> {
  return apiPost<void>(`/admin/users/${id}/deactivate`, {});
}

export function reactivateUser(id: number): Promise<void> {
  return apiPost<void>(`/admin/users/${id}/reactivate`, {});
}

// ---------------------------------------------------------------------------
// Refund requests
// ---------------------------------------------------------------------------

// Matches RefundRequestStatus (Domain/Enums) exactly.
export type RefundRequestStatus = "Pending" | "Approved" | "Rejected";

// Matches RefundRequestDto (src/MusicLounge.Application/Refunds/DTOs/RefundRequestDto.cs) exactly.
//
// Deliberately thin on human-readable context: there is no buyer name, event name, or original
// payment amount on this DTO -- only `paymentId` / `requestedBy` as raw ids. The Stitch design this
// screen was ported from mocked up "User / Event" and "Original Amt" columns; neither can be
// rendered honestly against this contract, so both were dropped rather than faked (same call as the
// "Mới" badge dropped in AdminPendingVenues).
export interface RefundRequest {
  id: number;
  paymentId: number;
  requestedBy: number | null;
  reason: string;
  amountRequested: number;
  amountApproved: number | null;
  refundPercentage: number | null;
  status: RefundRequestStatus;
  createdAt: string;
  resolvedAt: string | null;
  // True once ProcessRefundRequest tried VNPay and could not get a confirmed automated refund --
  // the money then has to move by manual bank transfer, so this is an operational flag an Admin
  // genuinely needs to see, not decoration.
  requiresManualTransfer: boolean;
  gatewayRefundResponseCode: string | null;
}

// GET /admin/refund-requests -- despite the generic name, the handler filters
// `r.Status == RefundRequestStatus.Pending`, so this only ever returns the pending queue. That is
// why this screen has no status filter: there is nothing else to filter to.
export function getPendingRefundRequests(page = 1, pageSize = 20): Promise<PaginatedResult<RefundRequest>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<RefundRequest>>(`/admin/refund-requests?${params.toString()}`);
}

// POST /admin/refund-requests/{id}/process. `approvedAmount` is only meaningful when approving;
// null means "refund exactly what was requested" (the handler's own default). The handler rejects
// an amount above the original payment's gross with a DomainException -> HTTP 422.
export function processRefundRequest(
  id: number,
  decision: "Approved" | "Rejected",
  approvedAmount: number | null = null,
): Promise<void> {
  return apiPost<void>(`/admin/refund-requests/${id}/process`, { decision, approvedAmount });
}

// Matches PendingLoungeDto (src/MusicLounge.Application/Lounges/DTOs/PendingLoungeDto.cs) exactly.
// Note: no primaryImageUrl here -- the pending-review queue only ever shows what an Admin needs to
// make an approve/reject call (owner identity, address, license doc), not marketing photography.
export interface PendingLounge {
  id: number;
  name: string;
  description: string | null;
  ownerId: number;
  ownerName: string;
  ownerEmail: string;
  businessLicenseUrl: string | null;
  street: string;
  ward: string;
  district: string;
  city: string;
  createdAt: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// GET /admin/lounges/pending -- whole AdminController requires the RequireAdmin policy.
export function getPendingLounges(page = 1, pageSize = 20): Promise<PaginatedResult<PendingLounge>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<PendingLounge>>(`/admin/lounges/pending?${params.toString()}`);
}

export function approveLounge(id: number): Promise<void> {
  return apiPost<void>(`/admin/lounges/${id}/approve`, {});
}

export function rejectLounge(id: number, reason: string): Promise<void> {
  return apiPost<void>(`/admin/lounges/${id}/reject`, { reason });
}
