namespace MusicLounge.Application.Analytics.DTOs;

public sealed record PlatformAnalyticsDto(
    int TotalVenues,
    int TotalPublishedShows,
    int TotalUsers,
    int TotalTicketsSold,
    decimal TotalGrossMerchandiseValue,
    decimal TotalDonationVolume,
    int PendingModerationsCount);
