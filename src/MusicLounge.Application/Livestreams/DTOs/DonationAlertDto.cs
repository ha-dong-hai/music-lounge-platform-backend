namespace MusicLounge.Application.Livestreams.DTOs;

public sealed record DonationAlertDto(
    string DonorName,
    decimal Amount,
    string? Message);
