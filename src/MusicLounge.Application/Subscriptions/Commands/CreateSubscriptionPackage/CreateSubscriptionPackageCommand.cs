using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Subscriptions.Commands.CreateSubscriptionPackage;

public sealed record CreateSubscriptionPackageCommand(
    string Name,
    string? Description,
    decimal Price,
    string BillingCycle,
    int MaxTicketsPerEvent,
    bool HasAiPoster,
    int MaxAiPostersPerMonth
) : ICommand<int>;
