using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets;

/// <summary>
/// MLACP-383. Vé vào cửa (<see cref="AccessType.Physical"/>) chỉ có nghĩa khi buổi diễn còn chỗ ngồi thật —
/// <see cref="LoungeShowFormat.Offline"/> hoặc <see cref="LoungeShowFormat.Hybrid"/>.
///
/// <para>Trước task này <c>ChangeLoungeShowFormat</c> (Offline → Online) huỷ và hoàn 100% vé vào cửa đã bán (D13),
/// nhưng không điểm bán vé nào hỏi lại hình thức buổi diễn và tier vào cửa vẫn mở. Một quy tắc, một chỗ — như
/// <c>Common.VenueLifecycle</c> — để điểm bán thứ tư sau này không tự trả lời lại theo cách của nó.</para>
/// </summary>
public static class PhysicalAccess
{
    public static bool IsOffered(LoungeShow show, AccessType accessType)
        => accessType != AccessType.Physical || show.Format != LoungeShowFormat.Online;

    public const string NoLongerOffered =
        "Buổi diễn này đã chuyển sang hình thức online — không còn bán vé vào cửa.";
}
