using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerAnalytics;

/// <param name="From">MLACP-659: đầu kỳ (bao gồm). Null = không giới hạn — giữ hành vi cũ (mọi thời gian, xu hướng 6 tháng).</param>
/// <param name="To">MLACP-659: cuối kỳ (bao gồm).</param>
public sealed record GetOwnerAnalyticsQuery(Guid LoungeId, DateTimeOffset? From = null, DateTimeOffset? To = null)
    : IQuery<OwnerAnalyticsDto>;
