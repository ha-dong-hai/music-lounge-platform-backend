import { apiGet, apiPost } from "./client";

// Both match their Domain/Enums counterparts exactly.
export type PenaltyType = "Warning" | "Suspension" | "Ban";
export type PenaltyStatus = "Active" | "Appealed" | "Overturned" | "Upheld" | "Expired";

// Matches AdminVenuePenaltyDto (src/MusicLounge.Application/VenuePenalties/DTOs/).
export interface AdminVenuePenalty {
  id: number;
  loungeId: number;
  loungeName: string;
  ownerId: number;
  ownerName: string;
  ownerEmail: string;
  penaltyType: PenaltyType;
  reason: string;
  evidenceRef: string | null;
  issuedAt: string;
  effectiveAt: string;
  suspensionDays: number | null;
  suspensionEnd: string | null;
  status: PenaltyStatus;
  // Owner's window to contest. Past this, an appeal can no longer be submitted.
  appealDeadline: string | null;
  appealedAt: string | null;
  appealReason: string | null;
  appealResult: string | null; // "Overturned" | "Upheld", null until reviewed
  reviewedAt: string | null;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// GET /venue-penalties?status=&page=&pageSize= -- Admin-only (the controller's other GETs are
// Owner/authenticated-scoped). Omit status for every penalty; pass "Appealed" for the actionable
// queue.
export function getVenuePenalties(
  status: PenaltyStatus | null,
  page = 1,
  pageSize = 20,
): Promise<PaginatedResult<AdminVenuePenalty>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (status) params.set("status", status);
  return apiGet<PaginatedResult<AdminVenuePenalty>>(`/venue-penalties?${params.toString()}`);
}

// POST /venue-penalties/{id}/appeal/review.
//
// ReviewAppealCommandValidator accepts ONLY "Overturned" | "Upheld" -- note these are outcomes for
// the PENALTY, so the wording inverts against the appeal: "Overturned" means the appeal SUCCEEDED
// (penalty cancelled, and if it was the venue's only Active penalty the venue is restored to
// Approved), "Upheld" means the penalty stands and the appeal failed.
//
// The handler refuses (422) unless the penalty is currently in status Appealed.
export function reviewAppeal(
  penaltyId: number,
  decision: "Overturned" | "Upheld",
  reviewNote: string | null,
): Promise<void> {
  return apiPost<void>(`/venue-penalties/${penaltyId}/appeal/review`, { decision, reviewNote });
}
