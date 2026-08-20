using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeAreaLayoutImage;

internal sealed class SetLoungeAreaLayoutImageCommandHandler : IRequestHandler<SetLoungeAreaLayoutImageCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetLoungeAreaLayoutImageCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetLoungeAreaLayoutImageCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        // null = xoa anh so do that, khu vuc chuyen ve dung auto-layout khi hien ban do.
        lounge.AreaLayoutImageUrl = request.ImageUrl;
        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
