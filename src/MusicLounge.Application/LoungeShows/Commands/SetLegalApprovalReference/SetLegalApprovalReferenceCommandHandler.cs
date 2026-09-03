using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.SetLegalApprovalReference;

// D18: Owner khai bao so van ban/lien ket "van ban chap thuan to chuc bieu dien" (NĐ 144/2020
// Dieu 10) truoc khi nop duyet — chi sua duoc khi con Draft, giong UpdateLoungeShowCommand.
internal sealed class SetLegalApprovalReferenceCommandHandler
    : IRequestHandler<SetLegalApprovalReferenceCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetLegalApprovalReferenceCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetLegalApprovalReferenceCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền sửa event này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể khai báo văn bản chấp thuận khi event còn ở trạng thái Draft.");

        show.LegalApprovalReference = request.LegalApprovalReference;
        showRepo.Update(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
