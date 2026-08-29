using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.FnbOrders.Commands.ProcessFnbOrderPayment;

public sealed record ProcessFnbOrderPaymentCommand(
    IDictionary<string, string> QueryParams
) : ICommand<bool>;
