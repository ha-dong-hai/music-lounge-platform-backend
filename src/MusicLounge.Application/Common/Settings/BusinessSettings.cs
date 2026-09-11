namespace MusicLounge.Application.Common.Settings;

// Environment-specific settings only (URLs differ per deploy target).
// Business parameters (rates, durations) live in the system_config table — D9.
public sealed class BusinessSettings
{
    // 4 luong (ve/donate/subscription/fnb) MOI luong co Payment record + callback handler RIENG
    // (xem PaymentsController/DonationsController/SubscriptionsController/FnbOrdersController) —
    // khong duoc dung chung 1 ReturnUrl, neu khong VNPay se redirect ve dung 1 endpoint cho ca 4
    // loai giao dich, khien cac loai con lai luon that bai xu ly du VNPay da thu tien thanh cong.
    public string TicketPaymentReturnUrl { get; init; } = string.Empty;
    public string DonationPaymentReturnUrl { get; init; } = string.Empty;
    public string SubscriptionPaymentReturnUrl { get; init; } = string.Empty;
    public string FnbOrderPaymentReturnUrl { get; init; } = string.Empty;
    // Used to default to a hardcoded "https://musiclounge.vn/..." production URL — a deployment
    // that forgot to configure these (Production.Local.json/env vars/secret manager) would silently
    // send real users to that URL instead of failing loudly, and any future domain change would
    // require a code change instead of a config one. Empty by default like the four above;
    // Program.cs fails fast at startup if any of these seven are still unconfigured.
    public string PaymentSuccessUrl { get; init; } = string.Empty;
    public string PaymentFailedUrl { get; init; } = string.Empty;

    // MLACP-344: trang cho khach DA tra tien nhung he thong chua cap duoc gi (VNPay xac nhan cho mot
    // ban ghi da dong). Trang thanh cong la noi doi — khach khong co gi; trang that bai cung sai —
    // tien da roi khoi tai khoan, va khach thay "that bai" co the mua lai roi bi tru hai lan. De
    // trong thi quay ve PaymentFailedUrl nhu cu, de khong lam vo moi truong chua cau hinh.
    public string PaymentProcessingUrl { get; init; } = string.Empty;
    public string PasswordResetUrl { get; init; } = string.Empty;

    // MLACP-364: trang frontend nghe si mo tu lien ket trong email (?token=...). Khong nam trong danh sach
    // bat buoc cua Program.cs — de trong thi he thong van chay, chi khong gui duoc lien ket (ghi log loi),
    // de khong lam vo cac moi truong dang chay chua cau hinh.
    public string PerformerConfirmationUrl { get; init; } = string.Empty;
}
