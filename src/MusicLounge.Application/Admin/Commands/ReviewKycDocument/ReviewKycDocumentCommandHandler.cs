using MediatR;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Admin.Commands.ReviewKycDocument;

/// <summary>
/// The decision step that finding R7 of the đợt-0 audit found missing: documents could be submitted
/// and looked at, and that was all — nothing accepted them, nothing refused them, and no state
/// recorded what anyone had concluded.
///
/// MLACP-289 turned that from a procedural gap into a financial one. Declaring yourself a doanh
/// nghiệp is the only thing that stops the platform withholding tax from you, and it deliberately
/// takes effect only once approved. Without this handler nobody could ever be approved, so the
/// exemption existed and was unreachable.
/// </summary>
internal sealed class ReviewKycDocumentCommandHandler : IRequestHandler<ReviewKycDocumentCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public ReviewKycDocumentCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ReviewKycDocumentCommand request, CancellationToken ct)
    {
        var userRepo = _uow.Repository<User, int>();
        var user = await userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        var now = DateTimeOffset.UtcNow;
        var decision = request.Approve ? KycReviewStatus.Approved : KycReviewStatus.Rejected;

        // MLACP-489: song ngữ. Trước đây nhánh thuế đặt "hồ sơ thuế" rồi ghép sau chữ "Hồ sơ" → người dùng đọc
        // "Hồ sơ hồ sơ thuế đã được duyệt". Nay là tên giấy tờ trần, "Hồ sơ" do câu ghép thêm.
        SongNgu documentName;
        if (request.Document == KycDocument.CitizenCard)
        {
            if (user.CitizenCardSubmittedAt is null)
                throw new DomainException("Người dùng này chưa nộp CCCD/CMND nào để duyệt.");

            user.CitizenCardReviewStatus = decision;
            user.CitizenCardReviewedAt = now;
            user.CitizenCardReviewedBy = _currentUser.UserId;
            user.CitizenCardReviewNote = request.Note;

            // MLACP-399. Chốt họ tên đúng lúc duyệt — Admin vừa đối chiếu họ tên này (cùng ngày sinh, số giấy tờ) với ảnh
            // CCCD/CMND. FullName sửa được bất cứ lúc nào mà không mất trạng thái đã duyệt, nên tài khoản nhận tiền được so
            // với tên đã chốt này. Từ chối thì không còn tên nào được xác nhận.
            user.CitizenCardVerifiedName = request.Approve ? user.FullName : null;
            documentName = new SongNgu("CCCD/CMND", "ID card");
        }
        else
        {
            if (user.TaxProfileSubmittedAt is null)
                throw new DomainException("Người dùng này chưa khai báo hồ sơ thuế nào để duyệt.");

            // MLACP-398. Duyệt hồ sơ doanh nghiệp là xác nhận tổ chức này có thật và tài khoản này đại diện cho nó — và
            // duyệt xong thì nền tảng thôi khấu trừ thuế. Admin cần đủ thứ để đối chiếu: tên doanh nghiệp đã khai, danh
            // tính người đại diện theo pháp luật (CCCD/CMND của tài khoản chủ phòng trà, đã duyệt) và giấy chứng nhận
            // đăng ký kinh doanh của phòng trà. Chỉ chặn khi DUYỆT: từ chối luôn được, và hộ/cá nhân không cần các thứ này.
            if (request.Approve && user.BusinessType == PayeeBusinessType.Enterprise)
            {
                if (string.IsNullOrWhiteSpace(user.LegalName))
                    throw new DomainException(
                        "Chưa duyệt được hồ sơ doanh nghiệp: người nộp chưa khai tên doanh nghiệp — cần khai lại hồ sơ thuế.");

                if (user.CitizenCardReviewStatus != KycReviewStatus.Approved)
                    throw new DomainException(
                        "Chưa duyệt được hồ sơ doanh nghiệp: CCCD/CMND của người đại diện (chủ tài khoản) chưa được duyệt.");

                var venues = await _uow.Repository<MusicLoungeEntity, int>().FindAsync(l => l.OwnerId == user.Id, ct);
                if (!venues.Any(l => !string.IsNullOrWhiteSpace(l.BusinessLicenseUrl)))
                    throw new DomainException(
                        "Chưa duyệt được hồ sơ doanh nghiệp: phòng trà chưa nộp giấy chứng nhận đăng ký kinh doanh để đối chiếu.");
            }

            user.TaxProfileReviewStatus = decision;
            user.TaxProfileReviewNote = request.Note;
            // TaxWithholdingPolicy reads this timestamp and nothing else, so clearing it on a
            // rejection is what actually keeps withholding on — leaving a stale approval behind
            // would quietly exempt a seller whose declaration was just turned down.
            user.TaxProfileVerifiedAt = request.Approve ? now : null;
            user.TaxProfileVerifiedBy = request.Approve ? _currentUser.UserId : null;
            documentName = new SongNgu("thuế", "tax");
        }

        userRepo.Update(user);

        var title = request.Approve
            ? new SongNgu($"Hồ sơ {documentName.Vi} đã được duyệt", $"Your {documentName.En} documents were approved")
            : new SongNgu($"Hồ sơ {documentName.Vi} bị từ chối", $"Your {documentName.En} documents were rejected");

        var body = request.Approve
            ? BuildApprovalMessage(request.Document, user)
            : new SongNgu(
                $"Lý do: {request.Note} Bạn có thể nộp lại sau khi chỉnh sửa.",
                $"Reason: {request.Note} You can resubmit after making changes.");

        // Staged, not saved — NotificationService follows the same contract as ILedgerService, so
        // the single SaveChangesAsync below is what commits both the decision and the message about
        // it. Saving the decision first and notifying afterwards would leave the row uncommitted.
        await _notifications.NotifyAsync(
            user.Id, NotificationType.KycReviewResult, title, body,
            referenceType: "kyc_review", referenceId: user.Id.ToString(), ct: ct);

        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }

    private static SongNgu BuildApprovalMessage(KycDocument document, User user)
    {
        if (document == KycDocument.CitizenCard)
            return new SongNgu(
                "Giấy tờ tùy thân của bạn đã được xác minh.",
                "Your ID document has been verified.");

        return user.BusinessType == PayeeBusinessType.Enterprise
            ? new SongNgu(
                "Hồ sơ doanh nghiệp của bạn đã được xác minh. Từ các giao dịch sau, nền tảng không " +
                "khấu trừ thuế thay cho bạn nữa — bạn tự kê khai và nộp theo quy định.",
                "Your business profile has been verified. From your next transactions, the platform will no longer " +
                "withhold tax on your behalf — you declare and pay it yourself as required by law.")
            : new SongNgu(
                "Hồ sơ thuế của bạn đã được xác minh. Nền tảng tiếp tục khấu trừ và nộp thay thuế " +
                "trên doanh thu mỗi giao dịch của bạn theo NĐ 117/2025/NĐ-CP.",
                "Your tax profile has been verified. The platform will continue to withhold and pay tax on your " +
                "behalf on the revenue of each of your transactions, under Decree 117/2025/ND-CP.");
    }
}
