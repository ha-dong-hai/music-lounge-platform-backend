using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Tickets;

/// <summary>
/// BR-31, MLACP-309. Giờ nhận khách cuối.
///
/// BR-31 phát biểu là "cho phép bỏ trống mốc đóng đợt bán vé" — một chỉ thị về cấu trúc dữ liệu,
/// không phải một quy tắc nghiệp vụ. Làm đúng chữ thì ra kết quả sai: nếu "bỏ trống" nghĩa là bán
/// tới khi buổi diễn kết thúc, hệ thống sẽ bán vé nguyên giá cho một chương trình còn năm phút.
/// Khách trả tiền để xem cái gì?
///
/// Quy tắc đúng là mốc nhận khách cuối: buổi diễn phải còn lại đủ một khoảng đáng đồng tiền. Mốc
/// mặc định 60 phút lấy theo Eventbrite cho vé vào cửa tự do, nguyên văn tài liệu của họ: "By
/// default, your ticket sales end an hour before your event ends" — tính lùi từ lúc KẾT THÚC, đúng
/// với tình huống bán cho người đến muộn.
///
/// Và nó là trần cứng chứ không phải giá trị mặc định: một điều khoản bảo vệ khách hàng mà Owner
/// tắt được bằng một ô nhập liệu thì không bảo vệ được ai.
/// </summary>
[Collection("Integration")]
public sealed class LastEntryCutoffTests
{
    private readonly ApiFactory _factory;

    public LastEntryCutoffTests(ApiFactory factory) => _factory = factory;

    private const int ShowHours = 4;

    /// <summary>
    /// Dựng một buổi diễn đang diễn ra, bắt đầu cách đây <paramref name="startedHoursAgo"/> tiếng
    /// và kéo dài 4 tiếng, kèm một đợt bán vé tại quầy.
    /// </summary>
    private async Task<(int PriceId, int LoungeId)> SeedOngoingShowAsync(
        double startedHoursAgo, DateTimeOffset? saleEnd)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"LastEntryVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Cửa Vào", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddHours(-startedHoursAgo);
        var show = new LoungeShow
        {
            LoungeId = lounge.Id,
            Name = $"LastEntryShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = LoungeShowStatus.Ongoing,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(ShowHours)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        var tier = new TicketTier
        {
            LoungeShowId = show.Id, Name = "Thường",
            AccessType = AccessType.Physical, TotalCapacity = 200
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Tại quầy", Price = 300_000m, Quota = 200, IsActive = true,
            SaleStart = start.AddDays(-7),
            SaleEnd = saleEnd,
            PurchaseChannel = PurchaseChannel.Offline
        };
        db.Add(price);
        await db.SaveChangesAsync();

        return (price.Id, lounge.Id);
    }

    private Task<HttpResponseMessage> SellAtTheDoorAsync(int priceId, int loungeId)
        => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", loungeId)
            .PostAsJsonAsync("/api/v1/tickets/walk-in", new { PriceId = priceId, Quantity = 1 });

    // ---------- điều BR-31 thật sự muốn ----------

    [Fact]
    public async Task ALatecomerCanStillBuyAtTheDoor_WithoutTheOwnerHavingSetAnyCutoff()
    {
        // Đây là cái BR-31 nhắm tới: trước đây mốc đóng là bắt buộc, nên Owner phải chốt một giờ
        // từ nhiều tuần trước, và nhân viên ở quầy chỉ thấy hệ thống từ chối mà không hiểu vì sao.
        // Buổi diễn đã đi được nửa chặng, còn 2 tiếng — thừa sức đáng tiền vé.
        var (priceId, loungeId) = await SeedOngoingShowAsync(startedHoursAgo: 2, saleEnd: null);

        var res = await SellAtTheDoorAsync(priceId, loungeId);

        res.StatusCode.Should().Be(HttpStatusCode.Created,
            "khách mua vé khi chương trình đã diễn một phần là chuyện bình thường của phòng trà");
    }

    // ---------- và điều BR-31 không nói, nhưng phải có ----------

    [Fact]
    public async Task NobodyCanBuyAFullPriceTicketToAShowThatIsAlmostOver()
    {
        // Buổi diễn còn 30 phút. Nếu để "bỏ trống mốc đóng = bán tới hết buổi diễn" thì lời gọi
        // này thành công, và khách trả nguyên giá 300.000đ cho nửa tiếng cuối.
        var (priceId, loungeId) = await SeedOngoingShowAsync(startedHoursAgo: 3.5, saleEnd: null);

        var res = await SellAtTheDoorAsync(priceId, loungeId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("giờ nhận khách cuối",
            "nhân viên ở quầy phải giải thích được cho khách vì sao không bán nữa");
    }

    [Fact]
    public async Task AnOwnerCannotConfigureTheirWayPastTheCutoff()
    {
        // Trọng tâm của cả thay đổi này. Owner đặt mốc đóng đúng lúc buổi diễn kết thúc — tức xin
        // được bán tới phút cuối. Nếu giờ nhận khách cuối chỉ là giá trị mặc định thì mốc này
        // thắng, và điều khoản bảo vệ khách hàng trở thành thứ tắt được bằng một ô nhập liệu.
        var start = DateTimeOffset.UtcNow.AddHours(-3.5);
        var (priceId, loungeId) = await SeedOngoingShowAsync(
            startedHoursAgo: 3.5, saleEnd: start.AddHours(ShowHours));

        var res = await SellAtTheDoorAsync(priceId, loungeId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "trần cứng, không phải mặc định");
    }

    [Fact]
    public async Task AnOwnerWhoWantsToStopSellingEarlier_StillCan()
    {
        // Trần chỉ chặn theo một chiều. Đóng sớm hơn là quyền của Owner — hết chỗ, hết đồ ăn,
        // hay đơn giản là không muốn nhận thêm khách.
        var (priceId, loungeId) = await SeedOngoingShowAsync(
            startedHoursAgo: 2, saleEnd: DateTimeOffset.UtcNow.AddMinutes(-10));

        var res = await SellAtTheDoorAsync(priceId, loungeId);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task TheCutoffIsATuningKnob_NotANumberBuriedInCode()
    {
        // Ngưỡng bảo vệ khách hàng ở cấp nền tảng thì Admin phải chỉnh được mà không cần deploy.
        // Hạ xuống 15 phút thì ca "còn 30 phút" ở trên chuyển thành bán được.
        var original = await ConfigValueAsync(ConfigKeys.TicketLastEntryMinutes);
        try
        {
            await SetConfigAsync(ConfigKeys.TicketLastEntryMinutes, "15");

            var (priceId, loungeId) = await SeedOngoingShowAsync(startedHoursAgo: 3.5, saleEnd: null);
            var res = await SellAtTheDoorAsync(priceId, loungeId);

            res.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await SetConfigAsync(ConfigKeys.TicketLastEntryMinutes, original);
        }
    }

    [Fact]
    public async Task TheSeededDefaultIsOneHour_TheFigureTheRuleIsSourcedFrom()
    {
        // Ghim lại con số, vì nó là thứ phải bảo vệ được: mặc định của Eventbrite cho vé vào cửa
        // tự do. Đổi nó thì phải đổi cả lập luận đi kèm.
        (await ConfigValueAsync(ConfigKeys.TicketLastEntryMinutes)).Should().Be("60");
    }

    // ---------- con số hiển thị phải là con số được áp ----------

    [Fact]
    public async Task TheCutoffShownToBuyers_IsTheCutoffActuallyEnforced()
    {
        // Trang bán vé ghi một giờ mà khâu thanh toán chặn ở một giờ khác là cách nhanh nhất để
        // mất lòng tin. Đợt bán này không đặt mốc, nên mốc hiển thị phải đúng bằng giờ nhận khách
        // cuối — kết thúc trừ 60 phút.
        var (priceId, loungeId) = await SeedOngoingShowAsync(startedHoursAgo: 1, saleEnd: null);

        int showId;
        DateTimeOffset showEnd;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var price = await db.Set<TicketPrice>().AsNoTracking()
                .Include(p => p.Tier).SingleAsync(p => p.Id == priceId);
            showId = price.Tier.LoungeShowId;
            showEnd = (await db.LoungeShows.AsNoTracking().SingleAsync(s => s.Id == showId))
                .ScheduledEnd!.Value;
        }

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", loungeId)
            .GetAsync($"/api/v1/ticket-tiers?showId={showId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Tier>>>();
        var shown = body!.Data.Single().Prices.Single();

        shown.SaleEnd.Should().BeCloseTo(showEnd.AddMinutes(-60), TimeSpan.FromSeconds(5));
        shown.SaleEndIsAutomatic.Should().BeTrue(
            "màn hình sửa đợt bán phải biết mốc này do hệ thống quyết, không phải Owner nhập");
    }

    [Fact]
    public async Task AnOwnerSetCutoffThatIsEarlier_IsShownAsTheOwnersOwn()
    {
        var ownEnd = DateTimeOffset.UtcNow.AddMinutes(30);
        var (priceId, loungeId) = await SeedOngoingShowAsync(startedHoursAgo: 1, saleEnd: ownEnd);

        int showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            showId = (await db.Set<TicketPrice>().AsNoTracking()
                .Include(p => p.Tier).SingleAsync(p => p.Id == priceId)).Tier.LoungeShowId;
        }

        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", loungeId)
            .GetAsync($"/api/v1/ticket-tiers?showId={showId}");

        var body = await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Tier>>>();
        var shown = body!.Data.Single().Prices.Single();

        shown.SaleEnd.Should().BeCloseTo(ownEnd, TimeSpan.FromSeconds(5));
        shown.SaleEndIsAutomatic.Should().BeFalse();
    }

    // ---------- helpers ----------

    private async Task<string> ConfigValueAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.SystemConfigs.AsNoTracking().SingleAsync(c => c.ConfigKey == key)).ConfigValue;
    }

    private async Task SetConfigAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue = value;
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISystemConfigService>().Invalidate(key);
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Tier(int Id, IReadOnlyList<Price> Prices);
    private sealed record Price(int Id, DateTimeOffset SaleEnd, bool SaleEndIsAutomatic);
}
