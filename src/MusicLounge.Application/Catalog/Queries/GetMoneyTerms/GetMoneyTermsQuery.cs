using MediatR;
using MusicLounge.Application.Catalog.DTOs;
using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Donations;
using MusicLounge.Application.Refunds;
using MusicLounge.Application.Tickets;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Catalog.Queries.GetMoneyTerms;

/// <summary>MLACP-625: biểu phí và điều khoản tiền đang áp dụng — xem <see cref="MoneyTermsDto"/>.</summary>
public sealed record GetMoneyTermsQuery : IQuery<MoneyTermsDto>;

internal sealed class GetMoneyTermsQueryHandler(ISystemConfigService config, IUnitOfWork uow)
    : IRequestHandler<GetMoneyTermsQuery, MoneyTermsDto>
{
    /// <summary>
    /// Các khoá mà lịch sử thay đổi được CÔNG BỐ — đúng những khoá Handle đọc bên dưới (trực tiếp hoặc qua RefundSla,
    /// RefundGatewayWindow, WalkInCommission, PublicDonationStatement). Thêm một con số vào MoneyTermsDto thì thêm khoá
    /// của nó vào đây, nếu không lần đổi của nó sẽ không hiện trong nhật ký. KHÔNG có test nào ép việc này — danh sách
    /// và các lệnh đọc bên dưới phải được sửa cùng nhau bằng tay.
    /// </summary>
    internal static readonly string[] PublishedKeys =
    [
        ConfigKeys.PlatformCommissionRate, ConfigKeys.TaxRate, ConfigKeys.PersonalIncomeTaxRate,
        ConfigKeys.TicketHoldMinutes, ConfigKeys.TicketHoldMaxQuantity, ConfigKeys.WalkInCommissionEnabled,
        ConfigKeys.RefundSlaHours, ConfigKeys.RefundAutoApproveGraceHours, ConfigKeys.VnPayRefundWindowDays,
        ConfigKeys.DonationPerformerShareRate, ConfigKeys.DonationHoldDays, ConfigKeys.DonationMaxAmount,
        ConfigKeys.SettlementPartialHoursAfterShow, ConfigKeys.SettlementFinalDaysAfterShow,
        ConfigKeys.SettlementTierNewPreRate, ConfigKeys.SettlementTierStandardPreRate,
        ConfigKeys.SettlementTierPremiumPreRate, ConfigKeys.SettlementTierStandardMinScore,
        ConfigKeys.SettlementTierPremiumMinScore, ConfigKeys.SettlementTierPremiumMinShows,
    ];

    /// <summary>Trần nhật ký trả về. Đổi tham số tiền là việc hiếm (mỗi lần phải ghi lý do); quá 50 dòng thì cần phân
    /// trang — khi đó tách thành endpoint riêng.</summary>
    private const int MaxChanges = 50;

    public async Task<MoneyTermsDto> Handle(GetMoneyTermsQuery request, CancellationToken ct)
    {
        // Ba tỉ lệ dưới đây là khoá CÓ seed (dòng trong DB luôn thắng); mặc định viết lại cho khớp các handler tiền
        // (WriteTicketLedgerHandler, TaxWithholdingPolicy). Đây là tỉ lệ áp cho hộ/cá nhân kinh doanh — doanh nghiệp đã
        // xác minh hồ sơ thuế không bị khấu trừ (TaxWithholdingPolicy.Resolve), client phải nói rõ điều đó.
        var commission = await config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
        var vat = await config.GetDecimalAsync(ConfigKeys.TaxRate, 0.05m, ct);
        var personalIncomeTax = await config.GetDecimalAsync(ConfigKeys.PersonalIncomeTaxRate, 0m, ct);

        // Khoá KHÔNG seed: đọc qua đúng hàm dùng chung để không sinh nguồn mặc định thứ hai
        // (UnseededConfigFallbackSingleSourceTests).
        var reviewHours = await RefundSla.SlaHoursAsync(config, ct);
        var graceHours = await RefundSla.AutoApproveGraceHoursAsync(config, ct);
        var gatewayWindowDays = await RefundGatewayWindow.WindowDaysAsync(config, ct);
        var walkInThroughPlatform = await WalkInCommission.IsEnabledAsync(config, ct);

        // Chính sách ủng hộ: dùng lại thứ trang sao kê công khai đang công bố, để hai nơi không nói hai con số.
        var donation = await PublicDonationStatement.PolicyAsync(config, ct);
        var venueShare = Math.Max(0m, 1m - donation.PerformerShareRate - commission - vat - personalIncomeTax);

        // Sắp xếp trong bộ nhớ: SQLite (test) không ORDER BY được DateTimeOffset — cùng cách với GetSystemConfigHistory.
        var changes = (await uow.Repository<SystemConfigHistory, Guid>()
                .FindAsync(h => PublishedKeys.Contains(h.ConfigKey), ct))
            .OrderByDescending(h => h.EffectiveFrom)
            .Take(MaxChanges)
            .Select(h => new MoneyTermChangeDto(h.ConfigKey, h.OldValue, h.NewValue, h.EffectiveFrom))
            .ToList();

        return new MoneyTermsDto(
            new TicketMoneyTermsDto(
                commission, vat, personalIncomeTax,
                await config.GetIntAsync(ConfigKeys.TicketHoldMinutes, 15, ct),
                await config.GetIntAsync(ConfigKeys.TicketHoldMaxQuantity, 10, ct),
                walkInThroughPlatform),
            new RefundMoneyTermsDto(reviewHours, reviewHours + graceHours, gatewayWindowDays),
            new DonationMoneyTermsDto(
                donation.PerformerShareRate, commission, vat, personalIncomeTax, venueShare,
                donation.VenuePayoutDays, donation.VenueWarningDays, donation.Refundable,
                await config.GetDecimalAsync(ConfigKeys.DonationMaxAmount, 50_000_000m, ct)),
            new SettlementMoneyTermsDto(
                await config.GetIntAsync(ConfigKeys.SettlementPartialHoursAfterShow, 48, ct),
                await config.GetIntAsync(ConfigKeys.SettlementFinalDaysAfterShow, 14, ct),
                await config.GetDecimalAsync(ConfigKeys.SettlementTierNewPreRate, 0.50m, ct),
                await config.GetDecimalAsync(ConfigKeys.SettlementTierStandardPreRate, 0.70m, ct),
                await config.GetDecimalAsync(ConfigKeys.SettlementTierPremiumPreRate, 0.80m, ct),
                await config.GetDecimalAsync(ConfigKeys.SettlementTierStandardMinScore, 3.5m, ct),
                await config.GetDecimalAsync(ConfigKeys.SettlementTierPremiumMinScore, 4.2m, ct),
                await config.GetIntAsync(ConfigKeys.SettlementTierPremiumMinShows, 10, ct)),
            changes.Count > 0 ? changes[0].EffectiveFrom : null,
            changes);
    }
}
