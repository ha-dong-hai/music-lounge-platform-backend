using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-363. Mỗi khoản donate trước đây chỉ có vài cột trạng thái bị ghi đè qua từng bước — không có
/// dòng thời gian, không biết cột nào bị sửa về sau, và ảnh chứng từ chuyển khoản chỉ là một URL. Khi có
/// tranh chấp, không có gì để chứng minh.
///
/// <para>Các bài đi qua đúng đường thật (tạo donate → IPN → job giải ngân → xác nhận → báo đã chuyển →
/// gỡ lời nhắn), rồi đọc nhật ký qua endpoint của Admin.</para>
/// </summary>
[Collection("Integration")]
public sealed class DonationEvidenceLogTests
{
    private const decimal Amount = 100_000m;

    private readonly ApiFactory _factory;

    public DonationEvidenceLogTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int PerformanceId);

    private sealed record InitData(int DonationId, string OrderId);

    private sealed record Wrapped<T>(T Data);

    private sealed record UploadData(string Url);

    private sealed record EventItem(
        int Sequence, string EventType, int? ActorUserId, decimal? Amount, string? Reference,
        string? EvidenceUrl, string? EvidenceSha256, string Hash);

    private sealed record Evidence(int DonationId, bool ChainIntact, int? FirstBrokenSequence, List<EventItem> Events);

    private async Task<Venue> SeedVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();

        var owner = new User { Email = $"evidence-owner-{Guid.NewGuid():N}@test.com", FullName = "Evidence Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Evidence-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"EvidenceShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"EvidenceArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();

        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000000363"), AccountHolder = "Evidence Owner", IsDefault = true
        });
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Performer, OwnerId = performer.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000000364"), AccountHolder = "Evidence Artist", IsDefault = true
        });
        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        db.Add(performance);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, performance.Id);
    }

    private HttpClient OwnerOf(Venue venue) => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);

    private async Task<int> DonateAndConfirmAsync(int performanceId)
    {
        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await audience.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = performanceId, Amount, IsAnonymous = false, Message = "Cảm ơn!", IsMessagePublic = true
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var init = (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;

        var ipn = await _factory.CreateClient().GetAsync(
            $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}");
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        return init.DonationId;
    }

    private async Task RunReleaseJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SettlementReleaseJob>()
            .ExecuteAsync(new JobCancellationToken(false));
    }

    private static byte[] EvidenceImageBytes()
    {
        // PNG hợp lệ ở phần chữ ký + đuôi ngẫu nhiên để mỗi bài có một file khác nhau.
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        return header.Concat(Guid.NewGuid().ToByteArray()).ToArray();
    }

    private static async Task<string> UploadAsync(HttpClient client, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", $"receipt-{Guid.NewGuid():N}.png");
        var res = await client.PostAsync("/api/v1/uploads/images", form);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Wrapped<UploadData>>())!.Data.Url;
    }

    /// <summary>Đi hết vòng đời: VNPay → giải ngân → xác nhận → báo đã chuyển (kèm chứng từ) → gỡ lời nhắn.</summary>
    private async Task<int> RunLifecycleAsync(Venue venue, string evidenceUrl)
    {
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);
        await RunReleaseJobAsync();

        var owner = OwnerOf(venue);
        (await owner.PostAsync($"/api/v1/donations/{donationId}/acknowledge", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PostAsJsonAsync($"/api/v1/donations/{donationId}/confirm-paid",
                new { PaymentRef = "CK-363-OK", PaymentEvidenceUrl = evidenceUrl }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.PostAsync($"/api/v1/donations/{donationId}/hide-message", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        return donationId;
    }

    private async Task<Evidence> EvidenceOfAsync(int donationId)
    {
        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync($"/api/v1/admin/donations/{donationId}/evidence");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Wrapped<Evidence>>())!.Data;
    }

    [Fact]
    public async Task EveryStep_IsRecordedInOrder_WithWhoDidIt_AndAnIntactHashChain()
    {
        var venue = await SeedVenueAsync();
        var bytes = EvidenceImageBytes();
        var evidenceUrl = await UploadAsync(OwnerOf(venue), bytes);

        var donationId = await RunLifecycleAsync(venue, evidenceUrl);
        var evidence = await EvidenceOfAsync(donationId);

        evidence.ChainIntact.Should().BeTrue();
        evidence.Events.Select(e => e.EventType).Should().Equal(
            "PaymentConfirmed", "PayoutReleased", "VenueAcknowledged", "VenueReportedPaid", "MessageHidden");
        evidence.Events.Select(e => e.Sequence).Should().Equal(1, 2, 3, 4, 5);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
        var payment = await db.Payments.SingleAsync(p => p.ReferenceType == "Donation" && p.ReferenceId == donationId.ToString());

        var confirmed = evidence.Events[0];
        confirmed.ActorUserId.Should().BeNull("cổng thanh toán xác nhận, không phải ai trong hệ thống");
        confirmed.Amount.Should().Be(Amount);
        confirmed.Reference.Should().Be(payment.TransactionId, "mã giao dịch VNPay là bằng chứng đối soát với cổng");

        var released = evidence.Events[1];
        released.ActorUserId.Should().BeNull();
        released.Amount.Should().Be(donation.Net);

        evidence.Events[2].ActorUserId.Should().Be(venue.OwnerId);

        var reportedPaid = evidence.Events[3];
        reportedPaid.ActorUserId.Should().Be(venue.OwnerId);
        reportedPaid.Reference.Should().Be("CK-363-OK");
        reportedPaid.EvidenceUrl.Should().Be(evidenceUrl);
        reportedPaid.EvidenceSha256.Should().Be(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            "bản băm phải là của đúng nội dung file chủ phòng trà đã nộp — thay file về sau sẽ không còn khớp");

        evidence.Events[4].ActorUserId.Should().Be(venue.OwnerId);
    }

    [Fact]
    public async Task EditingAnyRecordedStep_BreaksTheChain_AndIsReported()
    {
        var venue = await SeedVenueAsync();
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);
        await RunReleaseJobAsync();

        // Ai đó sửa thẳng trong database số tiền nền tảng đã chuyển cho phòng trà.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var released = await db.Set<DonationEvent>()
                .SingleAsync(e => e.DonationId == donationId && e.EventType == DonationEventType.PayoutReleased);
            released.Amount += 1_000m;
            await db.SaveChangesAsync();
        }

        var evidence = await EvidenceOfAsync(donationId);
        evidence.ChainIntact.Should().BeFalse();
        evidence.FirstBrokenSequence.Should().Be(2, "chính dòng bị sửa là dòng đầu tiên không còn khớp");
    }

    [Fact]
    public async Task ExternalEvidenceLink_IsRecorded_ButNeverFetched_OrHashed()
    {
        var venue = await SeedVenueAsync();
        var donationId = await RunLifecycleAsync(venue, "https://bank.example/receipt/363");

        var reportedPaid = (await EvidenceOfAsync(donationId)).Events.Single(e => e.EventType == "VenueReportedPaid");
        reportedPaid.EvidenceUrl.Should().Be("https://bank.example/receipt/363");
        reportedPaid.EvidenceSha256.Should().BeNull("liên kết bên ngoài không được tải về (tránh SSRF), nên không có gì để băm");
    }

    [Fact]
    public async Task SystemAutoAcknowledgement_IsRecordedAsTheSystem_NotAsTheVenue()
    {
        var venue = await SeedVenueAsync();
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);
        await RunReleaseJobAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var payment = await db.Payments.SingleAsync(p => p.ReferenceType == "Donation" && p.ReferenceId == donationId.ToString());
            var settlement = await db.Settlements.SingleAsync(s => s.PaymentId == payment.Id);
            settlement.ReleasedAt = DateTimeOffset.UtcNow.AddDays(-30);
            await db.SaveChangesAsync();
        }
        using (var scope = _factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AutoConfirmDonationsJob>()
                .ExecuteAsync(new JobCancellationToken(false));

        var evidence = await EvidenceOfAsync(donationId);
        evidence.ChainIntact.Should().BeTrue();
        var auto = evidence.Events.Single(e => e.EventType == "VenueAutoAcknowledged");
        auto.ActorUserId.Should().BeNull("phòng trà không bấm gì — không được ghi như thể phòng trà đã xác nhận");
    }

    [Fact]
    public async Task OnlyAdmin_CanExportTheEvidenceLog()
    {
        var venue = await SeedVenueAsync();
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);

        (await OwnerOf(venue).GetAsync($"/api/v1/admin/donations/{donationId}/evidence"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
