using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Subscriptions.DTOs;

namespace MusicLounge.Application.Subscriptions.Queries.GetMySubscription;

public sealed record GetMySubscriptionQuery : IQuery<MySubscriptionDto?>;
