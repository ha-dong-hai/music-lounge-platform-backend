using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.TicketTiers.Commands.AssignTicketTierZone;

// MLACP-545 (03/10/2026): sơ đồ chỗ ngồi của buổi diễn (GetShowSeatingMap) chỉ gom khu có ÍT NHẤT một hạng vé gắn vào.
// Hạng vé chỉ gắn được khu lúc TẠO; UpdateTicketTier không nhận ZoneId và khoá sau Draft — nên mọi buổi đã mở bán có
// hạng vé chưa gắn khu thì sơ đồ trống vĩnh viễn (Azure 03/10: 4/4 hạng vé của 2 buổi công khai, 0 khu trên sơ đồ).
//
// QUY TẮC (chủ dự án chọn "gắn khu MỘT lần"):
//  - Buổi còn Draft: gắn/đổi khu thoải mái (chưa ai mua).
//  - Buổi đã rời Draft: CHỈ gắn khi hạng vé CHƯA có khu. Đã có khu thì không đổi — người đã mua vé hạng đó không bị
//    chuyển sang khu khác sau khi trả tiền.
//  - Buổi đã kết thúc/huỷ: không gắn (không còn ai chọn chỗ).
//  - Chỉ hạng vé TẠI CHỖ (Physical); vé xem trực tuyến không có chỗ ngồi.
//  - Khu phải thuộc ĐÚNG phòng trà của buổi diễn và đang hoạt động.
internal sealed class AssignTicketTierZoneCommandHandler : IRequestHandler<AssignTicketTierZoneCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public AssignTicketTierZoneCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(AssignTicketTierZoneCommand request, CancellationToken ct)
    {
        var tierRepo = _uow.Repository<TicketTier, Guid>();
        var tier = await tierRepo.GetByIdAsync(request.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), request.TierId);

        var show = await _uow.Repository<LoungeShow, Guid>().GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền sửa hạng vé này.");

        if (tier.AccessType != AccessType.Physical)
            throw new DomainException("Chỉ hạng vé tại chỗ mới gắn được khu ghế.");

        if (show.Status is LoungeShowStatus.Ended or LoungeShowStatus.Cancelled)
            throw new DomainException("Buổi diễn đã kết thúc hoặc đã huỷ, không gắn khu ghế được nữa.");

        if (show.Status != LoungeShowStatus.Draft && tier.ZoneId.HasValue)
            throw new DomainException("Hạng vé đã có khu ghế và buổi diễn đã mở bán — không đổi khu được, để người đã mua không bị chuyển chỗ.");

        var zone = await _uow.Repository<SeatingZone, Guid>().GetByIdAsync(request.ZoneId, ct);
        if (zone is null || zone.LoungeId != show.LoungeId)
            throw new DomainException("Khu ghế không thuộc phòng trà của buổi diễn này.");
        if (!zone.IsActive)
            throw new DomainException("Khu ghế này đang tạm ngưng.");

        tier.ZoneId = zone.Id;
        tierRepo.Update(tier);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
