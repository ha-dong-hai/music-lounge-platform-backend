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
/// Nên file được chuyển sang vùng lưu riêng tư ngay, đúng cách ảnh CCCD đang được xử lý, và ai muốn
/// xem phải đi qua GetLoungeBusinessLicenseQuery vốn có kiểm quyền.
///
/// MLACP-485 (23/09/2026) — ĐÍNH CHÍNH ĐOẠN TRÊN. Bản trước của chú thích này nói thêm hai ý, cả
/// hai đều đã bị đo và bác bỏ:
///
/// 1. "URL trong DTO không còn tải trực tiếp được" — SAI. objects.copy của GCS kế thừa custom
///    metadata, nên firebaseStorageDownloadTokens đi theo sang bản riêng tư. Đo thật: gọi object
///    private-uploads/… bằng chính token cũ trả HTTP 200 và tải đủ 927KB; không kèm token mới 403.
///    Việc chuyển sang vùng riêng tư KHÔNG tự nó làm file hết tải được (DEF-BE-04).
///
/// 2. "tránh phải bỏ trường khỏi DTO — client đang dùng" — SAI nốt. Đã grep mlacp-ui và cả hai app
///    Flutter: KHÔNG nơi nào đọc businessLicenseUrl. Trường đã được bỏ khỏi LoungeListItemDto.
///
/// Bài học: hai tiền đề đó đứng vững thì quyết định cũ đúng; không ai đo chúng, nên một cột vốn
/// "luôn null nên vô hại" trở thành đường phát tán ngay khi handler này bắt đầu ghi vào.
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
