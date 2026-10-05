namespace MusicLounge.Application.FnbOrders.DTOs;

public sealed record OrderItemDto(
    Guid Id,
    Guid MenuItemId,
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    bool Cancelled,
    string? Note);

public sealed record FnbOrderDto(
    Guid Id,
    Guid LoungeId,
    Guid? ShowId,
    Guid? AudienceUserId,
    Guid? StaffId,
    string? TableNote,
    string Status,
    string PaymentMethod,
    decimal TotalAmount,
    string? Note,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderItemDto> Items,
    // MLACP-349: "da tra tien" tach khoi buoc cua bep — don tra truoc qua VNPay van o Pending/Preparing
    // cho toi khi phuc vu xong. Status khong con tra loi duoc cau "khach da tra chua".
    bool IsPaid,
    // MLACP-349: khach dang co mot link VNPay con tra duoc cho toi thoi diem nay — trong luc do he thong
    // tu choi thu tien mat va huy don, va man hinh nhan vien can thay vi sao.
    DateTimeOffset? OnlinePaymentLiveUntil,
    // MLACP-630: khu khách ngồi (theo vé của họ hoặc do client gửi) — bảng đơn của nhân viên in cùng TableNote để biết
    // "Bàn góc" là góc của khu nào. Null: đơn không gắn khu (khách không có vé đêm đó, hoặc đơn tạo trước MLACP-630).
    Guid? ZoneId = null,
    string? ZoneName = null,
    // MLACP-631: dấu vết huỷ. CancelledByName chỉ trả cho phía phòng trà; khách chỉ thấy lý do và thời điểm.
    DateTimeOffset? CancelledAt = null,
    string? CancelReason = null,
    string? CancelledByName = null,
    // MLACP-631: nhân viên đã cầm tiền mặt — chỉ trả cho phía phòng trà, để đối chiếu tiền mặt cuối ca.
    string? CashCollectedByName = null);
