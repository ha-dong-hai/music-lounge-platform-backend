using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Donations.Commands.ConfirmDonationPaid;

public sealed record ConfirmDonationPaidCommand(
    Guid DonationId,
    string PaymentRef,
    string? PaymentEvidenceUrl
) : ICommand;
