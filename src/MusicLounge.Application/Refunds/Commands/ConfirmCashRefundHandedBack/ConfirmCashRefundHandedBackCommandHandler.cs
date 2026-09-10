using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Refunds.Commands.ConfirmCashRefundHandedBack;

/// <summary>
/// MLACP-345. MLACP-337 xu ly dung viec duyet hoan cho ve ban tai quay: nen tang chua bao gio giu khoan
/// tien mat do, nen he thong bao phong tra rang chinh ho phai tra cho khach. Nhung sau loi nhac do
/// khong ai theo doi tiep — RefundOwedByVenue chi duoc ghi, va job canh bao SLA chi quet yeu cau
/// Pending. Voi khach, "yeu cau hoan tien da duoc duyet" co the chang dan toi dong nao.
///
/// <para>Ai xac nhan: nhan vien hoac chu cua DUNG phong tra do — nhan vien quay la nguoi thuc su tra
/// tien mat, cung nhu ho la nguoi ban ve tai quay.</para>
/// </summary>
internal sealed class ConfirmCashRefundHandedBackCommandHandler
    : IRequestHandler<ConfirmCashRefundHandedBackCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public ConfirmCashRefundHandedBackCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ConfirmCashRefundHandedBackCommand request, CancellationToken ct)
    {
        var refundRepo = _uow.Repository<RefundRequest, int>();
        var refund = await refundRepo.GetByIdAsync(request.RefundRequestId, ct)
            ?? throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        var payment = await _uow.Repository<Payment, int>().GetByIdAsync(refund.PaymentId, ct)
            ?? throw new NotFoundException(nameof(Payment), refund.PaymentId);

        // Lan tu giao dich ve phong tra qua ve — mot Payment chi thuoc mot buoi dien.
        var ticket = (await _uow.Repository<Ticket, Guid>()
                .FindAsync(t => t.PaymentId == payment.Id, ct))
            .FirstOrDefault()
            ?? throw new DomainException("Khong xac dinh duoc ve cua giao dich nay.");
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(ticket.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), ticket.ShowId);
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        // Phan quyen truoc moi dieu kien nghiep vu: nguoi ngoai khong duoc biet ca trang thai cua
        // yeu cau cua phong tra khac.
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Ban khong phai nhan vien hay chu cua phong tra nay.");

        if (payment.Method != PaymentMethod.Cash)
            throw new DomainException(
                "Chi xac nhan tra tien mat cho ve ban tai quay. Ve mua online duoc hoan qua cong thanh " +
                "toan, phong tra khong can lam gi.");

        if (refund.Status != RefundRequestStatus.Approved)
            throw new DomainException("Yeu cau hoan tien nay chua duoc duyet.");

        if (refund.CashHandedBackAt is not null)
            throw new ConflictException("Da xac nhan tra tien cho yeu cau nay tu truoc.");

        refund.CashHandedBackAt = DateTimeOffset.UtcNow;
        refundRepo.Update(refund);

        // Noi dung su that va chi duong neu khong dung: nen tang khong chung kien viec giao tien, nen
        // khong khang dinh thay phong tra — chi chuyen loi xac nhan cua ho, kem loi thoat cho khach.
        if (refund.RequestedBy is int buyerId)
            await _notifications.NotifyAsync(
                buyerId,
                NotificationType.RefundUpdate,
                "Phong tra xac nhan da hoan tien mat",
                $"Phong tra xac nhan da tra lai {refund.AmountApproved ?? refund.AmountRequested:N0}d " +
                "tien mat cho ban. Neu ban chua nhan duoc, hay gui khieu nai de chung toi xu ly.",
                referenceType: "refund",
                referenceId: refund.Id.ToString(),
                ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
