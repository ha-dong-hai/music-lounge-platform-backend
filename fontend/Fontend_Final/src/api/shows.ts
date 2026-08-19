import { apiDelete, apiGet, apiPost } from "./client";

// Matches LoungeShowStatus / LoungeShowFormat exactly (Domain/Enums) -- JsonStringEnumConverter
// is registered globally (Program.cs), so these come across the wire as their C# member names,
// not ints. Confirmed live against GET /lounge-shows?mine=true, not assumed.
export type LoungeShowStatus = "Draft" | "Pending" | "Published" | "Ongoing" | "Ended" | "Cancelled";
// No Hybrid: a show is strictly one or the other, never both (see docs/stitch/Stitch-Master-Brief.md
// Create Show section) -- selling both Physical and Livestream tickets for one show isn't
// supported, matching how Eventbrite/Luma/Zoom Events model this (two separate events instead).
export type LoungeShowFormat = "Offline" | "Online";

// Matches LoungeShowListItemDto exactly.
export interface LoungeShowListItem {
  id: number;
  name: string;
  coverImageUrl: string | null;
  loungeName: string;
  loungeDistrict: string;
  loungeCity: string;
  scheduledStart: string;
  format: LoungeShowFormat;
  status: LoungeShowStatus;
  minPrice: number | null;
  maxPrice: number | null;
  // GenreDto is (Id, Name) only -- verified 2026-08-18 against
  // src/MusicLounge.Application/LoungeShows/DTOs/GenreDto.cs. An earlier `nameEn` here was a type
  // lie (always undefined at runtime); nothing read it, so removing it is safe.
  genres: { id: number; name: string }[];
  performerNames: string[];
  offlineQuota: number | null;
  onlineQuota: number | null;
  isWishlisted: boolean | null;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// Matches LoungeShowSortBy exactly (Domain/Enums/LoungeShowSortBy.cs).
export type LoungeShowSortBy = "Newest" | "Popular" | "PriceAsc" | "PriceDesc" | "StartingSoon";

// Matches SearchLoungeShowsQuery's parameter list exactly (LoungeShowsController.Search) --
// AllowAnonymous + SwaggerOptionalAuth, works fully for a guest, no token needed. Every field is
// optional; omitted fields aren't sent (URLSearchParams below skips undefined/empty).
export interface SearchShowsParams {
  keyword?: string;
  genreIds?: number[];
  moodIds?: number[];
  atmosphereIds?: number[];
  // Backend gap fixed 2026-08-18: FilterOptionsDto returns Categories but
  // SearchLoungeShowsQuery had no categoryId param to filter by them -- added end-to-end
  // (Controller -> Query -> LoungeShowSearchParams -> EF repository) so this field works for real.
  categoryId?: number;
  loungeId?: number;
  city?: string;
  // Only meaningful together with city -- backend does a plain equality match, not "district
  // anywhere". See api/taxonomy.ts's getDistrictsByCity for where the picker's values come from.
  district?: string;
  format?: LoungeShowFormat;
  page?: number;
  pageSize?: number;
  sortBy?: LoungeShowSortBy;
}

export function searchShows(params: SearchShowsParams): Promise<PaginatedResult<LoungeShowListItem>> {
  const qs = new URLSearchParams();
  if (params.keyword) qs.set("keyword", params.keyword);
  for (const id of params.genreIds ?? []) qs.append("genreIds", String(id));
  for (const id of params.moodIds ?? []) qs.append("moodIds", String(id));
  for (const id of params.atmosphereIds ?? []) qs.append("atmosphereIds", String(id));
  if (params.categoryId) qs.set("categoryId", String(params.categoryId));
  if (params.loungeId) qs.set("loungeId", String(params.loungeId));
  if (params.city) qs.set("city", params.city);
  if (params.district) qs.set("district", params.district);
  if (params.format) qs.set("format", params.format);
  qs.set("page", String(params.page ?? 1));
  qs.set("pageSize", String(params.pageSize ?? 12));
  qs.set("sortBy", params.sortBy ?? "Newest");
  return apiGet<PaginatedResult<LoungeShowListItem>>(`/lounge-shows/search?${qs.toString()}`);
}

// GetPublishedLoungeShowsQueryHandler: mine=true switches to ILoungeShowRepository.GetMineAsync,
// which filters only by `Lounge.OwnerId == currentUser` -- no status filter -- so this genuinely
// returns shows at every lifecycle stage (Draft through Cancelled) across all of the owner's
// venues, exactly what "My Shows" needs. Requires auth; 401s if called anonymously.
export function getMyShows(page = 1, pageSize = 50): Promise<PaginatedResult<LoungeShowListItem>> {
  const params = new URLSearchParams({ mine: "true", page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<LoungeShowListItem>>(`/lounge-shows?${params.toString()}`);
}

// GET /lounge-shows/by-lounge/{loungeId} -- AllowAnonymous (not Staff-specific), no date filter on
// the backend (GetLoungeShowsByLoungeQuery only takes LoungeId/Page/PageSize) -- callers that want
// "today's shows" (e.g. the Staff box office) filter ScheduledStart client-side.
export function getLoungeShowsByLounge(loungeId: number, page = 1, pageSize = 50): Promise<PaginatedResult<LoungeShowListItem>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<LoungeShowListItem>>(`/lounge-shows/by-lounge/${loungeId}?${params.toString()}`);
}

// Matches PerformerRole exactly (Domain/Enums/PerformerRole.cs).
export type PerformerRole = "Main" | "Guest" | "Host";

// Matches CreateLoungeShowCommand's PerformanceInput record -- exactly one of performerId /
// performerName must be set (validator enforces this): performerId picks an existing catalog
// performer, performerName (with performerId omitted) silently creates a new one on submit.
export interface PerformanceInput {
  performerId: number | null;
  performerName: string | null;
  role: PerformerRole;
  orderIndex: number;
  setTime: string | null; // "HH:mm" (TimeOnly on the backend)
  acceptsDonation: boolean;
}

// Matches CreateLoungeShowCommand exactly. CustomValues intentionally omitted here (always sent
// empty) -- that's the venue-defined "Custom Criteria" feature (docs/stitch/Stitch-Master-Brief.md "Venue
// Extras"), a separate not-yet-built management screen; nothing to attach values to yet.
export interface CreateShowRequest {
  loungeId: number;
  name: string;
  description: string;
  format: LoungeShowFormat;
  scheduledStart: string;
  scheduledEnd: string | null;
  categoryId: number | null;
  offlineQuota: number | null;
  onlineQuota: number | null;
  genreIds: number[];
  performances: PerformanceInput[];
  customValues: [];
}

export function createLoungeShow(body: CreateShowRequest): Promise<number> {
  return apiPost<number>("/lounge-shows", body);
}

// ---------------------------------------------------------------------------
// Show detail (GET /lounge-shows/{id}) -- AllowAnonymous + SwaggerOptionalAuth, but
// isWishlisted/userHasTicket/userHasRated only resolve meaningfully with a token; anonymous
// callers get null for all three (confirmed live 2026-08-18, not assumed --
// docs/journeys/21-anonymous-journey.md itself flagged this as unverified at the time it was written).
// ---------------------------------------------------------------------------

export type AccessType = "Physical" | "Livestream";
export type PurchaseChannel = "Online" | "Offline" | "Both";

export interface LoungeSummary {
  id: number;
  name: string;
  street: string;
  ward: string;
  district: string;
  city: string;
  fullAddress: string;
  latitude: number | null;
  longitude: number | null;
  primaryImageUrl: string | null;
  model3DUrl: string | null;
}

// Note: no "role" field (Main/Guest/Host) -- that's write-only on CreateShowRequest.performances,
// never echoed back by this read DTO. Don't invent a role badge in the UI.
export interface PerformerSummary {
  id: number;
  name: string;
  avatarUrl: string | null;
  bio: string | null;
  genres: { id: number; name: string }[];
  performanceId: number;
  acceptsDonation: boolean;
}

export interface TicketPriceSummary {
  id: number;
  name: string;
  price: number;
  quota: number | null;
  saleStart: string;
  saleEnd: string;
  purchaseChannel: PurchaseChannel;
  availableSlots: number | null;
}

export interface TicketTierSummary {
  id: number;
  name: string;
  description: string | null;
  accessType: AccessType;
  totalCapacity: number | null;
  zoneId: number | null;
  prices: TicketPriceSummary[];
}

export interface RatingSummary {
  averageScore: number;
  totalCount: number;
}

export interface LoungeShowDetail {
  id: number;
  name: string;
  description: string;
  coverImageUrl: string | null;
  scheduledStart: string;
  scheduledEnd: string | null;
  format: LoungeShowFormat;
  status: LoungeShowStatus;
  isOngoing: boolean;
  livestreamId: number | null;
  lounge: LoungeSummary;
  performers: PerformerSummary[];
  ticketTiers: TicketTierSummary[];
  genres: { id: number; name: string }[];
  ratings: RatingSummary;
  isWishlisted: boolean | null;
  userHasTicket: boolean | null;
  userHasRated: boolean | null;
}

export function getShowDetail(id: number): Promise<LoungeShowDetail> {
  return apiGet<LoungeShowDetail>(`/lounge-shows/${id}`);
}

// ---------------------------------------------------------------------------
// GET /lounge-shows/{id}/seating-map -- AllowAnonymous. Matches SeatingMapDto/ZoneMapEntryDto
// exactly (GetShowSeatingMapQueryHandler). Only zones with at least 1 TicketTier on THIS show are
// included -- a venue can have more zones than a given show actually sells. Layout2D* is a % (0-100)
// coordinate on a canvas, independent of screen resolution (see SetZoneLayout2DCommandValidator) --
// null on every field until an Owner has actually positioned that zone (no Owner-facing zone-layout
// editor exists in this frontend yet, so most/all zones will have no layout data today).
// ---------------------------------------------------------------------------

export interface ZoneMapEntry {
  zoneId: number;
  name: string;
  capacity: number;
  color: string | null;
  layout2DX: number | null;
  layout2DY: number | null;
  layout2DWidth: number | null;
  layout2DHeight: number | null;
  layout2DRotationDeg: number | null;
  layout3DX: number | null;
  layout3DY: number | null;
  layout3DZ: number | null;
  availableCount: number | null;
  minPrice: number | null;
  maxPrice: number | null;
}

export interface SeatingMap {
  areaLayoutImageUrl: string | null;
  zones: ZoneMapEntry[];
}

export function getShowSeatingMap(id: number): Promise<SeatingMap> {
  return apiGet<SeatingMap>(`/lounge-shows/${id}/seating-map`);
}

// POST/DELETE /wishlist/{showId} -- RequireAuthenticated on the whole controller, 401s if called
// without a token (caller must gate the button, not rely on the API to redirect).
export function addToWishlist(showId: number): Promise<void> {
  return apiPost<void>(`/wishlist/${showId}`, {});
}

export function removeFromWishlist(showId: number): Promise<void> {
  return apiDelete<void>(`/wishlist/${showId}`);
}

// ---------------------------------------------------------------------------
// GET /recommendations -- RequireAuthenticated. Matches RecommendedLoungeShowDto exactly.
// Real AI pipeline (MLNetRecommendationService: content + collaborative + custom-criteria
// scores), NOT decorative -- but only personalizes once the user has AiConsent=true AND cached
// recommendations exist; otherwise GetRecommendedLoungeShowsQueryHandler itself falls back to
// trending shows with score 0 and reason "Đang thịnh hành". Both cases render through the same
// UI, no separate "empty" state needed for the fallback since it's still a real, valid response.
// ---------------------------------------------------------------------------

export interface RecommendedShow {
  id: number;
  name: string;
  coverImageUrl: string | null;
  loungeName: string;
  loungeDistrict: string;
  loungeCity: string;
  scheduledStart: string;
  format: LoungeShowFormat;
  status: LoungeShowStatus;
  minPrice: number | null;
  maxPrice: number | null;
  genres: { id: number; name: string }[];
  performerNames: string[];
  // FinalScore is a sum of sub-scores (content/collab each clamped to [0,1] individually) plus an
  // optional followed-venue boost on top -- NOT itself hard-capped at 1, confirmed against
  // MLNetRecommendationService.cs. Clamp again at render time before turning this into a percent.
  recommendationScore: number;
  recommendationReason: string;
}

export function getRecommendedShows(limit = 6): Promise<RecommendedShow[]> {
  return apiGet<RecommendedShow[]>(`/recommendations?limit=${limit}`);
}
