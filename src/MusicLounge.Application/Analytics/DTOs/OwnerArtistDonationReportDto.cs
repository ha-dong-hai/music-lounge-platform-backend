namespace MusicLounge.Application.Analytics.DTOs;

public sealed record ArtistDonationStatsDto(
    int PerformerId,
    string PerformerName,
    int DonationCount,
    decimal TotalGross,
    decimal TotalNet,
    int ShowCount);

public sealed record OwnerArtistDonationReportDto(
    decimal GrandTotalDonated,
    int? TopPerformerId,
    string? TopPerformerName,
    IReadOnlyList<ArtistDonationStatsDto> ByArtist);
