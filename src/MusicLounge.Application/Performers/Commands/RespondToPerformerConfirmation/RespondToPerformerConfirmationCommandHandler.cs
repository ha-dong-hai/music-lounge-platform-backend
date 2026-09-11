using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Performers.Commands.RespondToPerformerConfirmation;

/// <summary>
/// MLACP-364 — nghệ sĩ trả lời qua liên kết một lần.
///
/// <list type="bullet">
/// <item>Tài khoản ngân hàng: xác nhận → tài khoản được đánh dấu đã xác minh; báo sai → giữ chưa xác
/// minh và báo Admin (có thể là tài khoản nhập nhầm, hoặc cố ý).</item>
/// <item>Đã nhận donate: xác nhận hay báo chưa nhận đều đi vào nhật ký bằng chứng; báo chưa nhận thì
/// tự mở một khiếu nại để Admin xử lý.</item>
/// </list>
///
/// <para>Liên kết dùng đúng một lần, trong thời hạn, và phải kèm sự đồng ý xử lý dữ liệu: thỏa thuận của
/// nghệ sĩ là với phòng trà chứ không phải với nền tảng, nên nền tảng xin sự đồng ý trực tiếp.</para>
/// </summary>
internal sealed class RespondToPerformerConfirmationCommandHandler
    : IRequestHandler<RespondToPerformerConfirmationCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IPiiEncryptionService _pii;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigService _config;
    private readonly IAsyncKeyedLock _lock;

    public RespondToPerformerConfirmationCommandHandler(
        IUnitOfWork uow, IPiiEncryptionService pii, INotificationService notifications,
        ISystemConfigService config, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _pii = pii;
        _notifications = notifications;
        _config = config;
        _lock = @lock;
    }

    public async Task<Unit> Handle(RespondToPerformerConfirmationCommand request, CancellationToken ct)
    {
        // Bấm hai lần liên tiếp không được tạo hai khiếu nại hay hai dòng nhật ký.
        await using var _ = await _lock.AcquireAsync(
            $"performer-confirmation:{PerformerConfirmations.HashToken(request.Token)}", ct);

        var confirmation = await PerformerConfirmations.FindByTokenAsync(_uow, request.Token, ct);
        var now = DateTimeOffset.UtcNow;

        if (confirmation.UsedAt is not null)
            throw new DomainException("Liên kết này đã được dùng — mỗi liên kết chỉ dùng được một lần.");
        if (now > confirmation.ExpiresAt)
            throw new DomainException("Liên kết đã hết hạn. Hãy liên hệ phòng trà để được gửi liên kết mới.");
        if (!request.ConsentToDataProcessing)
            throw new DomainException(
                "Cần bạn đồng ý cho MusicLounge xử lý email và thông tin tài khoản nhận tiền của bạn " +
                "(Luật Bảo vệ dữ liệu cá nhân) trước khi xác nhận hoặc báo sai.");

        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(confirmation.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), confirmation.PerformerId);
        var dispute = string.Equals(request.Decision, "Dispute", StringComparison.OrdinalIgnoreCase);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        switch (confirmation.Purpose)
        {
            case PerformerConfirmationPurpose.BankAccount:
                await RespondToBankAccountAsync(confirmation, performer, dispute, note, ct);
                break;
            case PerformerConfirmationPurpose.DonationReceipt:
                await RespondToDonationReceiptAsync(confirmation, performer, dispute, note, now, ct);
                break;
        }

        performer.DataConsentAt ??= now;
        _uow.Repository<Performer, int>().Update(performer);

        confirmation.UsedAt = now;
        confirmation.Outcome = dispute ? PerformerConfirmationOutcome.Disputed : PerformerConfirmationOutcome.Confirmed;
        confirmation.ConsentGivenAt = now;
        confirmation.Note = note;
        _uow.Repository<PerformerConfirmation, int>().Update(confirmation);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task RespondToBankAccountAsync(
        PerformerConfirmation confirmation, Performer performer, bool dispute, string? note, CancellationToken ct)
    {
        var repo = _uow.Repository<BankAccount, int>();
        var account = confirmation.BankAccountId is int id ? await repo.GetByIdAsync(id, ct) : null;
        if (account is null)
            throw new DomainException("Tài khoản ngân hàng trong liên kết này không còn tồn tại.");

        // Liên kết cũ không được xác nhận thông tin mà nghệ sĩ chưa từng thấy.
        if (PerformerConfirmations.FingerprintOf(account) != confirmation.BankAccountFingerprint)
            throw new DomainException(
                "Thông tin tài khoản đã được sửa sau khi gửi liên kết này — hãy dùng liên kết mới nhất trong email.");

        account.IsVerified = !dispute;
        repo.Update(account);

        if (!dispute) return;

        var masked = PerformerConfirmations.MaskAccountNumber(_pii.Decrypt(account.AccountNumber));
        var admins = await _uow.Repository<User, int>().FindAsync(u => u.Role == UserRole.Admin, ct);
        foreach (var admin in admins)
        {
            await _notifications.NotifyAsync(
                admin.Id,
                NotificationType.SecurityAlert,
                "Nghệ sĩ báo tài khoản nhận tiền không phải của họ",
                $"Nghệ sĩ \"{performer.Name}\" báo tài khoản {account.BankName} {masked} (tài khoản #{account.Id}) " +
                $"không phải của họ. Tài khoản do người dùng #{performer.CreatedByUserId} nhập." +
                (note is null ? "" : $" Ghi chú của nghệ sĩ: {note}"),
                referenceType: "bank_account",
                referenceId: account.Id.ToString(),
                ct: ct);
        }
    }

    private async Task RespondToDonationReceiptAsync(
        PerformerConfirmation confirmation, Performer performer, bool dispute, string? note,
        DateTimeOffset now, CancellationToken ct)
    {
        var donationId = confirmation.DonationId
            ?? throw new DomainException("Liên kết này không gắn với khoản donate nào.");
        var donation = await _uow.Repository<Donation, int>().GetByIdAsync(donationId, ct)
            ?? throw new NotFoundException(nameof(Donation), donationId);

        var detail = dispute
            ? "Nghệ sĩ báo CHƯA nhận được khoản phòng trà đã báo chuyển — trả lời qua liên kết gửi tới email đã đăng ký."
            : "Nghệ sĩ xác nhận đã nhận — trả lời qua liên kết gửi tới email đã đăng ký.";
        if (note is not null) detail += $" Ghi chú: {note}";

        await DonationEvidence.AppendAsync(_uow, donationId,
            dispute ? DonationEventType.PerformerDisputedReceipt : DonationEventType.PerformerConfirmedReceipt,
            actorUserId: null, reference: donation.PaymentRef, detail: detail, ct: ct);

        if (!dispute) return;

        var slaHours = await _config.GetIntAsync(ConfigKeys.ComplaintSlaHours, 72, ct);
        _uow.Repository<Complaint, int>().Add(new Complaint
        {
            ComplainantUserId = null,
            TargetType = "donation",
            TargetId = donationId,
            Category = ComplaintCategory.DonationNotPaid,
            Description = $"Nghệ sĩ \"{performer.Name}\" báo chưa nhận được khoản donate #{donationId} mà phòng trà " +
                          $"đã báo chuyển (mã chuyển khoản {donation.PaymentRef})." +
                          (note is null ? "" : $" Ghi chú của nghệ sĩ: {note}"),
            Status = ComplaintStatus.Open,
            CreatedAt = now,
            SlaDeadline = now.AddHours(slaHours)
        });
    }
}
