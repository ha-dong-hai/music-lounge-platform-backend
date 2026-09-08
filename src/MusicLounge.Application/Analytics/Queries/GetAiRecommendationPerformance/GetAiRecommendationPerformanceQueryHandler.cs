using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Analytics.Queries.GetAiRecommendationPerformance;

internal sealed class GetAiRecommendationPerformanceQueryHandler
    : IRequestHandler<GetAiRecommendationPerformanceQuery, AiRecommendationPerformanceDto>
{
    // Same VN-local (UTC+7) convention as GetAdminPlatformOverviewQueryHandler.
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public GetAiRecommendationPerformanceQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<AiRecommendationPerformanceDto> Handle(
        GetAiRecommendationPerformanceQuery request, CancellationToken ct)
    {
        DateTimeOffset from, to;
        if (request.From.HasValue && request.To.HasValue)
        {
            from = request.From.Value;
            to = request.To.Value;
        }
        else
        {
            var nowVn = DateTimeOffset.UtcNow.ToOffset(VnOffset);
            var monthStart = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, VnOffset);
            from = request.From ?? monthStart;
            to = request.To ?? monthStart.AddMonths(1).AddTicks(-1);
        }

        // "Duoc goi y" = it xuat hien it nhat 1 lan trong AiRecommendation trong ky — tinh theo cap
        // (UserId, LoungeShowId) DUY NHAT, vi 1 cap co the duoc goi y lai nhieu lan qua cac chu ky
        // lam moi cache (MLACP-132) trong cung 1 ky bao cao.
        var allRecs = await _uow.Repository<AiRecommendation, int>()
            .FindAsync(r => r.CreatedAt >= from, ct);
        var recs = allRecs.Where(r => r.CreatedAt <= to).ToList();

        var recommendedPairs = recs
            .GroupBy(r => (r.UserId, r.LoungeShowId))
            .Select(g => (Pair: g.Key, FirstShownAt: g.Min(r => r.CreatedAt), LastExpiresAt: g.Max(r => r.ExpiresAt)))
            .ToList();

        if (recommendedPairs.Count == 0)
        {
            return new AiRecommendationPerformanceDto(from, to, 0, 0, 0m, 0, 0m);
        }

        var userIds = recommendedPairs.Select(p => p.Pair.UserId).Distinct().ToHashSet();
        var showIds = recommendedPairs.Select(p => p.Pair.LoungeShowId).Distinct().ToHashSet();

        // Chi lay du lieu hanh vi cho dung tap user/show lien quan, loc theo tung cap + cua so thoi
        // gian (tu luc duoc goi y toi luc het han goi y) o phia client — ket hop 2 dieu kien (thanh
        // vien tap + khoang thoi gian rieng tung dong) khong dich duoc thanh 1 truy van SQLite don.
        var candidateLogs = await _uow.Repository<UserBehaviourLog, int>().FindAsync(
            l => userIds.Contains(l.UserId) && showIds.Contains(l.LoungeShowId)
                && (l.Action == BehaviourAction.ViewEvent
                    || l.Action == BehaviourAction.ClickTicket
                    || l.Action == BehaviourAction.PurchaseTicket), ct);

        var logsByPair = candidateLogs
            .ToLookup(l => (l.UserId, l.LoungeShowId));

        int clickThroughCount = 0;
        int conversionCount = 0;
        foreach (var (pair, firstShownAt, lastExpiresAt) in recommendedPairs)
        {
            var pairLogs = logsByPair[pair]
                .Where(l => l.CreatedAt >= firstShownAt && l.CreatedAt <= lastExpiresAt)
                .ToList();

            if (pairLogs.Any(l => l.Action is BehaviourAction.ViewEvent or BehaviourAction.ClickTicket))
                clickThroughCount++;
            if (pairLogs.Any(l => l.Action == BehaviourAction.PurchaseTicket))
                conversionCount++;
        }

        var totalPairs = recommendedPairs.Count;
        return new AiRecommendationPerformanceDto(
            PeriodFrom: from,
            PeriodTo: to,
            RecommendedPairCount: totalPairs,
            ClickThroughCount: clickThroughCount,
            ClickThroughRatePercent: Math.Round(100m * clickThroughCount / totalPairs, 2),
            ConversionCount: conversionCount,
            ConversionRatePercent: Math.Round(100m * conversionCount / totalPairs, 2));
    }
}
