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
/// MLACP-641. Ủng hộ nghệ sĩ chỉ dành cho người đang xem buổi phát (chủ dự án chốt 05/10/2026: ủng hộ ở khung chat của
/// màn xem trực tuyến). Đo 05/10: tài khoản KHÔNG vé và tài khoản chỉ có vé TẠI CHỖ đều tạo được khoản ủng hộ (201) cho
/// buổi kết hợp có phí — trả xong thì lời nhắn của họ lên khung chat của buổi trả phí, vòng qua cổng vé của LivestreamHub.
/// Kèm theo: khi chủ phòng trà tắt khung chat, lời nhắn ủng hộ không còn lên sóng (tên + số tiền vẫn được xướng).
/// </summary>
[Collection("Integration")]
public sealed class DonationRequiresViewerAccessTests
{
    private const decimal Amount = 50_000m;
    private readonly ApiFactory _factory;

    public DonationRequiresViewerAccessTests(ApiFactory factory) => _factory = factory;

    private sealed record Show(Guid PerformanceId, Guid LivestreamId, Guid OnlineViewerId, Guid InPersonGuestId, Guid NoTicketId);
    private sealed record InitData(Guid DonationId, string OrderId);
    private sealed record Wrapped<T>(T Data);

    private RecordingLivestreamHubService Hub =>
        (RecordingLivestreamHubService)_factory.Services.GetRequiredService<ILivestreamHubService>();

    /// <summary>Buổi KẾT HỢP đang diễn, phát CÓ PHÍ, một nghệ sĩ nhận ủng hộ; ba khán giả: vé trực tuyến, vé tại chỗ, không vé.</summary>
    private async Task<Show> SeedHybridShowAsync(bool isFree = false, bool chatEnabled = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User
        {
            Email = $"don641-owner-{Guid.NewGuid():N}@test.com", FullName = "Chu 641", Role = UserRole.Owner,
            CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-30), CitizenCardReviewStatus = KycReviewStatus.Approved
        };
        User Khach(string ten) => new() { Email = $"don641-{ten}-{Guid.NewGuid():N}@test.com", FullName = ten, Role = UserRole.Audience };
        var online = Khach("Truc tuyen"); var inPerson = Khach("Tai cho"); var noTicket = Khach("Khong ve");
        db.Users.AddRange(owner, online, inPerson, noTicket);
        await db.SaveChangesAsync();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Venue641-{Guid.NewGuid():N}"[..30], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = lounge.Id, BankName = "Test Bank",
            AccountNumber = pii.Encrypt("0000000641"), AccountHolder = "Chu 641", IsDefault = true, IsVerified = true
        });
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id, Name = $"Show641-{Guid.NewGuid():N}", Description = "test",
            Format = LoungeShowFormat.Hybrid, Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start, ScheduledEnd = start.AddHours(3), VcpmcRoyaltyReference = "VCPMC-TEST"
        };
        var performer = new Performer { Name = $"Artist641-{Guid.NewGuid():N}"[..20], CreatedByUserId = owner.Id };
        db.AddRange(show, performer);
        await db.SaveChangesAsync();
        var performance = new Performance { LoungeShowId = show.Id, PerformerId = performer.Id, AcceptsDonation = true };
        var livestream = new Livestream
        {
            LoungeShowId = show.Id, Status = LivestreamStatus.Live, StartedAt = start, IsFree = isFree, ChatEnabled = chatEnabled
        };
        var onlineTier = new TicketTier { LoungeShowId = show.Id, Name = "Truc tuyen", AccessType = AccessType.Livestream, TotalCapacity = 10 };
        var inPersonTier = new TicketTier { LoungeShowId = show.Id, Name = "Tai cho", AccessType = AccessType.Physical, TotalCapacity = 10 };
        db.AddRange(performance, livestream, onlineTier, inPersonTier);
        await db.SaveChangesAsync();
        var onlinePrice = new TicketPrice { TierId = onlineTier.Id, Name = "Thuong", Price = 100_000m, Quota = 10, IsActive = true, SaleStart = start.AddDays(-5), PurchaseChannel = PurchaseChannel.Online };
        var inPersonPrice = new TicketPrice { TierId = inPersonTier.Id, Name = "Thuong", Price = 300_000m, Quota = 10, IsActive = true, SaleStart = start.AddDays(-5), PurchaseChannel = PurchaseChannel.Both };
        db.AddRange(onlinePrice, inPersonPrice);
        await db.SaveChangesAsync();
        db.AddRange(
            new Ticket { Id = Guid.NewGuid(), BuyerId = online.Id, PriceId = onlinePrice.Id, TierId = onlineTier.Id, ShowId = show.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow },
            new Ticket { Id = Guid.NewGuid(), BuyerId = inPerson.Id, PriceId = inPersonPrice.Id, TierId = inPersonTier.Id, ShowId = show.Id, Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return new Show(performance.Id, livestream.Id, online.Id, inPerson.Id, noTicket.Id);
    }

    private Task<HttpResponseMessage> DonateAsync(Guid userId, Guid performanceId, string? message = "Hay quá!")
        => _factory.CreateAuthenticatedClient(userId, "Audience").PostAsJsonAsync("/api/v1/donations",
            new { PerformanceId = performanceId, Amount, IsAnonymous = false, Message = message, IsMessagePublic = true });

    [Fact]
    public async Task AnOnlineTicketHolder_CanDonate()
    {
        var show = await SeedHybridShowAsync();

        (await DonateAsync(show.OnlineViewerId, show.PerformanceId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SomeoneWithoutATicket_CannotDonateToAPaidStream()
    {
        var show = await SeedHybridShowAsync();

        var res = await DonateAsync(show.NoTicketId, show.PerformanceId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "otherwise a paid donation puts their message into the chat of a stream they have no ticket for");
        (await res.Content.ReadAsStringAsync()).Should().Contain("vé xem trực tuyến");
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Donations.AsNoTracking()
            .AnyAsync(d => d.DonorUserId == show.NoTicketId)).Should().BeFalse("no donation row, no VNPay link");
    }

    [Fact]
    public async Task AnInPersonTicket_DoesNotCountAsWatchingTheStream()
    {
        var show = await SeedHybridShowAsync();

        (await DonateAsync(show.InPersonGuestId, show.PerformanceId)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "an in-person ticket of a hybrid show does not include the online stream (LivestreamRepository.HasViewerAccessAsync)");
    }

    [Fact]
    public async Task AFreeStream_LetsAnySignedInViewerDonate()
    {
        var show = await SeedHybridShowAsync(isFree: true);

        (await DonateAsync(show.NoTicketId, show.PerformanceId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WithTheChatTurnedOff_TheDonationIsStillAnnounced_ButWithoutItsMessage()
    {
        var show = await SeedHybridShowAsync(chatEnabled: false);

        var res = await DonateAsync(show.OnlineViewerId, show.PerformanceId, "Lời nhắn khi chat đang tắt");
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var init = (await res.Content.ReadFromJsonAsync<Wrapped<InitData>>())!.Data;
        (await _factory.CreateClient().GetAsync(
                $"/api/v1/donations/vnpay-ipn?vnp_TxnRef={Uri.EscapeDataString(init.OrderId)}&vnp_ResponseCode=00&vnp_Amount={(long)(Amount * 100)}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var alert = Hub.For(show.LivestreamId).Where(s => s.Event == "DonationAlert").Select(s => (DonationAlertDto)s.Payload!)
            .Should().ContainSingle("the money is real — the donation is still announced").Subject;
        alert.Message.Should().BeNull("the owner turned the chat off; a donation must not be a way around that");
    }
}
