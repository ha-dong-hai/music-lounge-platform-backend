using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Subscriptions.Commands.UpdateSubscriptionPackage;

public sealed record UpdateSubscriptionPackageCommand(
    Guid PackageId,
    string? Description,
    decimal Price,
    int MaxTicketsPerEvent,
    bool HasAiPoster,
    int MaxAiPostersPerMonth,
    int MaxTourScenes,
    bool IsActive
) : ICommand;
