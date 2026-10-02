using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Subscriptions.DTOs;

namespace MusicLounge.Application.Subscriptions.Commands.SubscribeToPackage;

public sealed record SubscribeToPackageCommand(
    Guid PackageId,
    string ClientIpAddress
) : ICommand<SubscriptionPaymentInitiationDto>;
