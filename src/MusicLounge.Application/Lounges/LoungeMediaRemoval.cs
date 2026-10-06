using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges;

/// <summary>
/// MLACP-692. Cách gỡ một ảnh thư viện / một cảnh tour 360 khỏi phòng trà — MỘT bản, dùng chung cho chủ phòng trà tự xoá
/// (RemoveLoungeGalleryImage / RemoveVenueTourScene) và Admin từ chối nội dung bị AI gắn cờ (ReviewLoungeMedia). Trước đây
/// logic nằm thẳng trong hai handler xoá; viết lại lần nữa cho đường Admin là chép hai bản của cùng một quy tắc, và các bẫy
/// bên dưới (MLACP-506, MLACP-543) đều là lỗi đã trả giá — bản chép nào quên một bước thì lỗi đó quay lại.
/// </summary>
public static class LoungeMediaRemoval
{
    /// <summary>Gỡ ảnh thư viện. Chưa lưu — người gọi lưu trong transaction của mình.</summary>
    public static async Task RemoveGalleryImageAsync(
        IUnitOfWork uow, MusicLoungeEntity lounge, LoungeGalleryImage image, CancellationToken ct)
    {
        var imageRepo = uow.Repository<LoungeGalleryImage, Guid>();
        imageRepo.Remove(image);

        // MLACP-506. Chiều ngược của MLACP-33 ("ảnh gallery đầu tiên tự là ảnh đại diện"): xoá đúng ảnh đang làm đại diện
        // thì ảnh đại diện phải đi theo — trước đây nó vẫn trỏ vào ảnh vừa xoá (file đã mất thì thành ô ảnh vỡ trên thẻ
        // phòng trà), và không có đường nào để gỡ. Chuyển sang ảnh gallery kế tiếp theo thứ tự hiển thị; hết ảnh thì để
        // trống. Ảnh đại diện đặt riêng (PUT /image, không thuộc gallery) không bị đụng tới.
        if (lounge.PrimaryImageUrl == image.ImageUrl)
        {
            var conLai = (await imageRepo.FindAsync(g => g.LoungeId == lounge.Id && g.Id != image.Id, ct))
                .OrderBy(g => g.OrderIndex).ThenBy(g => g.Id)
                .FirstOrDefault();
            lounge.PrimaryImageUrl = conLai?.ImageUrl;
            uow.Repository<MusicLoungeEntity, Guid>().Update(lounge);
        }
    }

    /// <summary>Gỡ cảnh tour 360. CÓ lưu (hai lần, cùng transaction của người gọi) — xem MLACP-543 bên dưới.</summary>
    public static async Task RemoveTourSceneAsync(IUnitOfWork uow, VenueTourScene scene, CancellationToken ct)
    {
        // VenueTourHotspot.TargetSceneId is Restrict (not Cascade — two cascade paths into the
        // same table from the same row isn't allowed by SQL Server), so any hotspot elsewhere in
        // this tour that navigates TO this scene must be cleaned up explicitly first, or the
        // delete below would fail with an FK violation. Hotspots physically INSIDE this scene
        // don't need handling here — those cascade via the Scene FK automatically.
        var hotspotRepo = uow.Repository<VenueTourHotspot, Guid>();
        var incomingHotspots = await hotspotRepo.FindAsync(h => h.TargetSceneId == scene.Id, ct);
        foreach (var hotspot in incomingHotspots)
            hotspotRepo.Remove(hotspot);

        // Same reasoning as the hotspot cleanup above — VenueTourStitchAttempt.ResultSceneId is
        // NoAction (not Cascade/SetNull, to avoid a second cascade path from MusicLounge into that
        // table), so any attempt log pointing at this scene needs its reference cleared explicitly
        // before the scene is deleted. The log ROW itself stays (it's an audit trail), just its
        // link to a now-gone scene is nulled.
        var attemptRepo = uow.Repository<VenueTourStitchAttempt, Guid>();
        var referencingAttempts = await attemptRepo.FindAsync(a => a.ResultSceneId == scene.Id, ct);
        foreach (var attempt in referencingAttempts)
        {
            attempt.ResultSceneId = null;
            attemptRepo.Update(attempt);
        }

        // MLACP-543: LƯU phần gỡ liên kết TRƯỚC, rồi mới xoá scene. FindAsync đọc AsNoTracking, nên Update() gắn lại
        // attempt với ResultSceneId = null mà EF KHÔNG biết giá trị cũ là scene này → không xếp UPDATE trước DELETE.
        // Azure SQL 03/10/2026 chạy DELETE trước: "conflicted with the REFERENCE constraint
        // FK_venue_tour_stitch_attempts_venue_tour_scenes_ResultSceneId" → 409, mọi scene do ghép ảnh tạo ra không xoá
        // được. Hai lần lưu vẫn nằm trong MỘT transaction (TransactionBehavior bọc mọi ICommand) — lỗi ở bước nào cũng
        // hoàn tác cả hai.
        await uow.SaveChangesAsync(ct);

        uow.Repository<VenueTourScene, Guid>().Remove(scene);
        await uow.SaveChangesAsync(ct);
    }
}
