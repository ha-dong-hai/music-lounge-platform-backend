using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.DeleteLounge;

internal sealed class DeleteLoungeCommandHandler : IRequestHandler<DeleteLoungeCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public DeleteLoungeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteLoungeCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xóa venue này.");

        // DONE WHEN: "Xoa phong tra thanh cong khi khong co su kien" - chan xoa neu con bat ky
        // LoungeShow nao (moi trang thai, khong chi show dang "hoat dong") de tranh mat lich su
        // du lieu cua show da ket thuc/huy.
        var hasAnyShow = await _uow.Repository<LoungeShow, int>()
            .AnyAsync(s => s.LoungeId == request.LoungeId, ct);
        if (hasAnyShow)
            throw new ConflictException("Phòng trà đang có sự kiện, không thể xóa.");

        repo.Remove(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
