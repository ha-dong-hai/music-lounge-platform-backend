namespace MusicLounge.Application.Analytics.DTOs;

public sealed record ArtistDonationStatsDto(
    Guid PerformerId,
    string PerformerName,
    int DonationCount,
    decimal TotalGross,
    decimal TotalNet,
    int ShowCount);

public sealed record OwnerArtistDonationReportDto(
    decimal GrandTotalDonated,
    Guid? TopPerformerId,
    string? TopPerformerName,
    IReadOnlyList<ArtistDonationStatsDto> ByArtist);
