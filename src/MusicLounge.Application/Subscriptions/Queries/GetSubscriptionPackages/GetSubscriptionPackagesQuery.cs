using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Subscriptions.DTOs;

namespace MusicLounge.Application.Subscriptions.Queries.GetSubscriptionPackages;

public sealed record GetSubscriptionPackagesQuery(bool ActiveOnly) : IQuery<IReadOnlyList<SubscriptionPackageDto>>;
