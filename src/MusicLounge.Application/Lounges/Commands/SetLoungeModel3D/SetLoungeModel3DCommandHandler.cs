using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeModel3D;

internal sealed class SetLoungeModel3DCommandHandler : IRequestHandler<SetLoungeModel3DCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetLoungeModel3DCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetLoungeModel3DCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        // null = xoa model that, quay lai dung scene mau dung code (khong loi, khong crash frontend).
        lounge.Model3DUrl = request.ModelUrl;
        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
