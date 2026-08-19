import { apiDelete, apiGet, apiPost, apiPut } from "./client";

// Matches FilterOptionsDto (src/MusicLounge.Application/LoungeShows/DTOs/FilterOptionsDto.cs).
// Lives under /lounge-shows/filter-options rather than a dedicated /taxonomy route -- reused here
// because it's the only public read endpoint for Atmosphere (Admin owns the CRUD side, taxonomy is
// platform-wide, not per-lounge data).
export interface AtmosphereDto {
  id: number;
  name: string;
}

export interface FilterOptionsDto {
  // GenreDto is (Id, Name) only -- verified 2026-08-18 against GenreDto.cs. An earlier `nameEn`
  // declared here never existed on the wire.
  genres: { id: number; name: string }[];
  moods: { id: number; name: string }[];
  atmospheres: AtmosphereDto[];
  categories: { id: number; name: string }[];
  cities: string[];
}

export function getFilterOptions(): Promise<FilterOptionsDto> {
  return apiGet<FilterOptionsDto>("/lounge-shows/filter-options");
}

// City/District are free text an Owner types at venue creation (no fixed VN administrative-
// division table backs them -- confirmed against OwnerCreateVenue.tsx and the Address value
// object), so this reflects real venues' data, not a canonical province/district gazetteer.
// Empty city arg or a city with no venues both resolve to []; caller should treat that as "no
// district filter available" rather than an error.
export function getDistrictsByCity(city: string): Promise<string[]> {
  const qs = new URLSearchParams({ city });
  return apiGet<string[]>(`/lounge-shows/filter-options/districts?${qs.toString()}`);
}

// ---------------------------------------------------------------------------
// Admin taxonomy CRUD (all under /admin, RequireAdmin policy)
// ---------------------------------------------------------------------------
//
// The four tag types are NOT symmetric, and the asymmetry matters to the UI:
//
//   moods, atmospheres  -- create/update take exactly { name }, which is also all the read endpoint
//                          returns. Fully round-trippable: rename is lossless.
//   genres              -- update takes { name, nameEn }, but GenreDto returns no nameEn, so an
//                          existing English name cannot be pre-filled and WILL be overwritten by
//                          whatever is submitted.
//   categories          -- update takes { name, description, isActive }, but CategoryDto returns
//                          neither description nor isActive. isActive is safely inferable as true
//                          (GetFilterOptionsQueryHandler filters `c => c.IsActive`, so every
//                          category this UI can see is active), but description has the same
//                          overwrite hazard as genre's nameEn.
//
// Both hazards are surfaced in the UI rather than hidden -- see AdminPlatformSettings.
export type TaxonomyKind = "categories" | "genres" | "moods" | "atmospheres";

export function createTaxonomyTerm(
  kind: TaxonomyKind,
  body: { name: string; description?: string | null; nameEn?: string | null },
): Promise<number> {
  return apiPost<number>(`/admin/${kind}`, body);
}

export function updateTaxonomyTerm(
  kind: TaxonomyKind,
  id: number,
  body: { name: string; description?: string | null; nameEn?: string | null; isActive?: boolean },
): Promise<void> {
  return apiPut<void>(`/admin/${kind}/${id}`, body);
}

// Answers 409 when the term is still referenced by any show, performer, venue, or user preference
// -- the "in-use? refuse" policy is identical across all four types.
export function deleteTaxonomyTerm(kind: TaxonomyKind, id: number): Promise<void> {
  return apiDelete<void>(`/admin/${kind}/${id}`);
}
