using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetMyCitizenCard;

internal sealed class GetMyCitizenCardQueryHandler : IRequestHandler<GetMyCitizenCardQuery, CitizenCardStatusDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IPiiEncryptionService _piiEncryption;

    public GetMyCitizenCardQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _currentUser = currentUser;
        _piiEncryption = piiEncryption;
    }

    public async Task<CitizenCardStatusDto> Handle(GetMyCitizenCardQuery request, CancellationToken ct)
    {
        var user = await _uow.Repository<User, Guid>().GetByIdAsync(_currentUser.UserId, ct)
            ?? throw new NotFoundException(nameof(User), _currentUser.UserId);

        var soGiaiMa = user.CitizenCardNumber is null ? null : _piiEncryption.TryDecrypt(user.CitizenCardNumber);
        var status = user.CitizenCardReviewStatus;

        // Đã duyệt thì nói thẳng là bán được; các trạng thái còn lại dùng ĐÚNG câu mà cổng bán trả về,
        // để màn hình và lỗi khi bấm bán không bao giờ nói hai điều khác nhau.
        var giaiThich = SellerIdentity.IsVerified(status)
            ? "Danh tính người bán đã được xác minh — bạn được mở bán vé và nhận đơn gọi món."
            : status == KycReviewStatus.Rejected && !string.IsNullOrWhiteSpace(user.CitizenCardReviewNote)
                ? $"Hồ sơ CCCD/CMND đã bị từ chối. Lý do: {user.CitizenCardReviewNote} Hãy nộp lại."
                : SellerIdentity.ExplainForSeller(status);

        return new CitizenCardStatusDto(
            user.CitizenCardSubmittedAt,
            status?.ToString(),
            user.CitizenCardReviewedAt,
            user.CitizenCardReviewNote,
            SellerIdentity.MaskCardNumber(soGiaiMa),
            user.CitizenCardNumber is not null && soGiaiMa is null,
            SellerIdentity.IsVerified(status),
            giaiThich);
    }
}
