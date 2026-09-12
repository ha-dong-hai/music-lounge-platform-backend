using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Refunds.Commands.ProvideRefundPayoutAccount;

/// <param name="Consent">Người mua xác nhận đồng ý nhận hoàn bằng chuyển khoản vào tài khoản này, thay cho phương thức đã
/// thanh toán (Luật BVQLNTD 2023 Điều 38 khoản 4).</param>
public sealed record ProvideRefundPayoutAccountCommand(
    int RefundRequestId,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool Consent) : ICommand;
