using System.Globalization;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Decides whether a proposed system_config value is acceptable before it is written.
///
/// Type-checking alone would not be enough here. Most of these keys are money: a commission rate
/// typed as a valid decimal can still be 7.5, and nothing downstream would reject it — the ledger
/// would simply start producing figures nobody can pay. The rules below are the ones that protect
/// invariants other code already depends on, not general tidiness.
///
/// The cross-key rule is the important one. PaymentFeeCalculator derives the owner's share as the
/// REMAINDER (gross − platformFee − tax), precisely so the three parts always sum to gross and the
/// double-entry journal balances. If commission + tax ever reaches 1, that remainder goes negative,
/// and a negative transfer is something the ledger cannot represent at all — DonationPayoutSplit
/// already documents that same limitation from the other direction. Catching it on write is the
/// only place it can be caught cheaply; the alternative is discovering it at the next ticket sale.
/// </summary>
public static class SystemConfigValidation
{
    /// <summary>Keys that are a proportion of money and must stay within 0..1.</summary>
    private static readonly HashSet<string> RateKeys =
    [
        ConfigKeys.PlatformCommissionRate,
        ConfigKeys.TaxRate,
        ConfigKeys.PersonalIncomeTaxRate,
        ConfigKeys.DonationPerformerShareRate,
        "gateway_fee_rate",
        ConfigKeys.SettlementTierNewPreRate,
        ConfigKeys.SettlementTierStandardPreRate,
        ConfigKeys.SettlementTierPremiumPreRate,
        ConfigKeys.SettlementCompletionThresholdPct,
    ];

    /// <summary>
    /// True when the key is a proportion of money. Surfaced to the Admin UI so a rate can be shown
    /// as a percentage and flagged before editing — "0.05" on its own gives no hint that it is the
    /// platform's entire commission.
    /// </summary>
    public static bool IsMoneyRate(string key) => RateKeys.Contains(key);

    /// <summary>
    /// Validates a proposed value on its own terms. <paramref name="otherRates"/> supplies the
    /// current values of the keys this one has to be consistent with, so a cross-key rule can be
    /// checked without this class reaching into the database itself.
    /// </summary>
    /// <returns>An error message, or null when the value is acceptable.</returns>
    public static string? Validate(
        string key,
        string proposedValue,
        ConfigDataType dataType,
        IReadOnlyDictionary<string, decimal> otherRates)
    {
        if (string.IsNullOrWhiteSpace(proposedValue))
            return "Giá trị không được để trống.";

        switch (dataType)
        {
            case ConfigDataType.Decimal:
                if (!decimal.TryParse(proposedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
                    return $"\"{proposedValue}\" không phải số thập phân hợp lệ (dùng dấu chấm, ví dụ 0.05).";
                if (dec < 0)
                    return "Giá trị không được âm.";
                if (RateKeys.Contains(key) && dec > 1)
                    return $"\"{key}\" là một tỉ lệ nên phải nằm trong khoảng 0 đến 1 " +
                           $"(0.05 nghĩa là 5%). Giá trị {dec} tương đương {dec:P0}.";
                return ValidateRateCombination(key, dec, otherRates)
                       ?? ValidateDonationSplit(key, dec, otherRates);

            case ConfigDataType.Integer:
                if (!int.TryParse(proposedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return $"\"{proposedValue}\" không phải số nguyên hợp lệ.";
                // Every integer key in this system is a count or a duration; zero or negative would
                // silently disable the thing it configures rather than tighten it.
                if (i <= 0)
                    return "Giá trị phải lớn hơn 0.";
                return null;

            case ConfigDataType.Boolean:
                return bool.TryParse(proposedValue, out _)
                    ? null
                    : $"\"{proposedValue}\" không phải giá trị true/false hợp lệ.";

            case ConfigDataType.String:
            case ConfigDataType.Json:
                if (proposedValue.Length > 500)
                    return "Giá trị không được dài quá 500 ký tự.";
                // MLACP-360: danh sách từ cấm phải đọc được ngay lúc ghi. Để lỗi tới lúc phát sóng thì
                // mọi lời nhắn đều bị giữ lại mà không ai biết vì sao.
                if (key == ConfigKeys.DonationMessageBlockedWords
                    && !Donations.DonationMessageFilter.TryParseList(proposedValue, out _))
                    return "Danh sách từ cấm phải là một mảng JSON các chuỗi, ví dụ [\"từ một\", \"cụm từ hai\"]. " +
                           "Muốn bỏ hết thì dùng [].";
                return null;

            default:
                return $"Kiểu dữ liệu \"{dataType}\" chưa được hỗ trợ.";
        }
    }

    /// <summary>
    /// Commission and tax are both taken off the same gross, and the owner receives what is left.
    /// Their sum reaching 1 leaves the owner nothing — or less than nothing — which is not a policy
    /// anyone would choose deliberately and which the ledger cannot record.
    ///
    /// Donations add a fourth claim on the same gross. The performer's share is a percentage of the
    /// ORIGINAL gross, and the owner forwards it out of what they received, so the owner keeps
    /// 1 − commission − VAT − TNCN − performerShare. Seeded today that is 2%. Turning on personal
    /// income tax at the 2% the decree specifies — which MLACP-289 documents as the intended value
    /// and MLACP-284 made reachable from an endpoint — lands it on exactly 0, and one more change
    /// after that makes it negative. At that point ConfirmDonationPaid throws and the performer can
    /// never be paid, while the donor's money was already taken at chặng 1.
    ///
    /// That failure surfaces days later, in a different flow, so it has to be caught on write. The
    /// point is not to forbid withholding personal income tax — it is legally required — but to
    /// force whoever enables it to say whose 2% it comes out of, instead of the system silently
    /// taking all of the venue's.
    /// </summary>
    private static string? ValidateRateCombination(
        string key, decimal proposed, IReadOnlyDictionary<string, decimal> otherRates)
    {
        // All three come off the same gross, and the owner receives what is left after all three.
        string[] deductedFromGross =
        [
            ConfigKeys.PlatformCommissionRate,
            ConfigKeys.TaxRate,
            ConfigKeys.PersonalIncomeTaxRate
        ];

        if (!deductedFromGross.Contains(key))
            return null;

        var counterparts = deductedFromGross
            .Where(k => k != key)
            .Select(k => (Key: k, Value: otherRates.TryGetValue(k, out var v) ? v : 0m))
            .ToArray();

        var combined = proposed + counterparts.Sum(c => c.Value);
        if (combined >= 1m)
        {
            var others = string.Join(", ", counterparts.Select(c => $"\"{c.Key}\" = {c.Value:P0}"));
            return $"Hoa hồng nền tảng cộng các khoản thuế khấu trừ sẽ là {combined:P0} — bằng hoặc " +
                   $"vượt 100% doanh thu, nghĩa là chủ phòng trà nhận về 0 đồng hoặc âm. Sổ cái kép " +
                   $"không biểu diễn được một khoản chuyển âm. Hiện {others}.";
        }

        return null;
    }

    /// <summary>
    /// Bốn khoản cùng cắt trên một gốc doanh thu donate, và chủ phòng trà nhận phần dư cuối cùng.
    /// Nếu tổng chạm 100% thì phần dư đó bằng 0 hoặc âm, và việc chi trả cho nghệ sĩ sẽ kẹt lại
    /// vĩnh viễn — sau khi tiền của người donate đã thu.
    /// </summary>
    private static string? ValidateDonationSplit(
        string key, decimal proposed, IReadOnlyDictionary<string, decimal> otherRates)
    {
        string[] claimsOnDonationGross =
        [
            ConfigKeys.PlatformCommissionRate,
            ConfigKeys.TaxRate,
            ConfigKeys.PersonalIncomeTaxRate,
            ConfigKeys.DonationPerformerShareRate
        ];

        if (!claimsOnDonationGross.Contains(key))
            return null;

        var others = claimsOnDonationGross
            .Where(k => k != key)
            .Select(k => (Key: k, Value: otherRates.TryGetValue(k, out var v) ? v : 0m))
            .ToArray();

        var combined = proposed + others.Sum(c => c.Value);
        if (combined < 1m)
            return null;

        // Nói rõ phải hạ khoá nào và xuống bao nhiêu. Người bật khấu trừ TNCN đang làm đúng nghĩa
        // vụ pháp lý; việc của thông báo này là bắt họ quyết định 2% đó lấy từ phần ai, chứ không
        // phải chặn họ lại bằng một câu "giá trị không hợp lệ".
        var performerShare = key == ConfigKeys.DonationPerformerShareRate
            ? proposed
            : others.First(c => c.Key == ConfigKeys.DonationPerformerShareRate).Value;
        var deductions = combined - performerShare;
        var maxPerformerShare = 1m - deductions;

        return $"Với donate, bốn khoản này cùng cắt trên một gốc: hoa hồng, thuế GTGT, thuế TNCN và " +
               $"phần nghệ sĩ. Tổng sẽ là {combined:P2} — chủ phòng trà giữ lại 0 đồng hoặc âm, và " +
               $"việc chi trả cho nghệ sĩ sẽ không thực hiện được sau khi đã thu tiền người donate. " +
               $"Muốn giữ mức này, hạ \"{ConfigKeys.DonationPerformerShareRate}\" xuống dưới " +
               $"{maxPerformerShare:P2} trước.";
    }
}
