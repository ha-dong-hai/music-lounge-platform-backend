namespace MusicLounge.Application.Donations.DTOs;

public sealed record DonationInitiationDto(
    int DonationId,
    string OrderId,
    decimal Gross,
    string PaymentUrl
);
