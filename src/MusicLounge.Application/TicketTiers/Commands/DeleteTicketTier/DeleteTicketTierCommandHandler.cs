using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.TicketTiers.Commands.DeleteTicketTier;

// Hard delete - chi ap dung khi show con Draft nen chua the co ve da ban (giong ly do cho phep
// DeleteLoungeShow). TicketPrice.TierId la FK bat buoc (khong nullable) nen EF Core mac dinh
// cascade delete cung, khong can tu tay xoa tung price.
internal sealed class DeleteTicketTierCommandHandler : IRequestHandler<DeleteTicketTierCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteTicketTierCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteTicketTierCommand request, CancellationToken ct)
    {
        var tierRepo = _uow.Repository<TicketTier, int>();
        var tier = await tierRepo.GetByIdAsync(request.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), request.TierId);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xóa hạng vé này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể xóa hạng vé khi event còn ở trạng thái Draft.");

        tierRepo.Remove(tier);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
