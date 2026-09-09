using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Mutes.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Mutes.Queries.GetMyMutedLounges;

internal sealed class GetMyMutedLoungesQueryHandler
    : IQueryHandler<GetMyMutedLoungesQuery, IReadOnlyList<MutedLoungeDto>>
{
    private readonly IRepository<LoungeMute, int> _muteRepo;
    private readonly IRepository<Domain.Entities.MusicLounge, int> _loungeRepo;
    private readonly ICurrentUserService _currentUser;

    public GetMyMutedLoungesQueryHandler(
        IRepository<LoungeMute, int> muteRepo,
        IRepository<Domain.Entities.MusicLounge, int> loungeRepo,
        ICurrentUserService currentUser)
    {
        _muteRepo = muteRepo;
        _loungeRepo = loungeRepo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MutedLoungeDto>> Handle(
        GetMyMutedLoungesQuery request, CancellationToken ct)
    {
        var mutes = await _muteRepo.FindAsync(m => m.UserId == _currentUser.UserId, ct);
        if (mutes.Count == 0) return [];

        var loungeIds = mutes.Select(m => m.LoungeId).ToList();
        var lounges = await _loungeRepo.FindAsync(l => loungeIds.Contains(l.Id), ct);
        var mutedAt = mutes.ToDictionary(m => m.LoungeId, m => m.CreatedAt);

        // Sắp mới nhất trước ở phía client: provider SQLite dùng trong test không ORDER BY được cột
        // DateTimeOffset — cùng giới hạn đã ghi khắp codebase này.
        return lounges
            .Select(l => new MutedLoungeDto(
                l.Id, l.Name, l.Address.District, l.Address.City, mutedAt[l.Id]))
            .OrderByDescending(x => x.MutedAt)
            .ToList();
    }
}
