using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-397. Luật Thương mại điện tử 2025 Điều 17 khoản 1 điểm c: nền tảng trung gian xác thực danh tính người bán
/// "trước khi cho phép bán hàng". Trước task này chủ phòng trà chưa ai duyệt CCCD vẫn nộp duyệt buổi diễn và bán F&amp;B —
/// MLACP-395 mới giữ tiền ở khâu chi trả.
///
/// <para>Mỗi bài một chủ phòng trà và một phòng trà riêng, không đụng dữ liệu mẫu dùng chung.</para>
/// </summary>
[Collection("Integration")]
public sealed class SellingRequiresVerifiedSellerTests
{
    private const string BuyerMessage = "tạm ngừng giao dịch trên nền tảng";

    private readonly ApiFactory _factory;

    public SellingRequiresVerifiedSellerTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record Venue(int OwnerId, int LoungeId, int MenuItemId);

    private async Task<Venue> VenueAsync(KycReviewStatus? identity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User
        {
            Email = $"seller397-{Guid.NewGuid():N}@test.com", FullName = "Chủ Phòng Trà", Role = UserRole.Owner,
            IsActive = true, EmailVerifiedAt = DateTimeOffset.UtcNow,
            CitizenCardSubmittedAt = identity is null ? null : DateTimeOffset.UtcNow.AddDays(-1),
            CitizenCardReviewStatus = identity
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = owner.Id, Name = $"Seller397 {Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var menu = new FnbMenu { LoungeId = lounge.Id, Name = "Menu", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Add(menu);
        await db.SaveChangesAsync();
        var item = new FnbMenuItem
        {
            MenuId = menu.Id, Category = "Drink", Name = "Trà đào", Price = 45_000m, IsAvailable = true
        };
        db.Add(item);
        await db.SaveChangesAsync();

        return new Venue(owner.Id, lounge.Id, item.Id);
    }

    private async Task SetIdentityAsync(int ownerId, KycReviewStatus? identity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == ownerId)).CitizenCardReviewStatus = identity;
        await db.SaveChangesAsync();
    }

    private async Task<int> DraftShowAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"Seller397-{Guid.NewGuid():N}", Description = "MLACP-397",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Draft,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(30), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(30).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    private async Task<int> PriceOfPublishedShowAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = loungeId, Name = $"Seller397-{Guid.NewGuid():N}", Description = "MLACP-397",
            Format = LoungeShowFormat.Offline, Status = LoungeShowStatus.Published,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(3), ScheduledEnd = DateTimeOffset.UtcNow.AddDays(3).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        var tier = new TicketTier { LoungeShowId = show.Id, Name = "Vào cửa", AccessType = AccessType.Physical, TotalCapacity = 50 };
        db.Add(tier);
        await db.SaveChangesAsync();
        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Đợt 1", Price = 100_000m, Quota = 50, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1), SaleEnd = DateTimeOffset.UtcNow.AddDays(2),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();
        return price.Id;
    }

    private HttpClient Owner(Venue venue) => _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner", venue.LoungeId);
    private HttpClient Audience() => _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

    private static object OrderBody(Venue venue) => new
    {
        LoungeId = venue.LoungeId, ShowId = (int?)null, ZoneId = (int?)null, TableNote = (string?)null,
        PaymentMethod = "Cash", Note = (string?)null,
        Items = new[] { new { MenuItemId = venue.MenuItemId, Quantity = 1, Note = (string?)null } }
    };

    // ── Nộp duyệt buổi diễn: lúc nền tảng cho phép mở bán vé ─────────────────

    [Theory]
    [InlineData(null, "chưa nộp CCCD/CMND")]
    [InlineData(KycReviewStatus.Pending, "đang chờ Admin xác minh")]
    [InlineData(KycReviewStatus.Rejected, "bị từ chối")]
    public async Task SubmittingAShow_ByASellerNotYetVerified_IsRefusedWithWhatToDoNext(
        KycReviewStatus? identity, string whatToDoNext)
    {
        var venue = await VenueAsync(identity);
        var showId = await DraftShowAsync(venue.LoungeId);

        var res = await Owner(venue).PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain(whatToDoNext);
    }

    [Fact]
    public async Task SubmittingAShow_ByAVerifiedSeller_PassesTheIdentityGate()
    {
        // Không kỳ vọng nộp duyệt thành công: bản nháp chưa có hạng vé — đó là cổng kế tiếp và phải giữ nguyên.
        var venue = await VenueAsync(KycReviewStatus.Approved);
        var showId = await DraftShowAsync(venue.LoungeId);

        var body = await (await Owner(venue).PostAsync($"/api/v1/lounge-shows/{showId}/submit", null))
            .Content.ReadAsStringAsync();

        body.Should().NotContain("CCCD/CMND");
        body.Should().Contain("hạng vé", "cổng kế tiếp mới là cổng được phép chặn ở đây");
    }

    [Fact]
    public async Task AnAdmin_CannotSubmitAShowOnBehalfOfAnUnverifiedSeller()
    {
        var venue = await VenueAsync(identity: null);
        var showId = await DraftShowAsync(venue.LoungeId);

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("chưa nộp CCCD/CMND",
            "điều kiện nằm ở người bán, không ở người bấm");
    }

    // ── F&B: không có bước nộp duyệt, nên chốt ở lúc tạo đơn và lúc thu tiền ─

    [Fact]
    public async Task OrderingFnb_FromAnUnverifiedSeller_TellsTheBuyerOnlyThatItIsNotAvailable()
    {
        var venue = await VenueAsync(identity: null);

        var res = await Audience().PostAsJsonAsync("/api/v1/fnb-orders", OrderBody(venue));

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(BuyerMessage);
        body.Should().NotContain("CCCD", "chuyện xác minh danh tính là giữa chủ phòng trà với nền tảng");
    }

    [Fact]
    public async Task OrderingFnb_AtTheCounterOfAnUnverifiedSeller_TellsTheVenueWhy()
    {
        var venue = await VenueAsync(KycReviewStatus.Pending);

        var res = await Owner(venue).PostAsJsonAsync("/api/v1/fnb-orders", OrderBody(venue));

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("đang chờ Admin xác minh");
    }

    [Fact]
    public async Task OrderingFnb_FromAVerifiedSeller_Works()
    {
        var venue = await VenueAsync(KycReviewStatus.Approved);

        (await Audience().PostAsJsonAsync("/api/v1/fnb-orders", OrderBody(venue)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PayingAnFnbOrder_AfterTheSellerFellBackToUnverified_IsRefusedBeforeAnyPaymentIsOpened()
    {
        var venue = await VenueAsync(KycReviewStatus.Approved);
        var created = await Audience().PostAsJsonAsync("/api/v1/fnb-orders", OrderBody(venue));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderId = (await created.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        // Nộp lại CCCD đưa hồ sơ về chờ duyệt.
        await SetIdentityAsync(venue.OwnerId, KycReviewStatus.Pending);

        var res = await Audience().PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "tiền thu ở bước này");
        (await res.Content.ReadAsStringAsync()).Should().Contain(BuyerMessage);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<Payment>().AnyAsync(p => p.ReferenceType == "FnbOrder" && p.ReferenceId == orderId.ToString()))
            .Should().BeFalse("không được mở giao dịch thanh toán nào");
    }

    // ── Vé của buổi đã được phép bán ─────────────────────────────────────────

    [Fact]
    public async Task TicketsOfAnAlreadyPublishedShow_KeepSellingWhileTheSellerIsReReviewed()
    {
        // Chốt ở lúc cho phép bán (nộp duyệt), không ở từng lần bán: chủ phòng trà nộp lại CCCD giữa lúc buổi diễn đang
        // mở bán thì khán giả vẫn giữ chỗ được.
        var venue = await VenueAsync(KycReviewStatus.Approved);
        var priceId = await PriceOfPublishedShowAsync(venue.LoungeId);
        await SetIdentityAsync(venue.OwnerId, KycReviewStatus.Pending);

        var res = await Audience().PostAsJsonAsync("/api/v1/tickets/holds", new { PriceId = priceId, Quantity = 1 });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
