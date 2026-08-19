import { apiGet } from "./client";

// Matches MyDonationDto exactly (src/MusicLounge.Application/Donations/DTOs/MyDonationDto.cs).
// Status is a real string from the domain enum (PendingOwnerAck/OwnerReceived/PerformerPaid/...),
// not modeled as a union here since this is a read-only history view -- rendered as-is with a
// small VN label map, same approach as TicketStatus in api/tickets.ts.
export interface MyDonation {
  id: number;
  performerName: string;
  showName: string;
  gross: number;
  net: number;
  status: string;
  isAnonymous: boolean;
  message: string | null;
  paymentConfirmedAt: string | null;
  createdAt: string;
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

// GET /api/v1/donations/my -- RequireAuthenticated, every donation the current user has ever made
// regardless of status.
export function getMyDonations(page = 1, pageSize = 20): Promise<PaginatedResult<MyDonation>> {
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiGet<PaginatedResult<MyDonation>>(`/donations/my?${qs.toString()}`);
}
