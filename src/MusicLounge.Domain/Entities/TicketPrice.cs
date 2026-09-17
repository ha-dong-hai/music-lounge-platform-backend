using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class TicketPrice : Common.BaseEntity<int>
{
    public int TierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int? Quota { get; set; }

    // MLACP-441: cot "Sold" da bi go han (khong con trong entity lan DB). No chua bao gio duoc ghi — do that tren Azure
    // 17/09: ca 5 dot ban deu Sold = 0 trong khi ve that la 3/1/2/2/4 — nen chi la bay: ai doc no de tinh ve con lai se
    // cho ban vuot. So ve da ban/dang giu luon tinh truc tiep tu Tickets + TicketHolds qua
    // ITicketRepository.GetReservedQuantitiesByPriceIdsAsync, dung chung cho ca duong ghi (dat ve) va duong doc (hien thi).
    public bool IsActive { get; set; } = true;
    public DateTimeOffset SaleStart { get; set; }
    // BR-31: bo trong nghia la ban toi khi buoi dien ket thuc. Ve ban tai quay khong the chot
    // truoc mot moc dong cung — khan gia den muon van mua ve vao duoc. Muon dung ban som hon thi
    // dat LoungeShow.TicketSaleClosesAt, cho danh rieng cho viec do.
    public DateTimeOffset? SaleEnd { get; set; }
    public PurchaseChannel PurchaseChannel { get; set; }

    public TicketTier Tier { get; set; } = null!;
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<TicketHold> Holds { get; set; } = [];
}
