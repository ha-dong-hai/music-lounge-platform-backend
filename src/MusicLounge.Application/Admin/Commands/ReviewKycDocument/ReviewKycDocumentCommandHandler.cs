using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

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

        string documentName;
        if (request.Document == KycDocument.CitizenCard)
        {
            if (user.CitizenCardSubmittedAt is null)
                throw new DomainException("Người dùng này chưa nộp CCCD/CMND nào để duyệt.");

            user.CitizenCardReviewStatus = decision;
            user.CitizenCardReviewedAt = now;
            user.CitizenCardReviewedBy = _currentUser.UserId;
            user.CitizenCardReviewNote = request.Note;
            documentName = "CCCD/CMND";
        }
        else
        {
            if (user.TaxProfileSubmittedAt is null)
                throw new DomainException("Người dùng này chưa khai báo hồ sơ thuế nào để duyệt.");

            user.TaxProfileReviewStatus = decision;
            user.TaxProfileReviewNote = request.Note;
            // TaxWithholdingPolicy reads this timestamp and nothing else, so clearing it on a
            // rejection is what actually keeps withholding on — leaving a stale approval behind
            // would quietly exempt a seller whose declaration was just turned down.
            user.TaxProfileVerifiedAt = request.Approve ? now : null;
            user.TaxProfileVerifiedBy = request.Approve ? _currentUser.UserId : null;
            documentName = "hồ sơ thuế";
        }

        userRepo.Update(user);

        var title = request.Approve
            ? $"Hồ sơ {documentName} đã được duyệt"
            : $"Hồ sơ {documentName} bị từ chối";

        var body = request.Approve
            ? BuildApprovalMessage(request.Document, user)
            : $"Lý do: {request.Note} Bạn có thể nộp lại sau khi chỉnh sửa.";

        // Staged, not saved — NotificationService follows the same contract as ILedgerService, so
        // the single SaveChangesAsync below is what commits both the decision and the message about
        // it. Saving the decision first and notifying afterwards would leave the row uncommitted.
        await _notifications.NotifyAsync(
            user.Id, NotificationType.KycReviewResult, title, body,
            referenceType: "kyc-review", referenceId: user.Id.ToString(), ct: ct);

        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }

    private static string BuildApprovalMessage(KycDocument document, User user)
    {
        if (document == KycDocument.CitizenCard)
            return "Giấy tờ tùy thân của bạn đã được xác minh.";

        return user.BusinessType == PayeeBusinessType.Enterprise
            ? "Hồ sơ doanh nghiệp của bạn đã được xác minh. Từ các giao dịch sau, nền tảng không " +
              "khấu trừ thuế thay cho bạn nữa — bạn tự kê khai và nộp theo quy định."
            : "Hồ sơ thuế của bạn đã được xác minh. Nền tảng tiếp tục khấu trừ và nộp thay thuế " +
              "trên doanh thu mỗi giao dịch của bạn theo NĐ 117/2025/NĐ-CP.";
    }
}
