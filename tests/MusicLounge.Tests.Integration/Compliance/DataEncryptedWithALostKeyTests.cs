using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Performers;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using Serilog.Events;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-401. Lần triển khai 04/09/2026 xoá mất bộ khoá Data Protection; giá trị mã hoá trước đó không còn giải mã được và
/// <c>GET /bank-accounts</c> trên Azure trả 500. Dữ liệu đó không cứu được — nhưng đọc nó không được làm hỏng cả trang, và
/// không được để tiền đi vào một số tài khoản không ai đọc được.
///
/// <para>"Khoá đã mất" được tạo lại đúng cách: mã hoá bằng một bộ khoá tạm khác, cùng purpose với app. Bộ khoá của app không có
/// khoá đó, nên giải mã lỗi y như trên Azure.</para>
/// </summary>
[Collection("Integration")]
public sealed class DataEncryptedWithALostKeyTests
{
    private const string Unreadable = "không đọc được";

    private readonly ApiFactory _factory;

    public DataEncryptedWithALostKeyTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(bool Success, T Data);

    /// <summary>Mã hoá bằng một bộ khoá mà app không có — đúng trạng thái của dữ liệu trước 04/09 trên Azure.</summary>
    private static string UnderALostKey(string plain)
        => new EphemeralDataProtectionProvider().CreateProtector("MusicLounge.PiiAtRest.v1").Protect(plain);

    private string UnderTheCurrentKey(string plain)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>().Encrypt(plain);
    }

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<(int OwnerId, int LoungeId)> VenueAsync(bool cardApproved = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User
        {
            Email = $"lostkey401-{Guid.NewGuid():N}@test.com", FullName = "Chủ phòng trà 401", Role = UserRole.Owner,
            IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow,
            CitizenCardSubmittedAt = cardApproved ? DateTimeOffset.UtcNow.AddDays(-2) : null,
            CitizenCardReviewStatus = cardApproved ? KycReviewStatus.Approved : null,
            CitizenCardVerifiedName = cardApproved ? "Chủ phòng trà 401" : null
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"LostKey401 {Guid.NewGuid():N}"[..22], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (owner.Id, lounge.Id);
    }

    private async Task<int> LoungeAccountAsync(int loungeId, string ciphertext, bool isDefault, bool verified)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = loungeId, BankName = "Vietcombank",
            AccountNumber = ciphertext, AccountHolder = "CHU PHONG TRA 401", IsDefault = isDefault, IsVerified = verified
        };
        db.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    // ── Đọc không làm hỏng cả trang ──────────────────────────────────────────

    [Fact]
    public async Task TheBankAccountList_FlagsTheUnreadableNumber_AndStillShowsTheReadableOnes()
    {
        var (ownerId, loungeId) = await VenueAsync();
        var readable = await LoungeAccountAsync(loungeId, UnderTheCurrentKey("0123456789"), isDefault: true, verified: false);
        var lost = await LoungeAccountAsync(loungeId, UnderALostKey("9876543210"), isDefault: false, verified: false);

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner")
            .GetAsync($"/api/v1/bank-accounts?ownerType=Lounge&ownerId={loungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK, "one unreadable row must not take the whole list down with a 500");
        using var json = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        var lostItem = items.Single(i => i.GetProperty("id").GetInt32() == lost);
        var readableItem = items.Single(i => i.GetProperty("id").GetInt32() == readable);
        lostItem.GetProperty("accountNumber").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        lostItem.GetProperty("accountNumberUnreadable").GetBoolean().Should().BeTrue();
        readableItem.GetProperty("accountNumber").GetString().Should().Be("0123456789");
        readableItem.GetProperty("accountNumberUnreadable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task TheTaxProfile_FlagsAnUnreadableTaxCode()
    {
        var (ownerId, _) = await VenueAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.SingleAsync(u => u.Id == ownerId);
            owner.BusinessType = PayeeBusinessType.HouseholdOrIndividual;
            owner.TaxCode = UnderALostKey("0101243150");
            owner.TaxCodeHash = Guid.NewGuid().ToString("N");
            owner.TaxProfileSubmittedAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }

        var res = await _factory.CreateAuthenticatedClient(ownerId, "Owner").GetAsync("/api/v1/me/tax-profile");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("taxCode").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        data.GetProperty("taxCodeUnreadable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TheKycQueue_FlagsAnUnreadableCardNumberAndTaxCode()
    {
        var (ownerId, _) = await VenueAsync(cardApproved: false);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Users.SingleAsync(u => u.Id == ownerId);
            owner.CitizenCardNumber = UnderALostKey("012345678901");
            owner.CitizenCardNumberHash = Guid.NewGuid().ToString("N");
            // Nộp từ lâu để nằm đầu hàng đợi (sắp theo thời điểm nộp), không rơi khỏi trang đầu.
            owner.CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddYears(-20);
            owner.CitizenCardReviewStatus = KycReviewStatus.Pending;
            owner.TaxCode = UnderALostKey("0101243151");
            owner.TaxCodeHash = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }

        var res = await Admin().GetAsync("/api/v1/admin/kyc-reviews?status=Pending&pageSize=100");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("userId").GetInt32() == ownerId);
        item.GetProperty("citizenCardNumberUnreadable").GetBoolean().Should().BeTrue();
        item.GetProperty("taxCodeUnreadable").GetBoolean().Should().BeTrue();
    }

    // ── Tiền không đi vào một số không ai đọc được ───────────────────────────

    [Fact]
    public async Task AnAccountWhoseNumberCannotBeRead_CannotBeVerified()
    {
        var (_, loungeId) = await VenueAsync();
        var accountId = await LoungeAccountAsync(loungeId, UnderALostKey("9876543210"), isDefault: true, verified: false);

        var res = await Admin().PostAsJsonAsync($"/api/v1/admin/bank-accounts/{accountId}/review", new { Approve = true, Note = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("không còn đọc được");
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<BankAccount>().AsNoTracking()
            .SingleAsync(a => a.Id == accountId)).IsVerified.Should().BeFalse();
    }

    private async Task<(int OwnerId, int SettlementId)> DuePayoutIntoAnUnreadableAccountAsync(bool accountVerified)
    {
        var (ownerId, loungeId) = await VenueAsync();
        var accountId = await LoungeAccountAsync(loungeId, UnderALostKey("9876543210"), isDefault: true, verified: accountVerified);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(-20);
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"Show {Guid.NewGuid():N}"[..18], Status = LoungeShowStatus.Ended,
            ScheduledStart = start, ScheduledEnd = start.AddHours(2), ActualStart = start, ActualEnd = start.AddHours(2),
            CreatedAt = DateTime.UtcNow
        };
        db.Add(show);
        await db.SaveChangesAsync();
        var payment = new Payment
        {
            OrderId = $"MLACP401-{Guid.NewGuid():N}"[..30], PayerId = SeedHelper.AudienceId, GrossAmount = 1_000_000m,
            NetAmount = 880_000m, Status = PaymentStatus.Confirmed, ReferenceType = "TicketHold", ReferenceId = "0",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();
        db.Add(new Ticket
        {
            Id = Guid.NewGuid(), BuyerId = SeedHelper.AudienceId, PriceId = SeedHelper.TicketPriceId,
            TierId = SeedHelper.TicketTierId, ShowId = show.Id, PaymentId = payment.Id, Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow
        });
        var settlement = new Settlement
        {
            OwnerId = ownerId, PaymentId = payment.Id, BankAccountId = accountId,
            ReleaseType = SettlementReleaseType.Partial70, GrossAmount = 1_000_000m, PreRateApplied = 0.70m,
            PostRateApplied = 0.30m, NetAmount = 616_000m, Status = SettlementStatus.Scheduled,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(settlement);
        await db.SaveChangesAsync();
        return (ownerId, settlement.Id);
    }

    private async Task RunReleaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>().ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<(SettlementStatus Status, List<Notification> OwnerNotices, int AdminNotices)> AfterReleaseAsync(int ownerId, int settlementId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var status = (await db.Settlements.AsNoTracking().SingleAsync(s => s.Id == settlementId)).Status;
        var reference = ownerId.ToString();
        var notices = await db.Notifications.AsNoTracking()
            .Where(n => n.Type == NotificationType.PayoutOnHold && n.ReferenceId == reference).ToListAsync();
        var adminIds = await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync();
        return (status, notices.Where(n => n.UserId == ownerId).ToList(), notices.Count(n => adminIds.Contains(n.UserId)));
    }

    [Fact]
    public async Task APayoutIntoAVerifiedAccountWhoseNumberCannotBeRead_IsHeld_AndTheOwnerIsAskedToReEnterIt()
    {
        var (ownerId, settlementId) = await DuePayoutIntoAnUnreadableAccountAsync(accountVerified: true);

        await RunReleaseAsync();

        var (status, ownerNotices, adminNotices) = await AfterReleaseAsync(ownerId, settlementId);
        status.Should().Be(SettlementStatus.Scheduled, "money must not be released to a number nobody can read");
        ownerNotices.Should().ContainSingle().Which.Body.Should().Contain("nhập lại tài khoản");
        adminNotices.Should().Be(0, "only the owner can re-enter the number");
    }

    [Fact]
    public async Task AnUnverifiedAccountWhoseNumberCannotBeRead_AsksTheOwner_NotTheAdmins()
    {
        // Không đọc được số thì Admin có xác minh cũng vô ích — người cần làm là chủ phòng trà.
        var (ownerId, settlementId) = await DuePayoutIntoAnUnreadableAccountAsync(accountVerified: false);

        await RunReleaseAsync();

        var (status, ownerNotices, adminNotices) = await AfterReleaseAsync(ownerId, settlementId);
        status.Should().Be(SettlementStatus.Scheduled);
        ownerNotices.Should().ContainSingle().Which.Body.Should().Contain("nhập lại tài khoản");
        adminNotices.Should().Be(0);
    }

    // ── Liên kết xác nhận của nghệ sĩ ────────────────────────────────────────

    private async Task<(int AccountId, string Token)> PerformerAccountNowUnreadableAsync(bool keepFingerprintInStep)
    {
        var (ownerId, _) = await VenueAsync();
        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner");
        var email = $"artist401-{Guid.NewGuid():N}@test.com";
        var performerRes = await owner.PostAsJsonAsync("/api/v1/performers", new
        {
            Name = $"Artist-{Guid.NewGuid():N}"[..20], AvatarUrl = (string?)null, Bio = (string?)null,
            Type = "Solo", GenreIds = Array.Empty<int>(), ContactEmail = email
        });
        performerRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var performerId = (await performerRes.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
        var accountRes = await owner.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Performer", OwnerId = performerId, BankName = "Vietcombank",
            AccountNumber = "0999888777", AccountHolder = "NGUYEN VAN NGHE SI", IsDefault = true
        });
        accountRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var accountId = (await accountRes.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
        var token = LatestTokenSentTo(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = await db.Set<BankAccount>().SingleAsync(a => a.Id == accountId);
        account.AccountNumber = UnderALostKey("0999888777");
        if (keepFingerprintInStep)
        {
            // Liên kết so dấu vân tay tính trên chuỗi đã mã hoá; cập nhật để liên kết không bị coi là đã cũ.
            var confirmation = await db.Set<PerformerConfirmation>().Where(c => c.BankAccountId == accountId)
                .OrderByDescending(c => c.Id).FirstAsync();
            confirmation.BankAccountFingerprint = PerformerConfirmations.FingerprintOf(account);
        }
        await db.SaveChangesAsync();
        return (accountId, token);
    }

    private static string LatestTokenSentTo(string email)
    {
        var sent = CapturingLogSink.Snapshot()
            .Where(e => e.Properties.TryGetValue("ConfirmationLink", out _)
                        && e.Properties.TryGetValue("Email", out var to)
                        && to is ScalarValue { Value: string address } && address == email)
            .LastOrDefault();
        sent.Should().NotBeNull($"phải có một liên kết xác nhận được gửi tới {email}");
        var link = (string)((ScalarValue)sent!.Properties["ConfirmationLink"]).Value!;
        return Uri.UnescapeDataString(link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
    }

    [Fact]
    public async Task LookingUpAPerformerConfirmation_ForAnUnreadableNumber_SaysSoInsteadOfFailing()
    {
        var (_, token) = await PerformerAccountNowUnreadableAsync(keepFingerprintInStep: false);

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/performer-confirmations/lookup", new { Token = token });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").GetProperty("accountNumberMasked").GetString().Should().Contain(Unreadable);
    }

    [Fact]
    public async Task APerformerDisputingAnUnreadableAccount_StillAlertsTheAdmins()
    {
        var (accountId, token) = await PerformerAccountNowUnreadableAsync(keepFingerprintInStep: true);

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/performer-confirmations/respond",
            new { Token = token, Decision = "Dispute", ConsentToDataProcessing = true, Note = "Không phải tài khoản của tôi" });

        res.IsSuccessStatusCode.Should().BeTrue("the security alert must still reach the admins");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AsNoTracking().AnyAsync(n => n.Type == NotificationType.SecurityAlert
                && n.ReferenceId == accountId.ToString() && n.Body.Contains(Unreadable)))
            .Should().BeTrue();
    }
}
