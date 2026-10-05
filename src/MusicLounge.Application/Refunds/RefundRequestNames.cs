using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Refunds;

/// <summary>
/// MLACP-672. Gắn TÊN người yêu cầu và TÊN buổi diễn vào yêu cầu hoàn tiền. Trước đây DTO chỉ có mã yêu cầu, mã thanh
/// toán và mã người gửi, nên trang Hoàn tiền của Admin in "Yêu cầu #0CAD7D02 · thanh toán #0A35AD2C" và khán giả thấy
/// "Yêu cầu hoàn tiền #…" — không ai biết đó là vé buổi nào của ai. Dùng chung cho hàng chờ Admin và "của tôi".
/// Mỗi loại một truy vấn cho cả trang.
/// </summary>
internal static class RefundRequestNames
{
    public static async Task<List<RefundRequestDto>> EnrichAsync(IUnitOfWork uow, List<RefundRequestDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return items;
        var userIds = items.Where(i => i.RequestedBy.HasValue).Select(i => i.RequestedBy!.Value).Distinct().ToList();
        var users = (await uow.Repository<User, Guid>().FindAsync(u => userIds.Contains(u.Id), ct)).ToDictionary(u => u.Id, u => u.FullName);

        var paymentIds = items.Select(i => i.PaymentId).Distinct().ToList();
        var tickets = await uow.Repository<Ticket, Guid>().FindAsync(t => t.PaymentId != null && paymentIds.Contains(t.PaymentId.Value), ct);
        var showIds = tickets.Select(t => t.ShowId).Distinct().ToList();
        var shows = (await uow.Repository<LoungeShow, Guid>().FindAsync(s => showIds.Contains(s.Id), ct)).ToDictionary(s => s.Id, s => s.Name);
        var showOfPayment = tickets.GroupBy(t => t.PaymentId!.Value)
            .ToDictionary(g => g.Key, g => (Show: shows.GetValueOrDefault(g.First().ShowId), Count: g.Count()));

        return items.Select(i => i with
        {
            RequesterName = i.RequestedBy is Guid uid ? users.GetValueOrDefault(uid) : null,
            ShowName = showOfPayment.TryGetValue(i.PaymentId, out var s) ? s.Show : null,
            TicketCount = showOfPayment.TryGetValue(i.PaymentId, out var c) ? c.Count : 0,
        }).ToList();
    }
}
