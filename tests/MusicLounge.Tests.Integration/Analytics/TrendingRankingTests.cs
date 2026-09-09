using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-315, phần nối với dữ liệu thật.
///
/// Cách chấm điểm đã được ghim chặt ở <c>TrendingScorerTests</c>, không lặp lại ở đây. Việc của bộ
/// test này là chứng minh repository gom ĐÚNG tín hiệu để đưa vào chấm — và điểm dễ sai nhất là bỏ
/// sót nguồn tín hiệu, đúng như bảng cũ đã bỏ sót toàn bộ lượt lưu vào danh sách quan tâm.
///
/// Mỗi test dựng một thành phố riêng và gọi bảng xếp hạng có lọc theo thành phố đó, nên dữ liệu của
/// các test khác không lọt vào. Không có cách cô lập này thì mọi khẳng định về thứ tự đều mong manh.
/// </summary>
[Collection("Integration")]
public sealed class TrendingRankingTests
{
    private readonly ApiFactory _factory;

    public TrendingRankingTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record ShowItem(int Id, string Name);

    private async Task<(int LoungeId, string City)> VenueInItsOwnCityAsync()
    {
        var city = $"City-{Guid.NewGuid():N}"[..20];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"TrendVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Xu Huong", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name, double daysFromNow = 10,
        LoungeShowStatus status = LoungeShowStatus.Published)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(daysFromNow);
        var show = new LoungeShow
        {
            LoungeId = loungeId,
            Name = name,
            Description = "Integration test show",
            Format = LoungeShowFormat.Offline,
            Status = status,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    /// <summary>Tạo <paramref name="people"/> người dùng khác nhau cùng thực hiện một hành động.</summary>
    private async Task BehaviourAsync(int showId, BehaviourAction action, int people, double hoursAgo = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < people; i++)
        {
            var user = new User
            {
                Email = $"trend-{Guid.NewGuid():N}@test.com",
                FullName = "Khan Gia",
                Role = UserRole.Audience,
                AuthProvider = "local",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.Add(new UserBehaviourLog
            {
                UserId = user.Id,
                LoungeShowId = showId,
                Action = action,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo)
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task WishlistAsync(int showId, int people, double hoursAgo = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < people; i++)
        {
            var user = new User
            {
                Email = $"wish-{Guid.NewGuid():N}@test.com",
                FullName = "Khan Gia",
                Role = UserRole.Audience,
                AuthProvider = "local",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.Add(new ShowWishlist
            {
                UserId = user.Id,
                LoungeShowId = showId,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo)
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Bán <paramref name="count"/> vé thật cho buổi diễn. Đây là tín hiệu quan trọng nhất của
    /// bảng xếp hạng, và cũng là tín hiệu duy nhất không phụ thuộc vào việc người dùng có bật
    /// AiConsent hay không.
    /// </summary>
    private async Task SellTicketsAsync(int showId, int count, double hoursAgo = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tier = new TicketTier
        {
            LoungeShowId = showId, Name = "Thuong",
            AccessType = AccessType.Physical, TotalCapacity = 500
        };
        db.Add(tier);
        await db.SaveChangesAsync();

        var price = new TicketPrice
        {
            TierId = tier.Id, Name = "Chuan", Price = 200_000m, Quota = 500, IsActive = true,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-30),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(1),
            PurchaseChannel = PurchaseChannel.Online
        };
        db.Add(price);
        await db.SaveChangesAsync();

        for (var i = 0; i < count; i++)
        {
            db.Add(new Ticket
            {
                // NEWSEQUENTIALID() khong co tren provider SQLite dung trong test.
                Id = Guid.NewGuid(),
                ShowId = showId,
                TierId = tier.Id,
                PriceId = price.Id,
                BuyerId = SeedHelper.AudienceId,
                Status = TicketStatus.Confirmed,
                QrCode = $"QR-{Guid.NewGuid():N}",
                PurchaseChannel = PurchaseChannel.Online,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo)
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<ShowItem>> TrendingAsync(string city)
    {
        var res = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounge-shows/trending?limit=50&city={city}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<ShowItem>>>())!.Data;
    }

    // ---------- điều bảng cũ xếp ngược ----------

    [Fact]
    public async Task TicketsSoldBeatPageViews_EndToEnd()
    {
        // Bảng cũ đếm mọi bản ghi như nhau, nên 40 lượt xem thắng 8 vé bán được. Một khán giả mở
        // trang chủ sẽ thấy buổi diễn được liếc qua nhiều nhất, không phải buổi diễn đang bán chạy.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var sellsTickets = await ShowAsync(loungeId, "Ban chay");
        var justBrowsed = await ShowAsync(loungeId, "Chi duoc liec qua");

        await SellTicketsAsync(sellsTickets, count: 8);
        await BehaviourAsync(justBrowsed, BehaviourAction.ViewEvent, people: 40);

        var ranked = await TrendingAsync(city);

        ranked.Select(s => s.Id).Should().ContainInOrder(sellsTickets, justBrowsed);
    }

    [Fact]
    public async Task SavingAShowCounts_WhichItNeverDidBefore()
    {
        // Lượt lưu vào danh sách quan tâm nằm ở bảng riêng, không phải một hành động trong nhật ký
        // hành vi — nên bảng cũ, vốn chỉ đọc nhật ký, bỏ sót hoàn toàn. Đó là một trong những tín
        // hiệu mạnh nhất: người ta chỉ lưu thứ mình định quay lại.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var saved = await ShowAsync(loungeId, "Duoc luu lai");
        var browsed = await ShowAsync(loungeId, "Chi duoc xem");

        await WishlistAsync(saved, people: 10);
        await BehaviourAsync(browsed, BehaviourAction.ViewEvent, people: 20);

        var ranked = await TrendingAsync(city);

        ranked.Select(s => s.Id).Should().ContainInOrder(saved, browsed);
    }

    [Fact]
    public async Task LastWeeksHitLosesToTodaysRiser()
    {
        // Khác biệt giữa "được chú ý nhiều" và "đang được chú ý". Cả hai đều nằm trong cửa sổ 7
        // ngày của bảng cũ nên nó xếp buổi đã nguội lên trên nhờ có nhiều lượt hơn.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var lastWeek = await ShowAsync(loungeId, "Nong tuan truoc");
        var rightNow = await ShowAsync(loungeId, "Dang len");

        await BehaviourAsync(lastWeek, BehaviourAction.ViewEvent, people: 30, hoursAgo: 6 * 24);
        await BehaviourAsync(rightNow, BehaviourAction.ViewEvent, people: 20, hoursAgo: 1);

        var ranked = await TrendingAsync(city);

        ranked.Select(s => s.Id).Should().ContainInOrder(rightNow, lastWeek);
    }

    // ---------- những thứ không được xuất hiện ----------

    [Fact]
    public async Task AShowThatHasAlreadyFinished_DoesNotAppear()
    {
        // Buổi diễn đã qua nhưng chưa kịp được job chuyển trạng thái vẫn là Published. Không ai
        // mua được vé vào một buổi tối đã kết thúc, nên nó không thuộc về bảng "đang được quan tâm"
        // — kể cả khi nó vừa có rất nhiều lượt xem.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var finished = await ShowAsync(loungeId, "Da dien xong", daysFromNow: -3);
        var upcoming = await ShowAsync(loungeId, "Sap dien");

        await SellTicketsAsync(finished, count: 50);

        var ranked = await TrendingAsync(city);

        ranked.Select(s => s.Id).Should().NotContain(finished);
        ranked.Select(s => s.Id).Should().Contain(upcoming);
    }

    // ---------- khi chưa có tín hiệu nào ----------

    [Fact]
    public async Task WithNoEngagementAtAll_TheSoonestShowComesFirst()
    {
        // Đây là tình trạng thật của một nền tảng mới, nên nó phải cho ra thứ tự có ích chứ không
        // phải thứ tự ngẫu nhiên. Bảng cũ khi hoà điểm xếp buổi diễn XA NHẤT lên đầu — tức người
        // đang tìm chỗ đi tối nay nhận được danh sách các buổi diễn tháng sau.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var farAway = await ShowAsync(loungeId, "Con lau moi dien", daysFromNow: 40);
        var soon = await ShowAsync(loungeId, "Sap dien roi", daysFromNow: 2);

        var ranked = await TrendingAsync(city);

        ranked.Select(s => s.Id).Should().ContainInOrder(soon, farAway);
    }

    [Fact]
    public async Task TheRankingWorksWithNoBehaviourLogsAtAll()
    {
        // Đây là tình trạng THẬT của hệ thống, không phải một trường hợp biên hiếm gặp. Nhật ký
        // hành vi chỉ được ghi cho người đã bật AiConsent, mà mặc định là tắt — nên trên production
        // bảng này gần như không có bản ghi hành vi nào để đọc.
        //
        // Bảng cũ chỉ đọc nhật ký hành vi, nên trong đúng tình huống này nó không xếp được gì và
        // trả về danh sách theo thứ tự tuỳ ý. Vé đã bán và lượt lưu quan tâm luôn tồn tại, và
        // chúng là hai tín hiệu mạnh nhất.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var sold = await ShowAsync(loungeId, "Ban duoc ve");
        var saved = await ShowAsync(loungeId, "Duoc luu lai");
        var nothing = await ShowAsync(loungeId, "Chua ai quan tam");

        await SellTicketsAsync(sold, count: 3);
        await WishlistAsync(saved, people: 4);

        var ranked = await TrendingAsync(city);

        // 3 vé bán được (3×10) hơn 4 lượt lưu (4×3), và cả hai hơn buổi chưa ai quan tâm.
        ranked.Select(s => s.Id).Should().ContainInOrder([sold, saved, nothing]);
    }

    [Fact]
    public async Task OnePersonRefreshingDoesNotOutrankRealInterest_EndToEnd()
    {
        // Chống thổi số, kiểm qua đúng đường đi thật: cùng một người xem lại nhiều lần vẫn thua
        // một nhóm nhỏ người quan tâm thật.
        var (loungeId, city) = await VenueInItsOwnCityAsync();
        var refreshed = await ShowAsync(loungeId, "Duoc bam lai nhieu lan");
        var genuine = await ShowAsync(loungeId, "Duoc nhieu nguoi xem");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var i = 0; i < 40; i++)
            {
                db.Add(new UserBehaviourLog
                {
                    UserId = SeedHelper.AudienceId,
                    LoungeShowId = refreshed,
                    Action = BehaviourAction.ViewEvent,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
                });
            }
            await db.SaveChangesAsync();
        }

        await BehaviourAsync(genuine, BehaviourAction.ViewEvent, people: 5, hoursAgo: 1);

        var ranked = await TrendingAsync(city);

        ranked.Select(s => s.Id).Should().ContainInOrder(genuine, refreshed);
    }
}
