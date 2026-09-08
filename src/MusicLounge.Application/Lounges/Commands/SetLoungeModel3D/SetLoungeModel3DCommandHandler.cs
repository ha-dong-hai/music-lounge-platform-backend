using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeModel3D;

/// <summary>
/// Cột Model3DUrl có sẵn, IFileStorageService.SaveModel3DAsync viết xong và cài đặt đầy đủ,
/// UploadModel3DValidator viết xong kèm giới hạn 30MB — và không thứ nào trong ba thứ đó được gọi
/// từ đâu cả. Cả một đường ống dựng hoàn chỉnh, thiếu đúng cái vòi.
///
/// Khác với tour ảo 360° (nhiều ảnh panorama nối nhau qua hotspot), đây là một file .glb/.gltf duy
/// nhất dựng tay cho không gian phòng trà.
/// </summary>
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

        // Mô hình 3D là nội dung trưng bày công khai, không phải giấy tờ — nên nó ở lại vùng file
        // công khai, khác hẳn giấy phép kinh doanh ngay bên cạnh.
        lounge.Model3DUrl = request.ModelUrl;
        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
