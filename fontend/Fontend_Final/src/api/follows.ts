import { apiDelete, apiGet, apiPost } from "./client";

// Matches FollowedLoungeDto exactly (src/MusicLounge.Application/Follows/DTOs/FollowedLoungeDto.cs).
export interface FollowedLounge {
  id: number;
  name: string;
  primaryImageUrl: string | null;
  district: string;
  city: string;
  followedAt: string;
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

// GET/POST/DELETE /api/v1/follows/lounges/{id} -- RequireAuthenticated. Only venues can be
// followed in this system (no Performer login/account to follow -- see
// docs memory musiclounge_no_artist_actor), so "Following" only ever means followed lounges.
export function getFollowedLounges(page = 1, pageSize = 20): Promise<PaginatedResult<FollowedLounge>> {
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<FollowedLounge>>(`/follows/lounges?${qs.toString()}`);
}

export function followLounge(loungeId: number): Promise<void> {
  return apiPost<void>(`/follows/lounges/${loungeId}`, {});
}

export function unfollowLounge(loungeId: number): Promise<void> {
  return apiDelete<void>(`/follows/lounges/${loungeId}`);
}
