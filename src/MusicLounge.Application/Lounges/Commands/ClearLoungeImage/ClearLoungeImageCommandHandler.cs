using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.ClearLoungeImage;

/// <summary>
/// MLACP-506. PUT /lounges/{id}/image bắt buộc một URL không rỗng, nên ảnh đại diện chỉ ĐỔI được chứ không GỠ được: ảnh
/// vi phạm bản quyền hay file đã mất trên máy chủ phải thay bằng một ảnh giữ chỗ (đúng việc tab mobile đã phải làm khi dọn
/// dữ liệu 29/09). Ảnh đại diện vốn được phép trống (cột nullable, mọi DTO là string?) — phòng trà mới tạo chưa có ảnh.
/// Cùng quyền với đặt ảnh: chủ phòng trà hoặc Admin.
/// </summary>
internal sealed class ClearLoungeImageCommandHandler : IRequestHandler<ClearLoungeImageCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ClearLoungeImageCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ClearLoungeImageCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa ảnh venue này.");

        lounge.PrimaryImageUrl = null;
        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
