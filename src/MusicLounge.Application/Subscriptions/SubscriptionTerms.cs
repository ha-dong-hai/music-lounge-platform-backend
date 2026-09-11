using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions;

/// <summary>MLACP-371 — một thanh toán gói được tạo từ lệnh nào.</summary>
public enum SubscriptionPurchase
{
    Subscribe,  // đăng ký lần đầu / sau khi hết hạn
    Renew,      // gia hạn — cộng thêm một kỳ, kể cả khi gói còn hạn
    Change      // đổi sang gói khác, có hiệu lực ngay
}

/// <summary>
/// MLACP-371 — quy tắc chung của huỷ / gia hạn / đổi gói.
///
/// <para><b>Gia hạn sớm cộng dồn</b> — Google Play gói trả trước (cùng mô hình với nền tảng này: trả một lần,
/// không tự gia hạn): "entitlement is extended by the duration specified in the top-up".</para>
///
/// <para><b>Đổi gói có hiệu lực ngay, không mất phần đã trả</b> — Stripe khi đổi gói giữa kỳ: "Unused time on
/// original plan (credit)". Nền tảng không hoàn tiền mặt phần này (Shopify: "account credits ... instead of a
/// refund"); phần giá trị còn lại được quy thành thời gian ở gói mới, theo giá của gói mới.</para>
/// </summary>
public static class SubscriptionTerms
{
    public const string SubscribePrefix = "SUB-";
    public const string RenewPrefix = "RNW-";
    public const string ChangePrefix = "CHG-";

    /// <summary>Mã đơn mang theo lệnh đã tạo nó — IPN đọc lại để biết thanh toán này dùng vào việc gì.</summary>
    public static string NewOrderId(SubscriptionPurchase purchase, DateTimeOffset now)
    {
        var prefix = purchase switch
        {
            SubscriptionPurchase.Renew => RenewPrefix,
            SubscriptionPurchase.Change => ChangePrefix,
            _ => SubscribePrefix
        };
        return $"{prefix}{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
    }

    public static SubscriptionPurchase PurchaseOf(string orderId)
        => orderId.StartsWith(RenewPrefix, StringComparison.Ordinal) ? SubscriptionPurchase.Renew
            : orderId.StartsWith(ChangePrefix, StringComparison.Ordinal) ? SubscriptionPurchase.Change
            : SubscriptionPurchase.Subscribe;

    public static DateTimeOffset CycleEnd(SubscriptionBillingCycle cycle, DateTimeOffset from) => cycle switch
    {
        SubscriptionBillingCycle.Monthly => from.AddMonths(1),
        SubscriptionBillingCycle.Quarterly => from.AddMonths(3),
        SubscriptionBillingCycle.Yearly => from.AddYears(1),
        _ => from.AddMonths(1)
    };

    /// <summary>
    /// Giá trị còn lại của gói, theo số đã trả và phần thời gian còn lại. Gói có từ trước khi lưu số đã trả thì
    /// dùng giá niêm yết của gói.
    ///
    /// <para>MLACP-375: ngày được <b>bù miễn phí</b> (tạm khoá oan — <see cref="PenaltyType.Suspension"/> trong
    /// <c>ApplyDuePenaltiesJob</c>; hoặc gộp từ gói bị khoá được phục hồi — <c>PenaltySubscriptions.
    /// RestoreAfterBanLifted</c>) không phải ngày đã trả tiền, dù nó đã đẩy <c>ExpiresAt</c> xa hơn — không
    /// tính vào giá trị quy đổi. Không thể trừ thẳng "tổng số ngày được bù" khỏi phần còn lại: nếu chủ
    /// <b>gia hạn sau khi được bù</b>, ngày miễn phí không còn nằm cuối cùng của khoảng thời gian nữa. Nên dùng
    /// đúng khoảng thời gian đã ghi trên từng <c>VenuePenalty</c> (không đoán vị trí) và lấy phần chồng lấn
    /// thật với khoảng đang xét — đúng bất kể chủ có gia hạn xen giữa hay không.</para>
    /// </summary>
    public static decimal RemainingValue(
        OwnerSubscription plan, decimal listPrice, DateTimeOffset now,
        IEnumerable<VenuePenalty>? compensations = null)
    {
        var total = plan.ExpiresAt - plan.StartedAt;
        var left = plan.ExpiresAt - now;
        if (total <= TimeSpan.Zero || left <= TimeSpan.Zero) return 0m;

        var freeTotal = TimeSpan.Zero;
        var freeLeft = TimeSpan.Zero;
        foreach (var grant in (compensations ?? [])
                     .Where(p => p.CompensatedSubscriptionId == plan.Id
                                 && p.SubscriptionCompensationFrom is not null
                                 && p.SubscriptionCompensationDays is decimal))
        {
            var from = grant.SubscriptionCompensationFrom!.Value;
            var to = from.AddDays((double)grant.SubscriptionCompensationDays!.Value);
            freeTotal += Overlap(from, to, plan.StartedAt, plan.ExpiresAt);
            freeLeft += Overlap(from, to, now, plan.ExpiresAt);
        }

        var paidTotal = total - freeTotal;
        var paidLeft = left - freeLeft;
        if (paidTotal <= TimeSpan.Zero || paidLeft <= TimeSpan.Zero) return 0m;

        var paid = plan.AmountPaid ?? listPrice;
        return Math.Round(paid * paidLeft.Ticks / paidTotal.Ticks, 2);
    }

    private static TimeSpan Overlap(DateTimeOffset from, DateTimeOffset to, DateTimeOffset lo, DateTimeOffset hi)
    {
        var start = from > lo ? from : lo;
        var end = to < hi ? to : hi;
        return end > start ? end - start : TimeSpan.Zero;
    }

    /// <summary>Một khoản giá trị đổi được bao nhiêu thời gian ở gói mới, theo giá của gói mới.</summary>
    public static TimeSpan TimeWorth(decimal value, decimal newPrice, TimeSpan newCycle)
        => value <= 0m || newPrice <= 0m
            ? TimeSpan.Zero
            : TimeSpan.FromTicks((long)(newCycle.Ticks * (value / newPrice)));

    public static string DescribeCredit(decimal credit, TimeSpan extra)
        => $"{credit:N0}đ ≈ {extra.TotalDays:0.#} ngày";

    /// <summary>
    /// Mỗi lúc chỉ một lệnh gia hạn / đổi gói chờ thanh toán: bấm hai lần không được thành hai lần gia hạn.
    /// Lệnh bỏ dở tự huỷ khi CancelAbandonedPaymentsJob đánh dấu thanh toán Failed.
    /// </summary>
    public static async Task EnsureNoPendingChangeAsync(IUnitOfWork uow, int ownerId, CancellationToken ct)
    {
        var pending = await uow.Repository<Payment, int>().FindAsync(
            p => p.PayerId == ownerId
                 && p.ReferenceType == SubscriptionPayments.ReferenceType
                 && p.Status == PaymentStatus.Pending, ct);
        if (pending.Any(p => PurchaseOf(p.OrderId) != SubscriptionPurchase.Subscribe))
            throw new ConflictException(
                "Bạn đang có một lệnh gia hạn hoặc đổi gói chờ thanh toán — hãy hoàn tất nó, hoặc đợi nó tự huỷ nếu bỏ dở.");
    }
}
