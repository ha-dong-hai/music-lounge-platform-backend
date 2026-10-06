using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Performers.Queries.GetPerformerSuggestions;

internal sealed class GetPerformerSuggestionsQueryHandler
    : IRequestHandler<GetPerformerSuggestionsQuery, IReadOnlyList<PerformerSuggestionItem>>
{
    private readonly IUnitOfWork _uow;

    public GetPerformerSuggestionsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<PerformerSuggestionItem>> Handle(
        GetPerformerSuggestionsQuery request, CancellationToken ct)
    {
        var keyword = SearchKeyword.Normalize(request.Keyword);
        if (keyword is null) return [];

        // Nghệ sĩ không có tài khoản — hồ sơ do phòng trà tạo trong danh mục dùng chung. Hồ sơ chưa từng xuất hiện ở buổi
        // diễn công khai nào (mới tạo, hoặc chỉ nằm trong bản nháp/hồ sơ chờ duyệt) thì chưa phải thứ sàn đem ra mời người
        // lạ xem. Nên chỉ gợi ý nghệ sĩ có ít nhất một buổi đã qua cổng duyệt ở phòng trà đang hoạt động — cùng hai quy
        // tắc của mọi đường khám phá công khai (ShowDiscoverability), không tự đặt quy tắc thứ ba.
        var chuaCongBo = ShowDiscoverability.AwaitingPublication;
        var dangHoatDong = VenueLifecycle.Operating;
        var (performers, _) = await _uow.Repository<Performer, Guid>().GetPagedAsync(
            p => p.Name.ToLower().Contains(keyword)
                 && p.Performances.Any(pf => !chuaCongBo.Contains(pf.LoungeShow.Status)
                                             && dangHoatDong.Contains(pf.LoungeShow.Lounge.Status)),
            p => p.Performances.Count,
            1, Math.Clamp(request.Limit, 1, 10), ct);

        return performers.Select(p => new PerformerSuggestionItem(p.Id, p.Name, p.AvatarUrl)).ToList();
    }
}
