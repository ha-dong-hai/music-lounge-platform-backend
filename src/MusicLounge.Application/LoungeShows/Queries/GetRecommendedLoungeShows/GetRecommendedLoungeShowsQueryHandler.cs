using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.LoungeShows.Queries.GetRecommendedLoungeShows;

internal sealed class GetRecommendedLoungeShowsQueryHandler
    : IRequestHandler<GetRecommendedLoungeShowsQuery, IReadOnlyList<RecommendedLoungeShowDto>>
{
    private readonly IRepository<AiRecommendation, int> _recRepo;
    private readonly IRepository<User, int> _userRepo;
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _jobs;

    public GetRecommendedLoungeShowsQueryHandler(
        IRepository<AiRecommendation, int> recRepo,
        IRepository<User, int> userRepo,
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser,
        IBackgroundJobService jobs)
    {
        _recRepo = recRepo;
        _userRepo = userRepo;
        _showRepo = showRepo;
        _currentUser = currentUser;
        _jobs = jobs;
    }

    public async Task<IReadOnlyList<RecommendedLoungeShowDto>> Handle(
        GetRecommendedLoungeShowsQuery request, CancellationToken ct)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);
        var user = await _userRepo.GetByIdAsync(_currentUser.UserId, ct);

        if (user is null || !user.AiConsent)
            return await GetTrendingFallbackAsync(limit, ct);

        var now = DateTimeOffset.UtcNow;
        var cached = await _recRepo.FindAsync(
            r => r.UserId == _currentUser.UserId && r.ExpiresAt > now, ct);

        if (cached.Count == 0)
        {
            _jobs.EnqueueRecommendationRefresh(_currentUser.UserId);
            return await GetTrendingFallbackAsync(limit, ct);
        }

        var showIds = cached.Select(r => r.LoungeShowId).ToList();
        var shows = await _showRepo.GetRecommendedByIdsAsync(showIds, ct);
        var recByShowId = cached.ToDictionary(r => r.LoungeShowId);

        return shows
            .OrderByDescending(s => recByShowId[s.Id].FinalScore)
            .Take(limit)
            .Select(s => s.ToRecommendedDto(recByShowId[s.Id].FinalScore, recByShowId[s.Id].Reason))
            .ToList();
    }

    private async Task<IReadOnlyList<RecommendedLoungeShowDto>> GetTrendingFallbackAsync(
        int limit, CancellationToken ct)
    {
        var shows = await _showRepo.GetTrendingAsync(limit, null, ct);
        return shows
            .Select(s => s.ToRecommendedDto(0f, "Đang thịnh hành"))
            .ToList();
    }
}
