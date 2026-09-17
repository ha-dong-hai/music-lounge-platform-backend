using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

// Every panorama-stitch attempt for a venue's tour — a log, not just the current scenes. Mirrors
// AiPosterGeneration's role: (a) tour_stitch_max_attempts_per_lounge (system_config) is enforced
// against every attempt here (success + failure, except FailedBySystem — MLACP-435) as an anti-abuse rate limit — unlike the AI
// poster vendor calls, a stitch runs on OUR OWN server's CPU, so an unbounded retry loop is a
// direct cost/DoS vector, not just a wasted vendor bill; and (b) it's an auditable record of why a
// given stitch failed, since the resulting VenueTourScene (on success) doesn't carry that context.
public sealed class VenueTourStitchAttempt : Common.BaseEntity<int>
{
    public int LoungeId { get; set; }
    public VenueTourStitchStatus Status { get; set; }
    public int? ResultSceneId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // MLACP-435: that bai do phia he thong (dich vu ghep anh khong phan hoi, sai cau hinh, loi 5xx, job bi gian doan)
    // — CPU chua xu ly bo anh nao, nen KHONG tinh vao tour_stitch_max_attempts_per_lounge. Truoc day moi luot deu bi
    // tinh: dich vu ngung vai lan la phong tra bi khoa tinh nang vinh vien. Loi do chinh bo anh va ghep qua thoi gian
    // (CPU da chay) van tinh. Mac dinh false chu khong phai "CountsTowardLimit = true": cot bool co gia tri mac dinh
    // true trong EF Core khong phan biet duoc "dat false" voi "chua dat".
    public bool FailedBySystem { get; set; }

    public MusicLounge Lounge { get; set; } = null!;
    public VenueTourScene? ResultScene { get; set; }
}
