using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Performer : Common.AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public PerformerType Type { get; set; } = PerformerType.Solo;
    public int? CreatedByUserId { get; set; }

    // MLACP-364: noi gui lien ket de nghe si tu xac nhan (nghe si khong co tai khoan dang nhap).
    public string? ContactEmail { get; set; }
    // Lan dau nghe si dong y cho nen tang xu ly du lieu cua ho — qua lien ket, khong phai do phong tra khai.
    public DateTimeOffset? DataConsentAt { get; set; }

    public User? CreatedByUser { get; set; }
    public ICollection<PerformerGenre> Genres { get; set; } = [];
    public ICollection<Performance> Performances { get; set; } = [];
}
