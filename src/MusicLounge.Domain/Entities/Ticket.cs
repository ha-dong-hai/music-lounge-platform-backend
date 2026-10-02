using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Ticket : Common.BaseEntity<Guid>
{
    public Guid? BuyerId { get; set; }       // nullable — ON DELETE SET NULL (BVDLCN 2025)
    public Guid PriceId { get; set; }
    public Guid TierId { get; set; }
    public Guid ShowId { get; set; }
    public Guid? PaymentId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    public string? QrCode { get; set; }     // generated on Confirmed
    public PurchaseChannel PurchaseChannel { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? PendingTransferToUserId { get; set; }          // chuyển nhượng vé: người nhận đang chờ accept
    public DateTimeOffset? PendingTransferInitiatedAt { get; set; }

    public User? Buyer { get; set; }
    public User? PendingTransferToUser { get; set; }
    public TicketPrice Price { get; set; } = null!;
    public TicketTier Tier { get; set; } = null!;
    public LoungeShow Show { get; set; } = null!;
    public Payment? Payment { get; set; }
    public PhysicalTicketDetail? PhysicalDetail { get; set; }
    public LivestreamTicketDetail? LivestreamDetail { get; set; }
}
