using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-360. Cảnh báo donate trên livestream từng chỉ phát khi chủ phòng trà bấm "đã nhận" — bấm
/// sau khi buổi phát kết thúc thì người donate không bao giờ thấy lời nhắn của mình trên sóng. Nay nó
/// phát ngay khi VNPay xác nhận, lời nhắn qua bộ lọc từ cấm, và người điều hành phòng trà gỡ được.
///
/// <para>Mỗi bài dựng một phòng trà riêng với một buổi diễn đang phát, và đọc đúng cái được gửi
/// xuống livestream của buổi đó (<see cref="RecordingLivestreamHubService"/>).</para>
/// </summary>
[Collection("Integration")]
public sealed class DonationLiveAlertTests
{
    private const decimal Amount = 100_000m;
    private const string BlockedWordsKey = "donation_message_blocked_words";

    private readonly ApiFactory _factory;

    public DonationLiveAlertTests(ApiFactory factory) => _factory = factory;

    private sealed record Venue(int OwnerId, int LoungeId, int StaffId, int PerformanceId, int LivestreamId);

    private sealed record InitData(int DonationId, string OrderId);

    private sealed record Wrapped<T>(T Data);

    private RecordingLivestreamHubService Hub =>
        (RecordingLivestreamHubService)_factory.Services.GetRequiredService<ILivestreamHubService>();

    private List<DonationAlertDto> AlertsOn(int livestreamId) => Hub.For(livestreamId)
        .Where(s => s.Event == "DonationAlert")
        .Select(s => (DonationAlertDto)s.Payload!)
        .ToList();

    private async Task<Venue> SeedVenueAsync(LivestreamStatus streamStatus = LivestreamStatus.Live)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"live-owner-{Guid.NewGuid():N}@test.com", FullName = "Live Alert Owner" };
        var staff = new User { Email = $"live-staff-{Guid.NewGuid():N}@test.com", FullName = "Live Alert Staff" };
        db.Users.AddRange(owner, staff);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"LiveAlert-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();

        // Nhân viên thật sự được phân công — ActiveUserBehavior (D6) từ chối mọi token Staff không còn
        // phân công hiệu lực tại đúng phòng trà đó.
        db.Add(new LoungeStaff
        {
            LoungeId = lounge.Id, UserId = staff.Id, AssignedBy = owner.Id,
            IsActive = true, AssignedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"LiveAlertShow-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Online, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"LiveAlertArtist-{Guid.NewGuid():N}"[..25], CreatedByUserId = owner.Id };
        db.Add(show);
        db.Add(performer);
        await db.SaveChangesAsync();

        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id };
        var livestream = new Livestream { LoungeShowId = show.Id, Status = streamStatus, StartedAt = start };
        db.Add(performance);
        db.Add(livestream);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, staff.Id, performance.Id, livestream.Id);
    }

    private async Task<int> DonateAndConfirmAsync(
        int performanceId, string? message = "Hay quá!", bool anonymous = false, bool messagePublic = true)
    {
        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await audience.PostAsJsonAsync("/api/v1/donations", new
        {
            PerformanceId = performanceId, Amount, IsAnonymous = anonymous,
            Message = message, IsMessagePublic = messagePublic
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var init = (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;

        var ipn = await _factory.CreateClient().GetAsync(
            $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}" +
            $"&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}");
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);

        return init.DonationId;
    }

    private Task<HttpResponseMessage> SetBlockedWordsAsync(string value)
        => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin").PutAsJsonAsync(
            $"/api/v1/admin/system-config/{BlockedWordsKey}",
            new { ConfigValue = value, Note = "Kiểm tra bộ lọc lời nhắn donate" });

    // ─── Phát ngay khi VNPay xác nhận ─────────────────────────────────────────

    [Fact]
    public async Task PaidDonation_IsAnnouncedOnTheLivestream_TheMomentVnPayConfirms()
    {
        var venue = await SeedVenueAsync();

        var donationId = await DonateAndConfirmAsync(venue.PerformanceId, "Hay quá!");

        var alert = AlertsOn(venue.LivestreamId).Should().ContainSingle(
            "VNPay xác nhận là đủ — không còn phải chờ chủ phòng trà bấm gì").Subject;
        alert.DonationId.Should().Be(donationId);
        alert.Amount.Should().Be(Amount);
        alert.Message.Should().Be("Hay quá!");
        alert.DonorName.Should().NotBeNullOrWhiteSpace().And.NotBe("Ẩn danh");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Donations.SingleAsync(d => d.Id == donationId)).Status
            .Should().Be(DonationStatus.PendingOwnerAck, "chủ phòng trà chưa làm gì cả");
    }

    [Fact]
    public async Task AnonymousDonor_AndPrivateMessage_AreRespectedOnAir()
    {
        var venue = await SeedVenueAsync();

        await DonateAndConfirmAsync(venue.PerformanceId, "Chỉ gửi riêng", anonymous: true, messagePublic: false);

        var alert = AlertsOn(venue.LivestreamId).Should().ContainSingle().Subject;
        alert.DonorName.Should().Be("Ẩn danh");
        alert.Message.Should().BeNull("người donate không để lời nhắn công khai");
        alert.Amount.Should().Be(Amount);
    }

    [Fact]
    public async Task NothingIsAnnounced_WhenTheShowIsNotOnAir()
    {
        var venue = await SeedVenueAsync(LivestreamStatus.Scheduled);

        await DonateAndConfirmAsync(venue.PerformanceId);

        AlertsOn(venue.LivestreamId).Should().BeEmpty();
    }

    [Fact]
    public async Task OwnerAcknowledging_DoesNotAnnounceTheSameDonationAgain()
    {
        var venue = await SeedVenueAsync();
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId);

        var owner = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
        (await owner.PostAsync($"/api/v1/donations/{donationId}/acknowledge", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        AlertsOn(venue.LivestreamId).Should().ContainSingle("một donate chỉ được xướng một lần trên sóng");
    }

    // ─── Bộ lọc từ cấm ────────────────────────────────────────────────────────

    [Fact]
    public async Task BlockedWord_KeepsTheMessageOffAir_ButTheDonationIsStillAnnounced()
    {
        var venue = await SeedVenueAsync();
        (await SetBlockedWordsAsync("[\"tu khoa cam\"]")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        try
        {
            // Viết hoa, có dấu — vẫn phải bị bắt: gõ không dấu / đổi hoa thường là cách lách phổ biến.
            var blockedId = await DonateAndConfirmAsync(venue.PerformanceId, "Đây là TỪ KHÓA CẤM nhé!");
            // So khớp theo từ: "camera" không phải "cấm".
            var cleanId = await DonateAndConfirmAsync(venue.PerformanceId, "Góc tu khoa camera đẹp quá");

            var alerts = AlertsOn(venue.LivestreamId);
            var blocked = alerts.Single(a => a.DonationId == blockedId);
            blocked.Message.Should().BeNull("lời nhắn dính từ cấm không được lên sóng");
            blocked.Amount.Should().Be(Amount, "bản thân donate vẫn được xướng lên — tiền là thật");

            alerts.Single(a => a.DonationId == cleanId).Message.Should().Be("Góc tu khoa camera đẹp quá");
        }
        finally
        {
            (await SetBlockedWordsAsync("[]")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task MalformedBlockedWordsList_IsRefusedWhenWritten()
    {
        // Để lỗi định dạng lọt tới lúc phát sóng thì mọi lời nhắn bị giữ lại mà không ai biết vì sao.
        (await SetBlockedWordsAsync("tu khoa cam")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ─── Gỡ lời nhắn ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ThisVenuesStaff_TakesTheMessageOffAir_AndTheOriginalIsKept()
    {
        var venue = await SeedVenueAsync();
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId, "Lời nhắn cần gỡ");

        var staff = _factory.CreateAuthenticatedClient(venue.StaffId, "Staff", venue.LoungeId);
        (await staff.PostAsync($"/api/v1/donations/{donationId}/hide-message", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Hub.For(venue.LivestreamId)
            .Should().Contain(s => s.Event == "DonationMessageHidden" && (int)s.Payload! == donationId,
                "màn hình của người đang xem phải gỡ lời nhắn ngay, không đợi tải lại");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var donation = await db.Donations.SingleAsync(d => d.Id == donationId);
        donation.MessageHiddenAt.Should().NotBeNull();
        donation.MessageHiddenByUserId.Should().Be(venue.StaffId, "phải biết ai đã gỡ");
        donation.Message.Should().Be("Lời nhắn cần gỡ", "lời nhắn gốc giữ lại để đối chiếu khi có khiếu nại");
    }

    [Fact]
    public async Task OnlyThisVenuesOperators_CanHideAMessage()
    {
        var venue = await SeedVenueAsync();
        var donationId = await DonateAndConfirmAsync(venue.PerformanceId, "Lời nhắn của tôi");

        var audience = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        (await audience.PostAsync($"/api/v1/donations/{donationId}/hide-message", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var otherVenuesStaff = _factory.CreateAuthenticatedClient(
            SeedHelper.OtherVenueStaffId, "Staff", SeedHelper.OtherLoungeId);
        (await otherVenuesStaff.PostAsync($"/api/v1/donations/{donationId}/hide-message", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "cùng vai trò nhân viên nhưng khác phòng trà");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Donations.SingleAsync(d => d.Id == donationId)).MessageHiddenAt.Should().BeNull();
    }
}
