using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.FnbOrders.DTOs;

namespace MusicLounge.Application.FnbOrders.Commands.InitiateFnbOrderPayment;

public sealed record InitiateFnbOrderPaymentCommand(
    int OrderId,
    string ClientIpAddress
) : ICommand<FnbOrderPaymentInitiationDto>;
