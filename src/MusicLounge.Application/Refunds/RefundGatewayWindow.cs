using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Refunds;

/// <summary>
/// MLACP-387. "Giao dịch này còn hoàn được qua VNPay không" — một câu trả lời cho mọi nơi hỏi: duyệt hoàn, hai job
/// hoàn tiền, khai tài khoản nhận hoàn, và danh sách yêu cầu hoàn. Trước task này câu hỏi được tính lại ở từng chỗ, và
/// job cảnh báo còn áp hạn VNPay cho cả vé tiền mặt mà VNPay chưa từng biết tới.
///
/// <para>Hạn chỉ áp cho thanh toán qua cổng (<see cref="PaymentMethod.Gateway"/>). Qua hạn thì đường duy nhất còn lại là
/// chuyển khoản — mà Luật BVQLNTD 2023 Điều 38 khoản 4 cho phép hoàn bằng phương thức khác phương thức đã thanh toán chỉ
/// khi người tiêu dùng đồng ý (<see cref="NeedsPayoutAccount"/>).</para>
/// </summary>
public static class RefundGatewayWindow
{
    public const int DefaultDays = 90;

    public static Task<int> WindowDaysAsync(ISystemConfigService config, CancellationToken ct)
        => config.GetIntAsync(ConfigKeys.VnPayRefundWindowDays, DefaultDays, ct);

    public static DateTimeOffset Deadline(DateTimeOffset transactionAt, int windowDays)
        => transactionAt.AddDays(windowDays);

    public static bool IsClosed(PaymentMethod method, DateTimeOffset transactionAt, int windowDays, DateTimeOffset now)
        => method == PaymentMethod.Gateway && Deadline(transactionAt, windowDays) < now;

    public static bool IsClosed(Payment payment, int windowDays, DateTimeOffset now)
        => IsClosed(payment.Method, payment.PaidAt ?? payment.CreatedAt, windowDays, now);

    /// <summary>Yêu cầu còn chờ, VNPay không còn hoàn được, và người mua chưa đồng ý nhận bằng chuyển khoản.</summary>
    public static bool NeedsPayoutAccount(RefundRequest refund, Payment? payment, int windowDays, DateTimeOffset now)
        => refund.Status == RefundRequestStatus.Pending
           && refund.PayoutConsentAt is null
           && payment is not null
           && IsClosed(payment, windowDays, now);

    /// <summary>Chỉ để lộ 4 số cuối trong thông báo và mô tả bút toán.</summary>
    public static string Masked(string? accountNumber)
        => string.IsNullOrEmpty(accountNumber) || accountNumber.Length <= 4
            ? accountNumber ?? ""
            : new string('*', accountNumber.Length - 4) + accountNumber[^4..];
}
