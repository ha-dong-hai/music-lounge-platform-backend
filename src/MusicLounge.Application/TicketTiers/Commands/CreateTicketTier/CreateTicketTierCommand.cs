using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;

public sealed record TicketPriceInput(
    string Name,
    decimal Price,
    int? Quota,
    string PurchaseChannel,
    DateTimeOffset SaleStart,
    /// <summary>Bỏ trống = bán tới khi buổi diễn kết thúc (BR-31).</summary>
    DateTimeOffset? SaleEnd);

public sealed record CreateTicketTierCommand(
    int ShowId,
    string Name,
    string? Description,
    string AccessType,
    int? ZoneId,
    int? TotalCapacity,
    IReadOnlyList<TicketPriceInput> Prices
) : ICommand<int>;
