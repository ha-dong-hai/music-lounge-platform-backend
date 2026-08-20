using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.StartLoungeShow;

// Show KHONG co Livestream (offline thuan) khong bao gio chuyen Ongoing qua
// StartLivestreamCommandHandler duoc - can lenh rieng cho Staff tai quay bam "bat dau show".
// Show co tier Livestream phai dung StartLivestreamCommand (lam ca 2 viec: mo stream + set show).
internal sealed class StartLoungeShowCommandHandler : IRequestHandler<StartLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamRepository _livestreamRepo;

    public StartLoungeShowCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILivestreamRepository livestreamRepo)
    {
        _uow = uow;
        _currentUser = currentUser;
        _livestreamRepo = livestreamRepo;
    }

    public async Task<Unit> Handle(StartLoungeShowCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        // D6: Staff chi duoc bat dau show cua venue duoc phan cong; Owner cua venue do cung duoc.
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền bắt đầu show của venue này.");

        if (show.Status != LoungeShowStatus.Published)
            throw new DomainException("Chỉ có thể bắt đầu show đang ở trạng thái Published.");

        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream is not null)
            throw new DomainException(
                "Show này có livestream — dùng chức năng bắt đầu livestream thay vì lệnh này.");

        // D19: phai tra tac quyen VCPMC truoc khi show dien ra
        if (string.IsNullOrWhiteSpace(show.VcpmcRoyaltyReference))
            throw new DomainException(
                "Cần khai báo đã thanh toán tác quyền VCPMC trước khi bắt đầu show.");

        show.Status = LoungeShowStatus.Ongoing;
        show.ActualStart = DateTimeOffset.UtcNow;
        showRepo.Update(show);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
