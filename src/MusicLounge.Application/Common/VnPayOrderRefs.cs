using MusicLounge.Application.Subscriptions;

namespace MusicLounge.Application.Common;

/// <summary>
/// MLACP-394. Tiền tố mã giao dịch VNPay (<c>vnp_TxnRef</c>) hệ thống tự sinh cho từng luồng thanh toán online, và cách
/// một IPN tìm lại luồng của nó.
///
/// <para>VNPay gọi IPN tới URL gắn với terminal (<c>vnp_TmnCode</c>), không nhận URL theo từng giao dịch (tài liệu tích
/// hợp VNPay: "Gửi lại VNPAY URL này khi thiết lập xong"). Bốn luồng dùng chung một terminal, nên mọi IPN vào một cửa
/// (<c>/payments/vnpay/ipn</c>) rồi mới rẽ nhánh theo tiền tố. Thêm luồng thanh toán online mới thì thêm tiền tố ở đây
/// và dùng chính hằng này khi sinh mã — router và nơi sinh mã không được lệch nhau.</para>
/// </summary>
public static class VnPayOrderRefs
{
    public const string TicketPrefix = "ML-";
    public const string DonationPrefix = "DON-";
    public const string FnbOrderPrefix = "FNB-";

    /// <summary><c>Payment.ReferenceType</c> của một lần mua vé online — handler IPN của vé chỉ được nhận đúng loại này.</summary>
    public const string TicketPaymentReferenceType = "TicketHold";

    /// <summary>Mã không mang tiền tố nào khác đi đường vé — đúng hành vi của URL chung trước MLACP-394.</summary>
    public static VnPayFlow FlowOf(string? txnRef)
    {
        if (string.IsNullOrEmpty(txnRef)) return VnPayFlow.Ticket;
        if (txnRef.StartsWith(DonationPrefix, StringComparison.Ordinal)) return VnPayFlow.Donation;
        if (txnRef.StartsWith(FnbOrderPrefix, StringComparison.Ordinal)) return VnPayFlow.FnbOrder;
        if (txnRef.StartsWith(SubscriptionTerms.SubscribePrefix, StringComparison.Ordinal)
            || txnRef.StartsWith(SubscriptionTerms.RenewPrefix, StringComparison.Ordinal)
            || txnRef.StartsWith(SubscriptionTerms.ChangePrefix, StringComparison.Ordinal))
            return VnPayFlow.Subscription;
        return VnPayFlow.Ticket;
    }
}

/// <summary>Luồng thanh toán online mà một giao dịch VNPay thuộc về.</summary>
public enum VnPayFlow
{
    Ticket,
    Donation,
    FnbOrder,
    Subscription
}
