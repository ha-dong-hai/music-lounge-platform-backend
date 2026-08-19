import { apiGet } from "./client";

// Matches OwnerAnalyticsDto exactly (src/MusicLounge.Application/Analytics/DTOs/OwnerAnalyticsDto.cs).
export interface TopShow {
  showId: number;
  name: string;
  scheduledStart: string;
  mainPerformerName: string | null;
  ticketsSold: number;
  totalCapacity: number | null;
  averageRating: number | null;
  revenue: number;
}

export interface RevenueMonth {
  year: number;
  month: number;
  fnbRevenue: number;
  offlineTicketRevenue: number;
  onlineTicketRevenue: number;
}

export interface OwnerAnalytics {
  totalShows: number;
  upcomingShows: number;
  pastShows: number;
  totalTicketsSold: number;
  offlineTicketsSold: number;
  onlineTicketsSold: number;
  totalRevenue: number;
  ticketRevenue: number;
  fnbRevenue: number;
  averageRating: number | null;
  totalRatings: number;
  pendingArtistPayoutCount: number;
  pendingArtistPayoutAmount: number;
  revenueTrend: RevenueMonth[];
  topShows: TopShow[];
}

// GET /analytics/my-lounge?loungeId= -- RequireOwner. 404 if the lounge doesn't belong to the
// caller (see GetOwnerAnalyticsQueryHandler's own ownership check).
export function getOwnerAnalytics(loungeId: number): Promise<OwnerAnalytics> {
  return apiGet<OwnerAnalytics>(`/analytics/my-lounge?loungeId=${loungeId}`);
}

// Matches EarningsSummaryDto exactly (src/MusicLounge.Application/Users/DTOs/EarningsSummaryDto.cs).
// Status is the real SettlementStatus enum name (Scheduled/Released/Cancelled/PendingReview) --
// rendered as-is with a VN label, same pattern as every other status enum in this codebase.
export interface RecentSettlement {
  id: number;
  amount: number;
  status: string;
  scheduledAt: string;
  paidAt: string | null;
}

export interface EarningsSummary {
  totalEarned: number;
  pendingSettlement: number;
  completedSettlement: number;
  pendingSettlementCount: number;
  recentSettlements: RecentSettlement[];
}

// GET /me/earnings -- RequireOwner. Owner-wide (every venue the Owner has, not filtered by
// loungeId) -- Settlement rows are queried by `s.OwnerId == currentUser`, not per-lounge.
export function getMyEarnings(): Promise<EarningsSummary> {
  return apiGet<EarningsSummary>("/me/earnings");
}
