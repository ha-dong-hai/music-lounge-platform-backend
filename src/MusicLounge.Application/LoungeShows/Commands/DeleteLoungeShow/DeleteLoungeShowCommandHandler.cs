using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.DeleteLoungeShow;

// Xoa that (hard delete) - khac CancelLoungeShow (soft, doi Status=Cancelled, danh cho show da
// co ve/publish). Su kien Draft chua tung publish nen chua the co ve/thanh toan, an toan de xoa
// that; Performance/LoungeShowGenre/Mood/Atmosphere la FK bat buoc (khong nullable) nen EF Core
// mac dinh cascade delete cung, khong can tu tay don tung bang con.
internal sealed class DeleteLoungeShowCommandHandler : IRequestHandler<DeleteLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteLoungeShowCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteLoungeShowCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xóa event này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể xóa event khi còn ở trạng thái Draft.");

        showRepo.Remove(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
