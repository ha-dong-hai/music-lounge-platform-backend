using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetTaxonomyForAdmin;

internal sealed class GetEventCategoriesForAdminQueryHandler
    : IRequestHandler<GetEventCategoriesForAdminQuery, List<AdminEventCategoryDto>>
{
    private readonly IRepository<EventCategory, int> _repo;

    public GetEventCategoriesForAdminQueryHandler(IRepository<EventCategory, int> repo) => _repo = repo;

    public async Task<List<AdminEventCategoryDto>> Handle(
        GetEventCategoriesForAdminQuery request, CancellationToken ct)
    {
        // KHÔNG lọc IsActive: đây chính là điểm khác biệt với danh mục công khai. Mục đã tắt phải nhìn
        // thấy được thì mới bật lại được.
        var rows = await _repo.FindAsync(_ => true, ct);
        return rows
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .Select(c => new AdminEventCategoryDto(c.Id, c.Name, c.Description, c.IsActive))
            .ToList();
    }
}

internal sealed class GetMusicGenresForAdminQueryHandler
    : IRequestHandler<GetMusicGenresForAdminQuery, List<AdminMusicGenreDto>>
{
    private readonly IRepository<MusicGenre, int> _repo;

    public GetMusicGenresForAdminQueryHandler(IRepository<MusicGenre, int> repo) => _repo = repo;

    public async Task<List<AdminMusicGenreDto>> Handle(
        GetMusicGenresForAdminQuery request, CancellationToken ct)
    {
        var rows = await _repo.FindAsync(_ => true, ct);
        return rows
            .OrderBy(g => g.Name, StringComparer.CurrentCulture)
            .Select(g => new AdminMusicGenreDto(g.Id, g.Name, g.NameEn))
            .ToList();
    }
}
