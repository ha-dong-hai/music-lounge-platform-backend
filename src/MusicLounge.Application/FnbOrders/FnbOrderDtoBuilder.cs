using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.FnbOrders.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.FnbOrders;

/// <summary>
/// MLACP-357 — dựng <see cref="FnbOrderDto"/> cho một danh sách đơn, dùng chung cho màn hàng đợi của
/// nhân viên (<c>GetFnbOrders</c>) và danh sách "đơn của tôi" của khán giả (<c>GetMyFnbOrders</c>).
///
/// <para>Tách ra thay vì chép vì hai màn phải trả lời <b>cùng một câu hỏi theo cùng một cách</b> — đặc
/// biệt <c>IsPaid</c>, vốn vừa đổi nghĩa ở MLACP-349 (hỏi bảng payments, không hỏi bước của bếp). Một
/// quy tắc có hai bản sao thì sớm muộn cũng lệch, và lúc đó khách thấy "chưa trả" trong khi nhân viên
/// thấy "đã trả".</para>
/// </summary>
internal static class FnbOrderDtoBuilder
{
    public static async Task<List<FnbOrderDto>> BuildAsync(
        IUnitOfWork uow, IReadOnlyList<FnbOrder> orders, CancellationToken ct)
    {
        if (orders.Count == 0) return [];

        var orderIds = orders.Select(o => o.Id).ToList();
        var items = await uow.Repository<OrderItem, int>().FindAsync(i => orderIds.Contains(i.FnbOrderId), ct);
        var menuItemIds = items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await uow.Repository<FnbMenuItem, int>().FindAsync(m => menuItemIds.Contains(m.Id), ct);
        var menuItemsById = menuItems.ToDictionary(m => m.Id);
        var itemsByOrder = items.ToLookup(i => i.FnbOrderId);

        var paidOrderIds = await FnbOrderPayments.ConfirmedOrderIdsAsync(uow, orderIds, ct);
        var liveUntil = await FnbOrderPayments.LiveOnlinePaymentDeadlinesAsync(
            uow, orderIds, DateTimeOffset.UtcNow, ct);

        return orders.Select(o => new FnbOrderDto(
            o.Id, o.LoungeId, o.ShowId, o.AudienceUserId, o.StaffId, o.TableNote,
            o.Status.ToString(), o.PaymentMethod.ToString(), o.TotalAmount, o.Note, o.CreatedAt,
            itemsByOrder[o.Id].Select(i => new OrderItemDto(
                i.Id, i.MenuItemId,
                menuItemsById.TryGetValue(i.MenuItemId, out var mi) ? mi.Name : "(deleted)",
                i.Quantity, i.UnitPrice, i.Cancelled, i.Note))
            .ToList(),
            FnbOrderPayments.IsPaid(o, paidOrderIds.Contains(o.Id)),
            liveUntil.TryGetValue(o.Id, out var until) ? until : null)
        ).ToList();
    }
}
