using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Admin.Commands.ReviewPayoutBankAccount;

/// <summary>
/// MLACP-395. Trước task này tài khoản ngân hàng của phòng trà không có đường nào để được xác minh — <c>IsVerified</c>
/// chỉ được đặt cho tài khoản của nghệ sĩ (nghệ sĩ tự xác nhận qua liên kết, MLACP-364). Giải ngân nay đòi tài khoản đã
/// xác minh (<c>PayeeVerification</c>), nên Admin cần bước này.
///
/// <para>Chỉ xác minh khi chủ phòng trà đã được duyệt CCCD/CMND: Admin đối chiếu tên chủ tài khoản với giấy tờ đã duyệt —
/// xác minh một tài khoản cho một danh tính chưa ai kiểm thì không chứng minh được gì. Chủ phòng trà sửa tài khoản thì
/// <c>UpdateBankAccount</c> tự bỏ xác minh.</para>
/// </summary>
internal sealed class ReviewPayoutBankAccountCommandHandler : IRequestHandler<ReviewPayoutBankAccountCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;

    public ReviewPayoutBankAccountCommandHandler(IUnitOfWork uow, INotificationService notifications)
    {
        _uow = uow;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ReviewPayoutBankAccountCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<BankAccount, int>();
        var account = await repo.GetByIdAsync(request.BankAccountId, ct)
            ?? throw new NotFoundException(nameof(BankAccount), request.BankAccountId);

        if (account.OwnerType != BankAccountOwnerType.Lounge)
            throw new DomainException(
                "Tài khoản của nghệ sĩ do chính nghệ sĩ xác nhận qua liên kết gửi email, không duyệt ở đây.");

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(account.OwnerId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), account.OwnerId);
        var owner = await _uow.Repository<User, int>().GetByIdAsync(lounge.OwnerId, ct)
            ?? throw new NotFoundException(nameof(User), lounge.OwnerId);

        if (request.Approve && owner.CitizenCardReviewStatus != KycReviewStatus.Approved)
            throw new DomainException(
                "Chủ phòng trà chưa được duyệt CCCD/CMND — cần duyệt danh tính trước để đối chiếu tên chủ tài khoản.");

        account.IsVerified = request.Approve;
        repo.Update(account);

        await _notifications.NotifyAsync(
            owner.Id,
            NotificationType.KycReviewResult,
            request.Approve ? "Tài khoản nhận tiền đã được xác minh" : "Tài khoản nhận tiền chưa được xác minh",
            request.Approve
                ? $"Tài khoản {account.BankName} của \"{lounge.Name}\" đã được xác minh. Các khoản quyết toán đang giữ sẽ " +
                  "được chuyển ở lần giải ngân kế tiếp."
                : $"Tài khoản {account.BankName} của \"{lounge.Name}\" chưa được chấp nhận. Lý do: {request.Note} " +
                  "Hãy cập nhật tài khoản rồi chờ xác minh lại.",
            referenceType: "bank_account",
            referenceId: account.Id.ToString(),
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
