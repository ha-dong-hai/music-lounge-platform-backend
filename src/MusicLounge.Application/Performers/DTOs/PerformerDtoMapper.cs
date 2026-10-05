using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Performers.DTOs;

// No eager-loading in the generic IRepository, so Performer.Genres never comes back populated from
// GetPagedAsync/GetByIdAsync — this stitches PerformerGenre + MusicGenre in separately, the same
// dictionary-lookup pattern used elsewhere in this codebase (e.g. CancelLoungeShowCommandHandler's
// priceById) instead of pulling in a full ORM-level Include.
internal static class PerformerDtoMapper
{
    /// <param name="viewer">MLACP-651: người đang xem. GET /performers là danh mục DÙNG CHUNG giữa mọi phòng trà (để chọn
    /// nghệ sĩ diễn khách), nhưng <c>ContactEmail</c> là dữ liệu cá nhân của nghệ sĩ, do người tạo hồ sơ nhập để nghệ sĩ
    /// nhận liên kết xác nhận tiền ủng hộ. Trước đây MỌI chủ phòng trà đọc được email của nghệ sĩ do phòng trà khác quản lý
    /// (đo 05/10/2026). Nay chỉ người tạo hồ sơ và Admin nhận trường này — cùng quy tắc với sửa hồ sơ
    /// (UpdatePerformerCommandHandler) và tài khoản ngân hàng nghệ sĩ (BankAccountAccess).</param>
    public static async Task<IReadOnlyList<PerformerDto>> MapAsync(
        IUnitOfWork uow, IReadOnlyList<Performer> performers, ICurrentUserService viewer, CancellationToken ct)
    {
        var laAdmin = viewer.Role == Roles.Admin;
        bool thayEmail(Performer p) => laAdmin || (viewer.IsAuthenticated && p.CreatedByUserId == viewer.UserId);
        if (performers.Count == 0) return [];

        var performerIds = performers.Select(p => p.Id).ToHashSet();
        var genreLinks = await uow.Repository<PerformerGenre, Guid>().FindAsync(
            g => performerIds.Contains(g.PerformerId), ct);

        var genreIds = genreLinks.Select(l => l.GenreId).Distinct().ToList();
        var genres = genreIds.Count == 0
            ? []
            : await uow.Repository<MusicGenre, Guid>().FindAsync(g => genreIds.Contains(g.Id), ct);
        var genreNameById = genres.ToDictionary(g => g.Id, g => g.Name);

        var genresByPerformer = genreLinks.GroupBy(l => l.PerformerId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.GenreId).ToList());

        var socialLinks = await uow.Repository<PerformerSocialLink, Guid>().FindAsync(
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
                socialLinksByPerformer.GetValueOrDefault(p.Id, []),
                thayEmail(p) ? p.ContactEmail : null);
        }).ToList();
    }
}
