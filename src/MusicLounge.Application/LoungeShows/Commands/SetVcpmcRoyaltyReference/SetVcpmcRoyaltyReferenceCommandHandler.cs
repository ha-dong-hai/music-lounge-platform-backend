using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.SetVcpmcRoyaltyReference;

// D19: khac D18 (van ban Nha nuoc, phai nop truoc 7 ngay lam viec luc nop duyet), hop dong
// li-xang VCPMC la thoa thuan dan su, luat chi yeu cau tra truoc khi show DIEN RA — nen cho
// Owner khai bao bat ky luc nao truoc khi show Ongoing/Ended/Cancelled (khong bo hep Draft-only
// nhu SetLegalApprovalReference).
internal sealed class SetVcpmcRoyaltyReferenceCommandHandler
    : IRequestHandler<SetVcpmcRoyaltyReferenceCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetVcpmcRoyaltyReferenceCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetVcpmcRoyaltyReferenceCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền sửa event này.");

        if (show.Status is LoungeShowStatus.Ongoing or LoungeShowStatus.Ended or LoungeShowStatus.Cancelled)
            throw new DomainException("Không thể khai báo tác quyền VCPMC sau khi show đã bắt đầu/kết thúc/hủy.");

        show.VcpmcRoyaltyReference = request.VcpmcRoyaltyReference;
        showRepo.Update(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
