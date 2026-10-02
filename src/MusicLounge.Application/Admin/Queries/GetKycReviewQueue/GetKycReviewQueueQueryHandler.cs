using MediatR;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Admin.Queries.GetKycReviewQueue;

internal sealed class GetKycReviewQueueQueryHandler
    : IRequestHandler<GetKycReviewQueueQuery, PaginatedResult<KycReviewItemDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IUserRepository _users;
    private readonly IPiiEncryptionService _piiEncryption;

    public GetKycReviewQueueQueryHandler(IUnitOfWork uow, IUserRepository users, IPiiEncryptionService piiEncryption)
    {
        _uow = uow;
        _users = users;
        _piiEncryption = piiEncryption;
    }

    public async Task<PaginatedResult<KycReviewItemDto>> Handle(
        GetKycReviewQueueQuery request, CancellationToken ct)
    {
        // A user belongs in the queue if EITHER document sits at the requested status — the two are
        // submitted independently, and waiting for both before showing anything would hide a card
        // that has been ready for review for a week. Lọc, sắp và cắt trang: UserRepository.GetKycReviewPageAsync
        // (MLACP-505 — chỉ nạp đầy đủ người thuộc trang, không còn nạp cả hàng đợi).
        var (pageUsers, total) = await _users.GetKycReviewPageAsync(request.Status, request.Page, request.PageSize, ct);

        // MLACP-398: duyệt hồ sơ doanh nghiệp cần giấy chứng nhận đăng ký kinh doanh của phòng trà — cho Admin thấy ngay
        // trên danh sách hồ sơ nào đã có, như danh sách duyệt phòng trà đang làm.
        var pageUserIds = pageUsers.Select(u => u.Id).ToList();
        var licensedOwnerIds = (await _uow.Repository<MusicLoungeEntity, Guid>().FindAsync(
                l => pageUserIds.Contains(l.OwnerId) && l.BusinessLicenseUrl != null && l.BusinessLicenseUrl != "", ct))
            .Select(l => l.OwnerId)
            .ToHashSet();

        var page = pageUsers
            .Select(u => new KycReviewItemDto(
                u.Id,
                u.FullName,
                u.DateOfBirth,
                u.Email,
                Mask(Decrypt(u.CitizenCardNumber)),
                u.CitizenCardSubmittedAt,
                u.CitizenCardReviewStatus?.ToString(),
                u.BusinessType?.ToString(),
                Decrypt(u.TaxCode),
                u.LegalName,
                u.TaxProfileSubmittedAt,
                u.TaxProfileReviewStatus?.ToString(),
                u.BusinessType == PayeeBusinessType.Enterprise
                    && u.TaxProfileReviewStatus == KycReviewStatus.Pending,
                licensedOwnerIds.Contains(u.Id),
                // MLACP-401: giá trị mã hoá bằng khoá đã mất — báo rõ thay vì 500 cho cả hàng đợi.
                u.CitizenCardNumber is not null && Decrypt(u.CitizenCardNumber) is null,
                u.TaxCode is not null && Decrypt(u.TaxCode) is null))
            .ToList();

        return new PaginatedResult<KycReviewItemDto>(page, request.Page, request.PageSize, total);
    }

    private string? Decrypt(string? ciphertext)
        => ciphertext is null ? null : _piiEncryption.TryDecrypt(ciphertext);

    private static string? Mask(string? cardNumber)
        => cardNumber is null || cardNumber.Length < 4
            ? cardNumber
            : new string('•', cardNumber.Length - 4) + cardNumber[^4..];
}
