namespace MusicLounge.Application.Donations.DTOs;

public sealed record PublicDonationDto(
    int Id,
    string ShowName,
    string VenueName,
    DateTimeOffset ShowDate,
    string? DonorDisplayName,
    decimal? Gross,
    string Status,
    DateTimeOffset CreatedAt
);
