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
                return ValidateRateCombination(key, dec, otherRates);

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
                return proposedValue.Length > 500
                    ? "Giá trị không được dài quá 500 ký tự."
                    : null;

            default:
                return $"Kiểu dữ liệu \"{dataType}\" chưa được hỗ trợ.";
        }
    }

    /// <summary>
    /// Commission and tax are both taken off the same gross, and the owner receives what is left.
    /// Their sum reaching 1 leaves the owner nothing — or less than nothing — which is not a policy
    /// anyone would choose deliberately and which the ledger cannot record.
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
}
