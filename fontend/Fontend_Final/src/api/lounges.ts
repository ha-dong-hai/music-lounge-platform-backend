import { apiGet, apiPost, apiPut, apiUpload } from "./client";

// Matches LoungeListItemDto exactly (src/MusicLounge.Application/Lounges/DTOs/LoungeListItemDto.cs).
// status/rejectionReason are only meaningful when fetched with mine=true -- the public/anonymous
// listing excludes Pending/Rejected only, NOT "Approved-only" as an earlier version of this comment
// claimed (verified against LoungeRepository.GetAllAsync). Warned/Suspended/Locked venues ARE still
// returned here -- deliberate per §6.8 (PublishLoungeShowCommandHandler.cs): a suspended/locked
// venue can't publish NEW shows, but its already-published shows and the venue itself stay visible
// so existing ticket holders/followers aren't affected by an enforcement action against the venue.
export type LoungeStatus = "Pending" | "Approved" | "Rejected" | "Warned" | "Suspended" | "Locked";

export interface LoungeListItem {
  id: number;
  name: string;
  primaryImageUrl: string | null;
  businessLicenseUrl: string | null;
  model3DUrl: string | null;
  areaLayoutImageUrl: string | null;
  street: string;
  district: string;
  city: string;
  followerCount: number;
  upcomingShowCount: number;
  status: LoungeStatus;
  rejectionReason: string | null;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// GET /api/v1/lounges?mine=true -- GetLoungesQueryHandler throws UnauthorizedException (401) if
// called without a valid token when mine=true, unlike the public listing which is AllowAnonymous.
// Callers must be logged in as Owner before calling this.
// GET /api/v1/lounges (mine omitted/false) -- AllowAnonymous, only ever returns Approved lounges.
// Used to populate a "Venue" filter picker on the public show-discovery page.
export function getLounges(params: { city?: string; page?: number; pageSize?: number } = {}): Promise<PaginatedResult<LoungeListItem>> {
  const qs = new URLSearchParams();
  if (params.city) qs.set("city", params.city);
  qs.set("page", String(params.page ?? 1));
  qs.set("pageSize", String(params.pageSize ?? 50));
  return apiGet<PaginatedResult<LoungeListItem>>(`/lounges?${qs.toString()}`);
}

export function getMyLounges(page = 1, pageSize = 20): Promise<PaginatedResult<LoungeListItem>> {
  const params = new URLSearchParams({ mine: "true", page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<LoungeListItem>>(`/lounges?${params.toString()}`);
}

// Matches CreateLoungeCommand exactly (src/MusicLounge.Application/Lounges/Commands/CreateLounge).
// Note there is no field here for photo/license/3D model -- those are set via separate PUT calls
// below, only possible once the lounge (and its id) already exists.
export interface CreateLoungeRequest {
  name: string;
  description: string | null;
  atmosphereId: number | null;
  street: string;
  ward: string;
  district: string;
  city: string;
  latitude: number | null;
  longitude: number | null;
}

// POST /lounges -- CreatedAtAction wraps the new id in ApiResponse<int>, so this resolves to the
// bare number the same way every other apiPost caller in this codebase already expects.
export function createLounge(body: CreateLoungeRequest): Promise<number> {
  return apiPost<number>("/lounges", body);
}

export function setLoungeImage(id: number, imageUrl: string): Promise<void> {
  return apiPut<void>(`/lounges/${id}/image`, { imageUrl });
}

export function setLoungeBusinessLicense(id: number, documentUrl: string): Promise<void> {
  return apiPut<void>(`/lounges/${id}/business-license`, { documentUrl });
}

export function setLoungeModel3D(id: number, modelUrl: string): Promise<void> {
  return apiPut<void>(`/lounges/${id}/model-3d`, { modelUrl });
}

// POST /uploads/images (jpg/jpeg/png/webp/gif only, 5MB max -- see LocalFileStorageService,
// no PDF support despite "business license" documents commonly being PDF in real life).
export function uploadLoungeImage(file: File): Promise<{ url: string }> {
  return apiUpload<{ url: string }>("/uploads/images", file);
}

// POST /uploads/models (.glb only, 5MB max).
export function uploadLoungeModel3D(file: File): Promise<{ url: string }> {
  return apiUpload<{ url: string }>("/uploads/models", file);
}
