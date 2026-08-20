using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeBusinessLicense;

internal sealed class SetLoungeBusinessLicenseCommandHandler : IRequestHandler<SetLoungeBusinessLicenseCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetLoungeBusinessLicenseCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetLoungeBusinessLicenseCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        lounge.BusinessLicenseUrl = request.DocumentUrl;
        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
