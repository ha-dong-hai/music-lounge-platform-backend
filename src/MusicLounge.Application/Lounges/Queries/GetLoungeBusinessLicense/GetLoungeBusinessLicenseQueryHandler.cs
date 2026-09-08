using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Queries.GetLoungeBusinessLicense;

/// <summary>
/// Đường duy nhất đọc được giấy phép kinh doanh sau khi nó được chuyển vào vùng lưu riêng tư. Cùng
/// khuôn với GetCitizenCardImageQueryHandler, và vì cùng lý do: file nằm ngoài wwwroot nên không
/// đoán URL mà tải được, và quyền xem phải kiểm ở đây chứ không ở tầng file tĩnh.
/// </summary>
internal sealed class GetLoungeBusinessLicenseQueryHandler
    : IRequestHandler<GetLoungeBusinessLicenseQuery, BusinessLicenseFileDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<GetLoungeBusinessLicenseQueryHandler> _logger;

    public GetLoungeBusinessLicenseQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage,
        ILogger<GetLoungeBusinessLicenseQueryHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<BusinessLicenseFileDto> Handle(
        GetLoungeBusinessLicenseQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>()
            .GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        var isOwner = lounge.OwnerId == _currentUser.UserId;
        if (!isOwner && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xem giấy phép kinh doanh của venue này.");

        if (string.IsNullOrEmpty(lounge.BusinessLicenseUrl))
            throw new NotFoundException("BusinessLicense", request.LoungeId);

        var (content, contentType) = await _fileStorage
            .OpenPrivateFileAsync(lounge.BusinessLicenseUrl, ct);

        // Cùng lý do như khi Admin xem ảnh CCCD của người khác: bản ghi trong DB không lưu ai đã
        // xem, nên nếu không log ở đây thì không còn dấu vết nào cả. Chủ venue xem giấy tờ của
        // chính mình thì không phải sự kiện đáng ghi.
        if (!isOwner)
            _logger.LogWarning(
                "Admin viewed venue business license: LoungeId={LoungeId} OwnerId={OwnerId} " +
                "by AdminUserId={AdminUserId} at {At}",
                lounge.Id, lounge.OwnerId, _currentUser.UserId, DateTimeOffset.UtcNow);

        return new BusinessLicenseFileDto(content, contentType);
    }
}
