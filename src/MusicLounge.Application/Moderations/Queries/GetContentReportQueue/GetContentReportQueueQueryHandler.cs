using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Moderations.Queries.GetContentReportQueue;

internal sealed class GetContentReportQueueQueryHandler
    : IRequestHandler<GetContentReportQueueQuery, PaginatedResult<ContentReportQueueItemDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetContentReportQueueQueryHandler(IUnitOfWork uow, ISystemConfigService config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<PaginatedResult<ContentReportQueueItemDto>> Handle(
        GetContentReportQueueQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);

        var openReports = await _uow.Repository<ContentReport, int>()
            .FindAsync(r => r.Status == ContentReportStatus.Open, ct);

        // Nhieu report cung tro toi 1 (TargetType, TargetId) -> gop lai thanh 1 dong hang doi, so
        // luong report Open chinh la "so lan bao cao" de sap xep uu tien.
        var groups = openReports
            .GroupBy(r => (r.TargetType, r.TargetId))
            .Select(g => new
            {
                g.Key.TargetType,
                g.Key.TargetId,
                ReportCount = g.Count(),
                EarliestReportedAt = g.Min(r => r.CreatedAt),
                LatestReason = g.OrderByDescending(r => r.CreatedAt).First().Reason
            })
            .OrderByDescending(g => g.ReportCount)
            .ThenBy(g => g.EarliestReportedAt)
            .ToList();

        var total = groups.Count;
        var pageItems = groups.Skip((page - 1) * size).Take(size).ToList();

        var slaHours = await _config.GetIntAsync(ConfigKeys.ContentReportSlaHours, 48, ct);

        var summaries = await ResolveTargetSummariesAsync(pageItems.Select(g => (g.TargetType, g.TargetId)), ct);

        var items = pageItems.Select(g => new ContentReportQueueItemDto(
            g.TargetType.ToString(),
            g.TargetId,
            summaries.GetValueOrDefault((g.TargetType, g.TargetId)),
            g.ReportCount,
            g.LatestReason,
            g.EarliestReportedAt,
            g.EarliestReportedAt.AddHours(slaHours)
        )).ToList();

        return new PaginatedResult<ContentReportQueueItemDto>(items, page, size, total);
    }

    private async Task<Dictionary<(ReportTargetType, int), string>> ResolveTargetSummariesAsync(
        IEnumerable<(ReportTargetType TargetType, int TargetId)> targets, CancellationToken ct)
    {
        var result = new Dictionary<(ReportTargetType, int), string>();

        var showIds = targets.Where(t => t.TargetType == ReportTargetType.Show).Select(t => t.TargetId).ToList();
        var livestreamIds = targets.Where(t => t.TargetType == ReportTargetType.Livestream).Select(t => t.TargetId).ToList();
        var ratingIds = targets.Where(t => t.TargetType == ReportTargetType.Rating).Select(t => t.TargetId).ToList();

        if (showIds.Count > 0)
        {
            var shows = await _uow.Repository<LoungeShow, int>().FindAsync(s => showIds.Contains(s.Id), ct);
            foreach (var s in shows) result[(ReportTargetType.Show, s.Id)] = s.Name;
        }

        if (livestreamIds.Count > 0)
        {
            var livestreams = await _uow.Repository<Livestream, int>().FindAsync(l => livestreamIds.Contains(l.Id), ct);
            var showIdsForLivestreams = livestreams.Select(l => l.LoungeShowId).Distinct().ToList();
            var relatedShows = showIdsForLivestreams.Count > 0
                ? await _uow.Repository<LoungeShow, int>().FindAsync(s => showIdsForLivestreams.Contains(s.Id), ct)
                : [];
            var showNameById = relatedShows.ToDictionary(s => s.Id, s => s.Name);
            foreach (var l in livestreams)
                result[(ReportTargetType.Livestream, l.Id)] = showNameById.GetValueOrDefault(l.LoungeShowId, $"Livestream #{l.Id}");
        }

        if (ratingIds.Count > 0)
        {
            var ratings = await _uow.Repository<LoungeShowRating, int>().FindAsync(r => ratingIds.Contains(r.Id), ct);
            foreach (var r in ratings)
                result[(ReportTargetType.Rating, r.Id)] =
                    string.IsNullOrWhiteSpace(r.Comment) ? $"Đánh giá {r.Score}★" : r.Comment;
        }

        return result;
    }
}
