using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.SetLoungeBusinessLicense;

/// <summary>
/// MusicLounge.BusinessLicenseUrl đã có cột từ lâu và chưa từng có đường nào ghi vào — cùng loại
/// với ba cột chính sách hoàn vé phải quay lại sửa ở MLACP-288.
///
/// Điểm khác biệt duy nhất, và là lý do handler này không chỉ gán thẳng chuỗi vào cột: giấy phép
/// kinh doanh là giấy tờ định danh một doanh nghiệp, còn BusinessLicenseUrl thì ĐANG NẰM TRONG
/// LoungeListItemDto, tức là danh sách venue công khai (GET /lounges là AllowAnonymous). Hôm nay
/// điều đó vô hại vì cột luôn null. Ghi một URL public vào đó sẽ biến chính commit này thành thứ
/// phát tán giấy phép kinh doanh của mọi venue ra ngoài.
///
/// Nên file được chuyển sang vùng lưu riêng tư ngay, đúng cách ảnh CCCD đang được xử lý: URL trong
/// DTO không còn tải trực tiếp được, và ai muốn xem phải đi qua GetLoungeBusinessLicenseQuery vốn
/// có kiểm quyền. Cách này cũng tránh phải bỏ trường khỏi DTO — đổi cấu trúc response là thứ phía
/// client đang dùng không được phép hứng chịu mà không báo trước.
/// </summary>
internal sealed class SetLoungeBusinessLicenseCommandHandler
    : IRequestHandler<SetLoungeBusinessLicenseCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;

    public SetLoungeBusinessLicenseCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    public async Task<Unit> Handle(SetLoungeBusinessLicenseCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        lounge.BusinessLicenseUrl = await _fileStorage.RelocateToPrivateAsync(request.DocumentUrl, ct);
        repo.Update(lounge);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
