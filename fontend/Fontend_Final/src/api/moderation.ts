import { apiGet, apiPost } from "./client";

// All four match their Domain/Enums counterparts exactly.
export type ModerationTargetType = "Show" | "Livestream" | "GalleryImage" | "TourScene";
export type ModerationRiskLevel = "Low" | "Medium" | "High" | "Critical";
export type AiModerationRecommendation = "SuggestApprove" | "NeedsReview" | "SuggestReject";
export type ModerationDecision = "Approved" | "Rejected" | "Terminated";

// Matches EventModerationDto (src/MusicLounge.Application/Moderations/DTOs/EventModerationDto.cs).
// The enum-ish fields come across as nullable strings, not enums -- they are `string?` on the DTO
// because the underlying entity columns are nullable (a row exists before the AI has scored it).
export interface EventModeration {
  id: number;
  targetType: ModerationTargetType;
  targetId: number;
  aiScore: number | null;
  riskLevel: ModerationRiskLevel | null;
  flagReason: string | null;
  aiRecommendation: AiModerationRecommendation | null;
  adminId: number | null;
  adminDecision: ModerationDecision | null;
  reviewNote: string | null;
  createdAt: string;
  // NĐ 147/2024 gives 24h to review flagged content; this is createdAt + system_config's
  // moderation_sla_hours. Null on rows that predate the column -- means "no SLA was ever
  // computed", not "already overdue".
  slaDeadline: string | null;
  reviewedAt: string | null;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// GET /moderations/pending -- whole EventModerationsController is RequireAdmin.
export function getPendingModerations(
  targetType: ModerationTargetType | null,
  page = 1,
  pageSize = 20,
): Promise<PaginatedResult<EventModeration>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (targetType) params.set("targetType", targetType);
  return apiGet<PaginatedResult<EventModeration>>(`/moderations/pending?${params.toString()}`);
}

export interface ReviewBody {
  decision: "Approved" | "Rejected";
  reviewNote: string | null;
}

// IMPORTANT ASYMMETRY -- the three review endpoints are NOT keyed the same way:
//
//   Show / Livestream -> keyed on the TARGET id (moderation.targetId), because a show id uniquely
//                        identifies one show and the handler looks its moderation row up by target.
//   GalleryImage /     -> keyed on the MODERATION row id (moderation.id), because those two target
//   TourScene             types live in different tables and a bare target id would be ambiguous
//                         between them.
//
// Passing the wrong one is a silent 404 (or worse, hits an unrelated row), so `reviewModeration`
// below is the only thing callers should use -- it picks the right id from the row itself.
export function reviewShow(showId: number, body: ReviewBody): Promise<void> {
  return apiPost<void>(`/moderations/shows/${showId}/review`, body);
}

export function reviewLivestream(livestreamId: number, body: ReviewBody): Promise<void> {
  return apiPost<void>(`/moderations/livestreams/${livestreamId}/review`, body);
}

export function reviewImage(moderationId: number, body: ReviewBody): Promise<void> {
  return apiPost<void>(`/moderations/images/${moderationId}/review`, body);
}

// Single entry point that routes to the correct endpoint AND the correct id for a given row.
export function reviewModeration(m: EventModeration, body: ReviewBody): Promise<void> {
  switch (m.targetType) {
    case "Show":
      return reviewShow(m.targetId, body);
    case "Livestream":
      return reviewLivestream(m.targetId, body);
    case "GalleryImage":
    case "TourScene":
      return reviewImage(m.id, body);
  }
}
