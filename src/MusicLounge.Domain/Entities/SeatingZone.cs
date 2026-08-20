namespace MusicLounge.Domain.Entities;

// D1: Venue-level zones — reused across multiple shows
public sealed class SeatingZone : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public string Name { get; set; } = string.Empty;    // VIP / Standard / Bar Area
    public int Capacity { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Vi tri tren ban do 2D (% 0-100 tren canvas so do phong tra) — null = chua ve, dung auto-layout
    public string? LayoutColor { get; set; }
    public double? Layout2DX { get; set; }
    public double? Layout2DY { get; set; }
    public double? Layout2DWidth { get; set; }
    public double? Layout2DHeight { get; set; }
    public double? Layout2DRotationDeg { get; set; }

    // Vi tri marker trong khong gian 3D (world-space unit cua chinh scene) — null = chua gan
    public double? Layout3DX { get; set; }
    public double? Layout3DY { get; set; }
    public double? Layout3DZ { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
}
