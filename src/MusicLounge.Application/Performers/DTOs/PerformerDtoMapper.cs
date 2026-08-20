using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Performers.DTOs;

// No eager-loading in the generic IRepository, so Performer.Genres never comes back populated from
// GetPagedAsync/GetByIdAsync — this stitches PerformerGenre + MusicGenre in separately, the same
// dictionary-lookup pattern used elsewhere in this codebase (e.g. CancelLoungeShowCommandHandler's
// priceById) instead of pulling in a full ORM-level Include.
internal static class PerformerDtoMapper
{
    public static async Task<IReadOnlyList<PerformerDto>> MapAsync(
        IUnitOfWork uow, IReadOnlyList<Performer> performers, CancellationToken ct)
    {
        if (performers.Count == 0) return [];

        var performerIds = performers.Select(p => p.Id).ToHashSet();
        var genreLinks = await uow.Repository<PerformerGenre, int>().FindAsync(
            g => performerIds.Contains(g.PerformerId), ct);

        var genreIds = genreLinks.Select(l => l.GenreId).Distinct().ToList();
        var genres = genreIds.Count == 0
            ? []
            : await uow.Repository<MusicGenre, int>().FindAsync(g => genreIds.Contains(g.Id), ct);
        var genreNameById = genres.ToDictionary(g => g.Id, g => g.Name);

        var genresByPerformer = genreLinks.GroupBy(l => l.PerformerId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.GenreId).ToList());

        var socialLinks = await uow.Repository<PerformerSocialLink, int>().FindAsync(
            s => performerIds.Contains(s.PerformerId), ct);
        var socialLinksByPerformer = socialLinks.GroupBy(s => s.PerformerId)
            .ToDictionary(g => g.Key, g => g
                .Select(s => new PerformerSocialLinkDto(s.Id, s.Platform.ToString(), s.Url, s.DisplayName))
                .ToList());

        return performers.Select(p =>
        {
            var performerGenreIds = genresByPerformer.GetValueOrDefault(p.Id, []);
            return new PerformerDto(
                p.Id,
                p.Name,
                p.AvatarUrl,
                p.Bio,
                p.Type.ToString(),
                p.CreatedByUserId,
                performerGenreIds,
                performerGenreIds.Select(id => genreNameById.GetValueOrDefault(id, string.Empty)).ToList(),
                socialLinksByPerformer.GetValueOrDefault(p.Id, []));
        }).ToList();
    }
}
