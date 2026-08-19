import { apiGet } from "./client";

// Matches PerformerDto exactly (src/MusicLounge.Application/Performers/DTOs/PerformerDto.cs).
export interface Performer {
  id: number;
  name: string;
  avatarUrl: string | null;
  bio: string | null;
  type: string;
  createdByUserId: number | null;
  genreIds: number[];
  genreNames: string[];
  socialLinks: { platform: string; url: string }[];
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// GET /performers?search=... -- catalog shared across all Owners (not scoped to one venue),
// requires RequireOwner policy. Used for the Create Show lineup builder's search-before-create flow.
export function searchPerformers(search: string, pageSize = 8): Promise<PaginatedResult<Performer>> {
  const params = new URLSearchParams({ search, page: "1", pageSize: String(pageSize) });
  return apiGet<PaginatedResult<Performer>>(`/performers?${params.toString()}`);
}
