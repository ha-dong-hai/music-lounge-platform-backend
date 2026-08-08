using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Donations.Commands.ProcessDonationPayment;

public sealed record ProcessDonationPaymentCommand(
    IDictionary<string, string> QueryParams
) : ICommand<bool>;
