using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class FnbOrder : Common.AuditableEntity<Guid>
{
    public Guid LoungeId { get; set; }
    public Guid? ShowId { get; set; }            // null = outside show hours
    public Guid? AudienceUserId { get; set; }    // BVDLCN: SET NULL on delete
    public Guid? StaffId { get; set; }           // staff who placed on behalf of guest
    public Guid? ZoneId { get; set; }
    public string? TableNote { get; set; }      // "Bàn A3"
    public FnbOrderStatus Status { get; set; } = FnbOrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; } = 0m;
    public string? Note { get; set; }

    // MLACP-631: dấu vết huỷ đơn — ai huỷ, lúc nào, vì sao. Phần mềm F&B thực tế bắt nhập lý do khi huỷ món đã báo
    // bếp/bar (KiotViet, CUKCUK) và báo cáo huỷ theo người huỷ (Toast). CancelledBy null + có lý do = hệ thống huỷ
    // hàng loạt (buổi diễn huỷ, chuyển online, phòng trà bị khoá — FnbOrderCancellation).
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    // MLACP-631: nhân viên đã cầm tiền mặt của đơn này. Trước đây bản ghi thu tiền mặt (Payment) không lưu người thu và
    // UpdatedBy của đơn bị ghi đè ở mỗi bước — chủ phòng trà không đối chiếu được tiền mặt cuối ca theo từng người.
    public Guid? CashCollectedBy { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public LoungeShow? Show { get; set; }
    public User? AudienceUser { get; set; }
    public User? Staff { get; set; }
    public SeatingZone? Zone { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
