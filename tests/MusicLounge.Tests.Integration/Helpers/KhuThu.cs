using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// MLACP-589: hạng vé vào cửa phải gắn một khu ghế, và mỗi khu chỉ thuộc một hạng vé của buổi diễn. Các test tạo hạng vé
/// qua API (không nhằm kiểm luật này) lấy một khu MỚI của đúng phòng trà tổ chức buổi diễn ở đây — mỗi lần gọi một khu
/// riêng, nên test nào tạo nhiều hạng vé cho cùng buổi cũng không vướng luật "mỗi khu một hạng vé".
/// </summary>
internal static class KhuThu
{
    /// <summary>Khu mới cho buổi diễn; trả null với hạng vé xem trực tuyến (không có chỗ ngồi).</summary>
    public static Guid? ChoBuoi(ApiFactory factory, Guid showId, string accessType = "Physical")
    {
        if (!string.Equals(accessType, "Physical", StringComparison.OrdinalIgnoreCase)) return null;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loungeId = db.LoungeShows.Where(s => s.Id == showId).Select(s => s.LoungeId).Single();
        var zone = new SeatingZone
        {
            Id = OrderedGuid.New(), LoungeId = loungeId, Name = $"Khu {Guid.NewGuid():N}"[..16], Capacity = 1000,
            Layout2DX = 10, Layout2DY = 10, Layout2DWidth = 20, Layout2DHeight = 18
        };
        db.SeatingZones.Add(zone);
        db.SaveChanges();
        return zone.Id;
    }
}
