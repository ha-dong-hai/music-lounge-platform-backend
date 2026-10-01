namespace MusicLounge.Application.Analytics.DTOs;

public sealed record LivestreamHistoryItemDto(
    Guid LivestreamId,
    Guid ShowId,
    string ShowName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int PeakViewerCount,
    int TotalViews,
    decimal PpvRevenue,
    decimal TotalDonations);
