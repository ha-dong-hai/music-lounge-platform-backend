using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;
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

    // MLACP-489: Subject/Message song ngữ — nghệ sĩ không có tài khoản nên không có ngôn ngữ ưa thích, thư gửi cả hai.
    public sealed record Invitation(
        PerformerConfirmationPurpose Purpose, Guid? BankAccountId, string? BankAccountFingerprint,
        Guid? DonationId, SongNgu Subject, SongNgu Message, string? EvidenceUrl = null);

    public static Invitation ForBankAccount(BankAccount account, string plainAccountNumber)
    {
        var masked = MaskAccountNumber(plainAccountNumber);
        return new(
            PerformerConfirmationPurpose.BankAccount, account.Id, FingerprintOf(account), null,
            new SongNgu(
                "Xác nhận tài khoản nhận tiền của bạn trên MusicLounge",
                "Confirm your payout account on MusicLounge"),
            new SongNgu(
                $"Tài khoản {account.BankName} số {masked}, chủ tài khoản " +
                $"{account.AccountHolder}, vừa được đăng ký để nhận tiền donate của bạn trên MusicLounge. Hãy mở " +
                "liên kết để xác nhận đây là tài khoản của bạn — hoặc báo cho chúng tôi nếu không phải.",
                $"The {account.BankName} account number {masked}, held by {account.AccountHolder}, was just " +
                "registered to receive your donations on MusicLounge. Open the link to confirm this is your " +
                "account — or tell us if it is not."));
    }

    /// <param name="evidenceUrl">MLACP-673: ảnh chứng từ phòng trà đã nộp — đính kèm vào thư để nghệ sĩ tự đối chiếu.</param>
    public static Invitation ForDonationReceipt(Guid donationId, decimal amount, string paymentRef, string? evidenceUrl = null)
    {
        var money = amount.ToString("#,0", CultureInfo.InvariantCulture);
        return new(
            PerformerConfirmationPurpose.DonationReceipt, null, null, donationId,
            new SongNgu(
                "Xác nhận bạn đã nhận tiền donate trên MusicLounge",
                "Confirm you received a donation on MusicLounge"),
            new SongNgu(
                $"Phòng trà báo đã chuyển {money}đ tiền donate vào tài " +
                $"khoản của bạn (mã chuyển khoản {paymentRef}). Hãy mở liên kết để xác nhận đã nhận — hoặc báo nếu " +
                "bạn chưa nhận được.",
                $"The music lounge reports that it transferred {money} VND in donations to your account " +
                $"(transfer reference {paymentRef}). Open the link to confirm you received it — or tell us if " +
                "you did not."),
            evidenceUrl);
    }

    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Đổi bất kỳ trường nào (kể cả mã hoá lại cùng số tài khoản) thì đổi dấu vân tay.</summary>
    public static string FingerprintOf(BankAccount account)
    {
        var canonical = string.Concat(new[] { account.BankName, account.AccountNumber, account.AccountHolder }
            .Select(f => $"{f.Length}:{f}|"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    /// <summary>MLACP-401. Hiển thị thay cho số đã che khi số tài khoản không còn giải mã được.</summary>
    public const string UnreadableAccountNumber = "(số tài khoản không đọc được)";

    public static string MaskAccountNumber(string plain)
        => plain.Length <= 4 ? new string('*', plain.Length) : new string('*', plain.Length - 4) + plain[^4..];

    public static async Task<PerformerConfirmation> FindByTokenAsync(IUnitOfWork uow, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new NotFoundException(nameof(PerformerConfirmation), "token");
        var hash = HashToken(token);
        return (await uow.Repository<PerformerConfirmation, Guid>().FindAsync(c => c.TokenHash == hash, ct))
            .FirstOrDefault()
            ?? throw new NotFoundException(nameof(PerformerConfirmation), "token");
    }

    /// <summary>
    /// Ghi một liên kết mới vào unit of work của người gọi và XẾP HÀNG thư gửi nó tới email của nghệ sĩ. Nghệ sĩ chưa
    /// có email thì không làm gì (false). Gửi thư là việc phụ của một thao tác tiền (vd phòng trà báo đã chuyển) — để nó
    /// huỷ thao tác chính thì bản ghi tiền cũng mất theo.
    /// <para>MLACP-642: trước đây thư được gửi NGAY ở đây, bên trong giao dịch của lệnh gọi và với token huỷ của request —
    /// gửi qua Gmail mất 4–5 giây, người dùng đóng tab là cả lời báo đã trả nghệ sĩ quay lui (đo 05/10/2026). Nay chỉ xếp
    /// hàng (<see cref="Jobs.SendPerformerConfirmationEmailJob"/>); Hangfire gửi sau, có thử lại.
    /// Trần giới hạn: job được xếp ngay khi gọi, trước khi giao dịch của lệnh commit — lệnh quay lui vì lỗi khác thì thư vẫn
    /// đi với một liên kết không tồn tại (trang xác nhận báo liên kết không hợp lệ, không có tiền nào bị đụng). Đường nâng
    /// cấp: hàng đợi outbox ghi cùng giao dịch.</para>
    /// </summary>
    public static Task<bool> InviteAsync(
        IUnitOfWork uow, IBackgroundJobService jobs, BusinessSettings settings, ILogger logger,
        Performer performer, Invitation invitation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(performer.ContactEmail)) return Task.FromResult(false);

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
        uow.Repository<PerformerConfirmation, Guid>().Add(confirmation);

        if (string.IsNullOrWhiteSpace(settings.PerformerConfirmationUrl))
        {
            logger.LogError(
                "Business:PerformerConfirmationUrl chưa cấu hình — không gửi được liên kết xác nhận cho nghệ sĩ #{PerformerId}",
                performer.Id);
            return Task.FromResult(true);
        }

        var link = $"{settings.PerformerConfirmationUrl}?token={Uri.EscapeDataString(token)}";
        try
        {
            jobs.EnqueuePerformerConfirmationEmail(
                performer.ContactEmail, performer.Name, invitation.Subject, invitation.Message, link, confirmation.ExpiresAt,
                invitation.EvidenceUrl);
        }
        catch (Exception ex)
        {
            // Hàng đợi không nhận (kho Hangfire lỗi) cũng chỉ ghi log — cùng lý do như khi gửi thư hỏng ở bản cũ.
            logger.LogError(ex, "Không xếp hàng được thư xác nhận cho nghệ sĩ #{PerformerId}", performer.Id);
        }
        return Task.FromResult(true);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
