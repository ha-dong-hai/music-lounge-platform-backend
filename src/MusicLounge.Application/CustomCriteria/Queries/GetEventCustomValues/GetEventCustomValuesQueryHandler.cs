using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.CustomCriteria;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.CustomCriteria.Queries.GetEventCustomValues;

internal sealed class GetEventCustomValuesQueryHandler
    : IRequestHandler<GetEventCustomValuesQuery, IReadOnlyList<EventCustomValueDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetEventCustomValuesQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EventCustomValueDto>> Handle(
        GetEventCustomValuesQuery request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        // Đúng luật quyền của lệnh GHI (SetEventCustomValuesCommandHandler): chủ phòng trà của chính buổi
        // diễn đó, hoặc Admin. Tiêu chí riêng là cách phòng trà tự phân loại buổi diễn của mình — không có
        // lý do gì để phòng trà khác đọc được, và nới ở phía đọc thì coi như bỏ luôn hàng rào ở phía ghi.
        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền xem tiêu chí của buổi diễn này.");

        var values = await _uow.Repository<EventCustomValue, int>()
            .FindAsync(v => v.ShowId == request.ShowId, ct);
        if (values.Count == 0) return [];

        var criteriaIds = values.Select(v => v.CriteriaId).Distinct().ToList();
        var criteria = (await _uow.Repository<CustomCriteriaEntity, int>()
                .FindAsync(c => criteriaIds.Contains(c.Id), ct))
            .ToDictionary(c => c.Id);

        return values
            // Tiêu chí đã bị xoá hẳn khỏi bảng định nghĩa thì không dựng được ô nhập; bỏ qua thay vì ném
            // lỗi cho cả màn hình vì một dòng mồ côi.
            .Where(v => criteria.ContainsKey(v.CriteriaId))
            .Select(v =>
            {
                var c = criteria[v.CriteriaId];
                // Dùng ĐÚNG hàm mà lệnh ghi dùng để từ chối, không viết lại phép so ở đây: một luật, một
                // chỗ. Giá trị sai vẫn còn từ trước khi MLACP-470 dựng hàng rào, và ô chọn không khớp
                // lựa chọn nào thì màn hình hiện ô TRỐNG — trông như chưa đặt, rồi lần Lưu sau xoá mất.
                return new EventCustomValueDto(
                    c.Id, c.Name, c.Key, c.DataType, c.Options, c.IsActive, v.Value,
                    CustomCriteriaValue.LoiNeuCo(c.DataType, c.Options, v.Value));
            })
            .OrderBy(v => v.Name, StringComparer.CurrentCulture)
            .ToList();
    }
}
