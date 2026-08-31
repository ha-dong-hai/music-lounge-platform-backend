using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Subscriptions.Commands.ProcessSubscriptionPayment;

public sealed record ProcessSubscriptionPaymentCommand(
    IDictionary<string, string> QueryParams
) : ICommand<bool>;
