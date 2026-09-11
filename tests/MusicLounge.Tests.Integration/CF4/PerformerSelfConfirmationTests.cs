using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using Serilog.Events;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-364. Nghệ sĩ không có tài khoản đăng nhập, nên tài khoản ngân hàng của họ do người khác nhập
/// thay, và "đã trả nghệ sĩ" chỉ có lời khai của phòng trà. Nay nghệ sĩ nhận một liên kết một lần qua
/// email để tự xác nhận — hoặc phản bác.
///
/// <para>Liên kết được lấy từ nhật ký của dịch vụ email: trong test không cấu hình máy chủ SMTP nên
/// <c>SmtpEmailService</c> ghi liên kết ra log thay vì gửi — cùng cách luồng đặt lại mật khẩu vẫn chạy
/// trong dev. Mọi bước khác đi qua đúng API thật.</para>
/// </summary>
[Collection("Integration")]
public sealed class PerformerSelfConfirmationTests
{
    private const decimal Amount = 100_000m;

    private readonly ApiFactory _factory;

    public PerformerSelfConfirmationTests(ApiFactory factory) => _factory = factory;

    private sealed record Wrapped<T>(T Data);

    private sealed record InitData(int DonationId, string OrderId);

    private sealed record ConfirmationView(
        string Purpose, string State, string? AccountNumberMasked, string? AccountHolder,
        decimal? Amount, string? PaymentRef);

    private sealed record EventItem(string EventType);

    private sealed record Evidence(bool ChainIntact, List<EventItem> Events);

    private static string NewEmail() => $"artist-{Guid.NewGuid():N}@test.com";

    private async Task<int> FreshOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"confirm-owner-{Guid.NewGuid():N}@test.com", FullName = "Confirm Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private HttpClient Owner(int ownerId) => _factory.CreateAuthenticatedClient(ownerId, "Owner");

    private static async Task<int> CreatePerformerAsync(HttpClient owner, string? email)
    {
        var res = await owner.PostAsJsonAsync("/api/v1/performers", new
        {
            Name = $"Artist-{Guid.NewGuid():N}"[..20], AvatarUrl = (string?)null, Bio = (string?)null,
            Type = "Solo", GenreIds = Array.Empty<int>(), ContactEmail = email
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
    }

    private static async Task<int> CreatePerformerAccountAsync(HttpClient owner, int performerId, string number)
    {
        var res = await owner.PostAsJsonAsync("/api/v1/bank-accounts", new
        {
            OwnerType = "Performer", OwnerId = performerId, BankName = "Vietcombank",
            AccountNumber = number, AccountHolder = "NGUYEN VAN NGHE SI", IsDefault = true
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<Wrapped<int>>())!.Data;
    }

    /// <summary>Token của liên kết mới nhất đã gửi tới hộp thư này.</summary>
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

    private Task<HttpResponseMessage> RespondAsync(string token, string decision, bool consent = true, string? note = null)
        => _factory.CreateClient().PostAsJsonAsync("/api/v1/performer-confirmations/respond",
            new { Token = token, Decision = decision, ConsentToDataProcessing = consent, Note = note });

    private async Task<ConfirmationView> LookupAsync(string token)
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/performer-confirmations/lookup", new { Token = token });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Wrapped<ConfirmationView>>())!.Data;
    }

    private async Task<BankAccount> AccountAsync(int accountId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<BankAccount>().AsNoTracking().SingleAsync(a => a.Id == accountId);
    }

    // ─── Tài khoản nhận tiền ──────────────────────────────────────────────────

    [Fact]
    public async Task PerformerConfirmsTheirAccount_WithConsent_AndTheLinkWorksOnlyOnce()
    {
        var owner = Owner(await FreshOwnerAsync());
        var email = NewEmail();
        var performerId = await CreatePerformerAsync(owner, email);
        var accountId = await CreatePerformerAccountAsync(owner, performerId, "0123456789");
        var token = LatestTokenSentTo(email);

        var view = await LookupAsync(token);
        view.Purpose.Should().Be("BankAccount");
        view.State.Should().Be("Open");
        view.AccountNumberMasked.Should().Be("******6789", "đủ để nghệ sĩ nhận ra tài khoản, không lộ cả số");
        view.AccountHolder.Should().Be("NGUYEN VAN NGHE SI");

        (await RespondAsync(token, "Confirm", consent: false)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "không có sự đồng ý xử lý dữ liệu thì không xác nhận");
        (await RespondAsync(token, "Confirm")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AccountAsync(accountId)).IsVerified.Should().BeTrue("chính nghệ sĩ đã xác nhận tài khoản là của mình");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<Performer>().SingleAsync(p => p.Id == performerId)).DataConsentAt.Should().NotBeNull();
        }

        (await RespondAsync(token, "Confirm")).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity, "liên kết chỉ dùng được một lần");
    }

    [Fact]
    public async Task AccountEditedAfterTheLinkWasSent_OldLinkCannotConfirmTheNewDetails()
    {
        var owner = Owner(await FreshOwnerAsync());
        var email = NewEmail();
        var performerId = await CreatePerformerAsync(owner, email);
        var accountId = await CreatePerformerAccountAsync(owner, performerId, "0123456789");
        var oldToken = LatestTokenSentTo(email);

        (await owner.PutAsJsonAsync($"/api/v1/bank-accounts/{accountId}", new
        {
            BankName = "Vietcombank", AccountNumber = "9999888877", AccountHolder = "NGUYEN VAN NGHE SI", IsDefault = true
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var newToken = LatestTokenSentTo(email);
        newToken.Should().NotBe(oldToken, "thông tin đổi thì phải có liên kết mới");

        (await LookupAsync(oldToken)).State.Should().Be("Outdated");
        (await RespondAsync(oldToken, "Confirm")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "nghệ sĩ chưa từng thấy số tài khoản mới — liên kết cũ không được xác nhận nó");
        (await AccountAsync(accountId)).IsVerified.Should().BeFalse();

        (await RespondAsync(newToken, "Confirm")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AccountAsync(accountId)).IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task PerformerDisputesTheAccount_ItStaysUnverified_AndAdminsAreAlerted()
    {
        var owner = Owner(await FreshOwnerAsync());
        var email = NewEmail();
        var performerId = await CreatePerformerAsync(owner, email);
        var accountId = await CreatePerformerAccountAsync(owner, performerId, "0123456789");

        (await RespondAsync(LatestTokenSentTo(email), "Dispute", note: "Đây không phải tài khoản của tôi"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AccountAsync(accountId)).IsVerified.Should().BeFalse();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Notifications.AnyAsync(n =>
                n.UserId == SeedHelper.AdminId && n.Type == NotificationType.SecurityAlert
                && n.ReferenceId == accountId.ToString()))
            .Should().BeTrue("tài khoản sai có thể là nhập nhầm hoặc cố ý — Admin phải biết");
    }

    [Fact]
    public async Task PerformerWithoutAnEmail_GetsNoLink_AndNothingElseBreaks()
    {
        var owner = Owner(await FreshOwnerAsync());
        var performerId = await CreatePerformerAsync(owner, email: null);
        await CreatePerformerAccountAsync(owner, performerId, "0123456789");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<PerformerConfirmation>().CountAsync(c => c.PerformerId == performerId)).Should().Be(0);
    }

    [Fact]
    public async Task ExpiredLink_IsRefused()
    {
        var owner = Owner(await FreshOwnerAsync());
        var email = NewEmail();
        var performerId = await CreatePerformerAsync(owner, email);
        await CreatePerformerAccountAsync(owner, performerId, "0123456789");
        var token = LatestTokenSentTo(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var confirmation = await db.Set<PerformerConfirmation>().SingleAsync(c => c.PerformerId == performerId);
            confirmation.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        (await RespondAsync(token, "Confirm")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Đã nhận tiền donate ──────────────────────────────────────────────────

    private async Task<(int DonationId, string Email)> DonationReportedPaidAsync()
    {
        int ownerId, loungeId, performanceId;
        var email = NewEmail();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            var owner = new User { Email = $"receipt-owner-{Guid.NewGuid():N}@test.com", FullName = "Receipt Owner" };
            db.Users.Add(owner);
            await db.SaveChangesAsync();

            var lounge = new MusicLoungeVenue
            {
                OwnerId = owner.Id, Name = $"Receipt-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
            };
            db.Lounges.Add(lounge);
            await db.SaveChangesAsync();

            var start = DateTimeOffset.UtcNow.AddHours(-1);
            var show = new LoungeShow
            {
                LoungeId = lounge.Id, Name = $"ReceiptShow-{Guid.NewGuid():N}", Description = "test",
                Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ongoing,
                ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
            };
            var performer = new Performer
            {
                Name = $"ReceiptArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id, ContactEmail = email
            };
            db.Add(show);
            db.Add(performer);
            await db.SaveChangesAsync();

            db.Add(new BankAccount
            {
                OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
                AccountNumber = pii.Encrypt("0000000364"), AccountHolder = "Receipt Owner", IsDefault = true
            });
            db.Add(new BankAccount
            {
                OwnerType = BankAccountOwnerType.Performer, OwnerId = performer.Id, BankName = "Test Bank",
                AccountNumber = pii.Encrypt("0000000365"), AccountHolder = "Receipt Artist", IsDefault = true
            });
            var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
            db.Add(performance);
            await db.SaveChangesAsync();
            (ownerId, loungeId, performanceId) = (owner.Id, lounge.Id, performance.Id);
        }

        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var created = await audience.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = performanceId, Amount, IsAnonymous = false, Message = (string?)null, IsMessagePublic = true
        });
        var init = (await created.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;
        (await _factory.CreateClient().GetAsync(
                $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}" +
                $"&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        var ownerClient = _factory.CreateAuthenticatedClient(ownerId, "Owner", loungeId);
        (await ownerClient.PostAsync($"/api/v1/donations/{init.DonationId}/acknowledge", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ownerClient.PostAsJsonAsync($"/api/v1/donations/{init.DonationId}/confirm-paid",
                new { PaymentRef = "CK-364", PaymentEvidenceUrl = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (init.DonationId, email);
    }

    private async Task<Evidence> EvidenceOfAsync(int donationId)
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync($"/api/v1/admin/donations/{donationId}/evidence");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Wrapped<Evidence>>())!.Data;
    }

    [Fact]
    public async Task PerformerConfirmsReceipt_AndItIsRecordedInTheEvidenceLog()
    {
        var (donationId, email) = await DonationReportedPaidAsync();
        var token = LatestTokenSentTo(email);

        var view = await LookupAsync(token);
        view.Purpose.Should().Be("DonationReceipt");
        view.Amount.Should().Be(88_000m, "đúng số phòng trà báo đã chuyển (88% gross)");
        view.PaymentRef.Should().Be("CK-364");

        (await RespondAsync(token, "Confirm")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var evidence = await EvidenceOfAsync(donationId);
        evidence.ChainIntact.Should().BeTrue();
        evidence.Events.Last().EventType.Should().Be("PerformerConfirmedReceipt",
            "lời xác nhận của chính nghệ sĩ là mắt xích thiếu nhất của bằng chứng \"đã trả\"");
    }

    [Fact]
    public async Task PerformerSaysNotReceived_ItIsRecorded_AndAComplaintIsOpened()
    {
        var (donationId, email) = await DonationReportedPaidAsync();

        (await RespondAsync(LatestTokenSentTo(email), "Dispute", note: "Tôi chưa nhận được tiền"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EvidenceOfAsync(donationId)).Events.Last().EventType.Should().Be("PerformerDisputedReceipt");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<Complaint>().AnyAsync(c =>
                c.TargetType == "donation" && c.TargetId == donationId
                && c.Category == ComplaintCategory.DonationNotPaid && c.Status == ComplaintStatus.Open))
            .Should().BeTrue("một bên nói đã chuyển, một bên nói chưa nhận — phải có người xử lý");
    }
}
