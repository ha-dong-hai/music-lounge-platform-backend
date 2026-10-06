using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.LoungeShows;

/// <summary>
/// MLACP-676. Hai cách nói về cùng một lý do huỷ:
/// <list type="bullet">
/// <item><see cref="Label"/> — tên loại lý do, cho chủ phòng trà chọn và cho Admin xét.</item>
/// <item><see cref="ForBuyers"/> — cụm ghép sau "đã bị huỷ vì …" trong thông báo gửi khán giả. Trung tính: mô tả chi tiết
/// và bằng chứng của phòng trà là chuyện giữa phòng trà với nền tảng (cùng tinh thần với
/// <see cref="ShowCancellation.VenueStoppedTrading"/>). "Bán được ít vé" và "Lý do khác" nói với khán giả là quyết định của
/// phòng trà — đúng sự thật, không đổ cho nghệ sĩ hay hoàn cảnh.</item>
/// </list>
/// </summary>
public static class ShowCancellationReasons
{
    public static SongNgu Label(ShowCancellationReason r) => r switch
    {
        ShowCancellationReason.ForceMajeure => new("Bất khả kháng (thiên tai, dịch bệnh, sự cố an ninh)", "Force majeure (natural disaster, epidemic, security incident)"),
        ShowCancellationReason.PerformerUnavailable => new("Nghệ sĩ không thể biểu diễn", "Performer unable to perform"),
        ShowCancellationReason.AuthorityRequest => new("Cơ quan chức năng yêu cầu", "Required by the authorities"),
        ShowCancellationReason.VenueIncident => new("Sự cố tại phòng trà", "Incident at the venue"),
        ShowCancellationReason.LowSales => new("Bán được ít vé", "Low ticket sales"),
        _ => new("Lý do khác", "Other reason"),
    };

    public static SongNgu ForBuyers(ShowCancellationReason r) => r switch
    {
        ShowCancellationReason.ForceMajeure => new("sự kiện bất khả kháng", "circumstances beyond the venue's control"),
        ShowCancellationReason.PerformerUnavailable => new("nghệ sĩ không thể biểu diễn", "the performer is unable to perform"),
        ShowCancellationReason.AuthorityRequest => new("yêu cầu của cơ quan chức năng", "a request from the authorities"),
        ShowCancellationReason.VenueIncident => new("sự cố tại phòng trà", "an incident at the venue"),
        _ => new("quyết định của phòng trà", "the music lounge's decision"),
    };

    /// <summary>Độ dài tối thiểu của mô tả — đủ để Admin có gì mà xét, không phải "huỷ", "bận".</summary>
    public const int MinDetailLength = 20;
}
