using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Settlements;

/// <summary>
/// MLACP-671. Điểm uy tín và hạng chi trả của phòng trà (D3) — MỘT chỗ tính cho mọi nơi dùng.
///
/// <para><b>Cơ chế:</b> điểm = trung bình sao các đánh giá còn hiệu lực của mọi buổi diễn của phòng trà (thang 0–5). Hạng
/// quyết định phần tiền vé chuyển TRƯỚC cho phòng trà ở đợt 1 (48 giờ sau buổi diễn); phần còn lại giữ tới đợt 2 để trừ
/// hoàn tiền/khiếu nại: Mới 50% · Tiêu chuẩn 70% (điểm ≥ 3.5) · Cao cấp 80% (điểm ≥ 4.2 VÀ ≥ 10 buổi đã diễn). Ngưỡng và
/// tỉ lệ nằm ở system_config. Đây KHÔNG phải tỉ lệ hoàn tiền cho khách — khách luôn được hoàn theo chính sách của buổi
/// diễn, và 100% khi phòng trà có lỗi (TicketRefundPolicy).</para>
///
/// <para><b>Vì sao tính trực tiếp chứ không đọc cột MusicLounge.ReputationScore:</b> cột đó chỉ được ghi lại khi có vé bán
/// (ScheduleSettlementHandler), nên đánh giá mới hoặc đánh giá bị gỡ không làm nó đổi — bảng xếp hạng của Admin và màn của
/// chủ phòng trà sẽ hiện điểm cũ. Trước task này hạng chỉ tồn tại bên trong lúc xếp lịch quyết toán: chủ phòng trà không
/// hề biết mình hạng nào, vì sao nhận trước 50% chứ không phải 70%.</para>
///
/// <para>Trần: mỗi lần gọi đọc mọi đánh giá của các phòng trà được hỏi — ổn ở quy mô hiện tại (vài phòng trà, vài trăm
/// đánh giá). Khi lớn: lưu tổng/số đánh giá theo phòng trà và cập nhật ở lệnh đánh giá.</para>
/// </summary>
public static class VenueReputation
{
    public const string TierNew = "New";
    public const string TierStandard = "Standard";
    public const string TierPremium = "Premium";

    // Mặc định trùng giá trị seed (D3); dòng trong system_config luôn thắng.
    public const decimal DefaultNewPreRate = 0.50m;
    public const decimal DefaultStandardPreRate = 0.70m;
    public const decimal DefaultPremiumPreRate = 0.80m;
    public const decimal DefaultStandardMinScore = 3.5m;
    public const decimal DefaultPremiumMinScore = 4.2m;
    public const int DefaultPremiumMinShows = 10;

    public static async Task<IReadOnlyDictionary<Guid, VenueStanding>> ComputeAsync(
        IUnitOfWork uow, ISystemConfigService config, IReadOnlyCollection<Guid> loungeIds, CancellationToken ct = default)
    {
        if (loungeIds.Count == 0) return new Dictionary<Guid, VenueStanding>();
        var ids = loungeIds.Distinct().ToList();

        var newRate = await config.GetDecimalAsync(ConfigKeys.SettlementTierNewPreRate, DefaultNewPreRate, ct);
        var standardRate = await config.GetDecimalAsync(ConfigKeys.SettlementTierStandardPreRate, DefaultStandardPreRate, ct);
        var premiumRate = await config.GetDecimalAsync(ConfigKeys.SettlementTierPremiumPreRate, DefaultPremiumPreRate, ct);
        var standardMinScore = await config.GetDecimalAsync(ConfigKeys.SettlementTierStandardMinScore, DefaultStandardMinScore, ct);
        var premiumMinScore = await config.GetDecimalAsync(ConfigKeys.SettlementTierPremiumMinScore, DefaultPremiumMinScore, ct);
        var premiumMinShows = await config.GetIntAsync(ConfigKeys.SettlementTierPremiumMinShows, DefaultPremiumMinShows, ct);

        var shows = await uow.Repository<LoungeShow, Guid>().FindAsync(s => ids.Contains(s.LoungeId), ct);
        var loungeOfShow = shows.ToDictionary(s => s.Id, s => s.LoungeId);
        var showIds = loungeOfShow.Keys.ToList();
        var ratings = await uow.Repository<LoungeShowRating, Guid>().FindAsync(
            r => !r.IsRemoved && showIds.Contains(r.LoungeShowId), ct);

        var result = new Dictionary<Guid, VenueStanding>();
        foreach (var loungeId in ids)
        {
            var scores = ratings.Where(r => loungeOfShow[r.LoungeShowId] == loungeId).Select(r => r.Score).ToList();
            var score = scores.Count > 0 ? (decimal)scores.Average() : 0m;
            var completed = shows.Count(s => s.LoungeId == loungeId && s.Status == LoungeShowStatus.Ended);

            var (tier, rate) = score >= premiumMinScore && completed >= premiumMinShows ? (TierPremium, premiumRate)
                : score >= standardMinScore ? (TierStandard, standardRate)
                : (TierNew, newRate);

            result[loungeId] = new VenueStanding(loungeId, Math.Round(score, 2), scores.Count, completed, tier, rate,
                new VenueTierRules(newRate, standardRate, premiumRate, standardMinScore, premiumMinScore, premiumMinShows));
        }
        return result;
    }
}

/// <param name="Score">Trung bình sao còn hiệu lực, làm tròn 2 chữ số; 0 khi chưa có đánh giá.</param>
/// <param name="PreRate">Phần tiền vé chuyển ở đợt 1 theo hạng hiện tại (0.50 = 50%).</param>
/// <param name="Rules">Ngưỡng đang áp — để màn hình nói được "cần thêm gì để lên hạng" mà không viết cứng con số.</param>
public sealed record VenueStanding(
    Guid LoungeId, decimal Score, int RatingCount, int CompletedShows, string Tier, decimal PreRate, VenueTierRules Rules);

public sealed record VenueTierRules(
    decimal NewPreRate, decimal StandardPreRate, decimal PremiumPreRate,
    decimal StandardMinScore, decimal PremiumMinScore, int PremiumMinShows);
