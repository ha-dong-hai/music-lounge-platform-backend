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
    /// </summary>
    public static decimal RemainingValue(OwnerSubscription plan, decimal listPrice, DateTimeOffset now)
    {
        var total = plan.ExpiresAt - plan.StartedAt;
        var left = plan.ExpiresAt - now;
        if (total <= TimeSpan.Zero || left <= TimeSpan.Zero) return 0m;
        var paid = plan.AmountPaid ?? listPrice;
        return Math.Round(paid * left.Ticks / total.Ticks, 2);
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
