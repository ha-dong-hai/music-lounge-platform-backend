namespace MusicLounge.Application.Analytics.DTOs;

public sealed record VenueReputationRankDto(int LoungeId, string LoungeName, decimal ReputationScore);

public sealed record AdminContentOverviewDto(
    int PendingEventsCount,
    int UnresolvedComplaintsCount,
    int ViolationsThisMonthCount,
    IReadOnlyList<VenueReputationRankDto> TopVenuesByReputation);
