using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.Commands.CheckInLivestreamViewer;

// MLACP-140: tuong duong CheckInTicketCommandHandler nhung cho ve Livestream — khong co buoc quet
// QR nao de goi lenh nay tu con nguoi, nen no duoc kich hoat tu dong (enqueue tu
// GetLivestreamDetailQueryHandler dung luc xac minh chu ve that su nhan duoc HlsUrl phat). Idempotent:
// chi doi Ticket dang Confirmed, goi lai nhieu lan (moi lan tai lai trang chi tiet) khong lam gi them.
public sealed class CheckInLivestreamViewerJob
{
    private readonly IUnitOfWork _uow;

    public CheckInLivestreamViewerJob(IUnitOfWork uow) => _uow = uow;

    public async Task ExecuteAsync(int userId, int showId)
    {
        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(t =>
            t.ShowId == showId
            && t.BuyerId == userId
            && t.Status == TicketStatus.Confirmed
            && t.Tier.AccessType == AccessType.Livestream);

        if (tickets.Count == 0) return;

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        foreach (var ticket in tickets)
        {
            ticket.Status = TicketStatus.Used;
            ticketRepo.Update(ticket);
        }

        await _uow.SaveChangesAsync();
    }
}
