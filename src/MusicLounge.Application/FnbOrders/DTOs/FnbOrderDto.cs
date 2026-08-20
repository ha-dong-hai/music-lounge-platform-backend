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
    IReadOnlyList<OrderItemDto> Items);
