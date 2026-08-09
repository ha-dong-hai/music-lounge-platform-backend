using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Utils;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.RescheduleLoungeShow;

// D13: doi lich sau khi show da Published/Ongoing (co the da ban ve) - khac UpdateLoungeShowCommand
// (chi cho sua khi con Draft).
internal sealed class RescheduleLoungeShowCommandHandler : IRequestHandler<RescheduleLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;

    public RescheduleLoungeShowCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(RescheduleLoungeShowCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền đổi lịch event này.");

        // Ongoing bi loai khoi day: show da ActualStart (dang dien ra that) — doi ScheduledStart
        // sang tuong lai luc nay tao trang thai mau thuan "dang dien ra nhung lich bat dau nam o
        // tuong lai", anh huong sort "StartingSoon"/danh sach sap dien.
        if (show.Status is not LoungeShowStatus.Published)
            throw new DomainException("Chỉ có thể đổi lịch event đã Published (chưa bắt đầu diễn ra).");

        // D18: doi lich la doi ngay dien da duoc "van ban chap thuan" ban dau bao ve — ap dung lai
        // dung nguyen tac 7 ngay lam viec (NĐ 144/2020 Điều 10) cho ngay dien MOI, tranh loophole
        // "publish dung han roi doi lich gap ngay hom sau".
        var businessDaysUntilNewStart = BusinessDayCalculator.CountBusinessDaysBetween(
            DateTimeOffset.UtcNow, request.NewScheduledStart);
        if (businessDaysUntilNewStart < 7)
            throw new DomainException(
                "Theo NĐ 144/2020 Điều 10, ngày diễn mới phải cách thời điểm đổi lịch tối thiểu " +
                $"7 ngày làm việc. Hiện chỉ còn {businessDaysUntilNewStart} ngày làm việc.");

        var oldStart = show.ScheduledStart;
        var delta = request.NewScheduledStart - oldStart;
        show.ScheduledStart = request.NewScheduledStart;
        if (show.ScheduledEnd.HasValue)
            show.ScheduledEnd = show.ScheduledEnd.Value + delta;

        // D13: "mo cua so hoan tien theo refund_percentage" - dam bao ticket holder thuc su dung
        // duoc CancelTicket sau khi doi lich (deadline se duoc tinh lai theo ScheduledStart moi).
        show.CancellationAllowed = true;
        showRepo.Update(show);

        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed && t.BuyerId != null, ct);
        var buyerIds = tickets.Select(t => t.BuyerId!.Value).Distinct();

        // Was NotificationType.EventReminder — collided with EventReminderJob's own dedup key
        // (Type=EventReminder + ReferenceType="show" + ReferenceId=show.Id, per buyer). The job
        // saw this reschedule notice, concluded the buyer was "already reminded" for the show, and
        // silently never sent the real pre-show reminder for the new date.
        foreach (var buyerId in buyerIds)
            await _notifications.NotifyAsync(
                buyerId,
                NotificationType.EventRescheduled,
                "Lịch diễn đã thay đổi",
                $"\"{show.Name}\" đã đổi lịch từ {oldStart:HH:mm dd/MM/yyyy} sang " +
                $"{show.ScheduledStart:HH:mm dd/MM/yyyy}. Bạn có thể hủy vé để được hoàn tiền nếu không " +
                "thể tham dự vào thời gian mới.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
