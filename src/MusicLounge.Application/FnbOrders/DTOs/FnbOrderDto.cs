namespace MusicLounge.Application.FnbOrders.DTOs;

public sealed record OrderItemDto(
    int Id,
    int MenuItemId,
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    bool Cancelled,
    string? Note);

public sealed record FnbOrderDto(
    int Id,
    int LoungeId,
    int? ShowId,
    int? AudienceUserId,
    int? StaffId,
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
    DateTimeOffset? OnlinePaymentLiveUntil);
