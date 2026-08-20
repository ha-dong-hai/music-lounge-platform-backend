using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Commands.CreateDonation;

public sealed record CreateDonationCommand(
    int PerformanceId,
    decimal Amount,
    bool IsAnonymous,
    string? Message,
    bool IsMessagePublic,
    string ClientIpAddress
) : ICommand<DonationInitiationDto>;
