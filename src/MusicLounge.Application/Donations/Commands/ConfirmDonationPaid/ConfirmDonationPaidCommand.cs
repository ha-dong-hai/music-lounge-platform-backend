using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Donations.Commands.ConfirmDonationPaid;

public sealed record ConfirmDonationPaidCommand(
    int DonationId,
    string PaymentRef,
    string? PaymentEvidenceUrl
) : ICommand;
