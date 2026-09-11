using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Performers;

/// <summary>
/// MLACP-364 — nghệ sĩ tự xác nhận qua liên kết một lần, không cần tài khoản đăng nhập.
///
/// <para><b>Vì sao cần</b>: nghệ sĩ không đăng nhập được, nên tài khoản ngân hàng của họ do người tạo hồ
/// sơ nghệ sĩ (thường là chủ phòng trà) nhập và sửa, còn "đã trả nghệ sĩ" do chủ phòng trà tự khai. Mọi
/// bằng chứng về tiền của nghệ sĩ đều do một phía tạo ra. GoFundMe giải quyết đúng tình huống này bằng
/// cách mời người thụ hưởng qua email để họ tự kết nối tài khoản của mình; Stripe Express để người nhận
/// tiền tự khai thông tin, nền tảng không sửa được.</para>
///
/// <para><b>Không phải một vai trò đăng nhập</b>: liên kết chỉ làm được đúng một việc, một lần, trong
/// thời hạn. Token 256-bit ngẫu nhiên, chỉ lưu bản băm.</para>
///
/// <para><b>Giới hạn phải nói rõ</b>: email do người tạo hồ sơ nghệ sĩ nhập. Liên kết chứng minh người
/// kiểm soát hộp thư đó đã xác nhận — không chứng minh danh tính.</para>
/// </summary>
public static class PerformerConfirmations
{
    public const int LinkValidHours = 72;

    public sealed record Invitation(
        PerformerConfirmationPurpose Purpose, int? BankAccountId, string? BankAccountFingerprint,
        int? DonationId, string Subject, string Message);

    public static Invitation ForBankAccount(BankAccount account, string plainAccountNumber) => new(
        PerformerConfirmationPurpose.BankAccount, account.Id, FingerprintOf(account), null,
        "Xác nhận tài khoản nhận tiền của bạn trên MusicLounge",
        $"Tài khoản {account.BankName} số {MaskAccountNumber(plainAccountNumber)}, chủ tài khoản " +
        $"{account.AccountHolder}, vừa được đăng ký để nhận tiền donate của bạn trên MusicLounge. Hãy mở " +
        "liên kết để xác nhận đây là tài khoản của bạn — hoặc báo cho chúng tôi nếu không phải.");

    public static Invitation ForDonationReceipt(int donationId, decimal amount, string paymentRef) => new(
        PerformerConfirmationPurpose.DonationReceipt, null, null, donationId,
        "Xác nhận bạn đã nhận tiền donate trên MusicLounge",
        $"Phòng trà báo đã chuyển {amount.ToString("#,0", CultureInfo.InvariantCulture)}đ tiền donate vào tài " +
        $"khoản của bạn (mã chuyển khoản {paymentRef}). Hãy mở liên kết để xác nhận đã nhận — hoặc báo nếu " +
        "bạn chưa nhận được.");

    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Đổi bất kỳ trường nào (kể cả mã hoá lại cùng số tài khoản) thì đổi dấu vân tay.</summary>
    public static string FingerprintOf(BankAccount account)
    {
        var canonical = string.Concat(new[] { account.BankName, account.AccountNumber, account.AccountHolder }
            .Select(f => $"{f.Length}:{f}|"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static string MaskAccountNumber(string plain)
        => plain.Length <= 4 ? new string('*', plain.Length) : new string('*', plain.Length - 4) + plain[^4..];

    public static async Task<PerformerConfirmation> FindByTokenAsync(IUnitOfWork uow, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new NotFoundException(nameof(PerformerConfirmation), "token");
        var hash = HashToken(token);
        return (await uow.Repository<PerformerConfirmation, int>().FindAsync(c => c.TokenHash == hash, ct))
            .FirstOrDefault()
            ?? throw new NotFoundException(nameof(PerformerConfirmation), "token");
    }

    /// <summary>
    /// Ghi một liên kết mới vào unit of work của người gọi và gửi nó tới email của nghệ sĩ. Nghệ sĩ chưa
    /// có email thì không làm gì (false). Gửi thư thất bại chỉ được ghi log: đây là việc phụ của một thao
    /// tác tiền (vd phòng trà báo đã chuyển) — để nó huỷ thao tác chính thì bản ghi tiền cũng mất theo.
    /// </summary>
    public static async Task<bool> InviteAsync(
        IUnitOfWork uow, IEmailService email, BusinessSettings settings, ILogger logger,
        Performer performer, Invitation invitation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(performer.ContactEmail)) return false;

        var now = DateTimeOffset.UtcNow;
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var confirmation = new PerformerConfirmation
        {
            PerformerId = performer.Id,
            Purpose = invitation.Purpose,
            BankAccountId = invitation.BankAccountId,
            BankAccountFingerprint = invitation.BankAccountFingerprint,
            DonationId = invitation.DonationId,
            TokenHash = HashToken(token),
            SentToEmail = performer.ContactEmail,
            CreatedAt = now,
            ExpiresAt = now.AddHours(LinkValidHours)
        };
        uow.Repository<PerformerConfirmation, int>().Add(confirmation);

        if (string.IsNullOrWhiteSpace(settings.PerformerConfirmationUrl))
        {
            logger.LogError(
                "Business:PerformerConfirmationUrl chưa cấu hình — không gửi được liên kết xác nhận cho nghệ sĩ #{PerformerId}",
                performer.Id);
            return true;
        }

        var link = $"{settings.PerformerConfirmationUrl}?token={Uri.EscapeDataString(token)}";
        try
        {
            await email.SendPerformerConfirmationAsync(
                performer.ContactEmail, performer.Name, invitation.Subject, invitation.Message, link,
                confirmation.ExpiresAt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Gửi liên kết xác nhận cho nghệ sĩ #{PerformerId} thất bại", performer.Id);
        }
        return true;
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
