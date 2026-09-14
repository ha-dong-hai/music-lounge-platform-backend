using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetKycReviewQueue;

/// <param name="Status">Defaults to Pending — the only status with work attached to it.</param>
public sealed record GetKycReviewQueueQuery(
    KycReviewStatus Status = KycReviewStatus.Pending,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<KycReviewItemDto>>;

/// <param name="CitizenCardNumberMasked">
/// Last four digits only. Deciding a review needs the images, which have their own authenticated
/// endpoint; a list screen does not need every reviewer's browser history to hold full card numbers.
/// </param>
/// <param name="WithholdingWouldStopIfApproved">
/// Flags the reviews where approving actually changes money — an enterprise declaration is a request
/// to stop deducting tax, and it should not look like the routine case in the queue.
/// </param>
/// <param name="DateOfBirth">MLACP-397. Ngày sinh người nộp khai, để Admin đối chiếu với ảnh CCCD/CMND.</param>
/// <param name="LegalName">MLACP-398. Tên doanh nghiệp đã khai — null với hộ/cá nhân.</param>
/// <param name="HasBusinessLicense">
/// MLACP-398. Phòng trà của người nộp đã có giấy chứng nhận đăng ký kinh doanh chưa — duyệt hồ sơ doanh nghiệp cần nó.
/// </param>
/// <param name="CitizenCardNumberUnreadable">MLACP-401. Số CCCD/CMND đã lưu không còn giải mã được — cần nộp lại.</param>
/// <param name="TaxCodeUnreadable">MLACP-401. Mã số thuế đã lưu không còn giải mã được — cần khai lại.</param>
public sealed record KycReviewItemDto(
    int UserId,
    string FullName,
    DateOnly? DateOfBirth,
    string Email,
    string? CitizenCardNumberMasked,
    DateTimeOffset? CitizenCardSubmittedAt,
    string? CitizenCardReviewStatus,
    string? BusinessType,
    string? TaxCode,
    string? LegalName,
    DateTimeOffset? TaxProfileSubmittedAt,
    string? TaxProfileReviewStatus,
    bool WithholdingWouldStopIfApproved,
    bool HasBusinessLicense,
    bool CitizenCardNumberUnreadable,
    bool TaxCodeUnreadable);
