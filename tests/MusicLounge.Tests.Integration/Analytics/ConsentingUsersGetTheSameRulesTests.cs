using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-322. Người đã bật đồng ý AI phải nhận danh sách theo cùng bộ luật với mọi người khác.
///
/// Endpoint gợi ý có hai đường: tính ngay trong request, và lấy kết quả đã tính sẵn — đường thứ hai
/// chỉ dành cho người đã bật đồng ý AI. Ba quy tắc chỉ tồn tại ở đường thứ nhất, nên nhóm hợp tác
/// nhất với hệ thống lại là nhóm nhận danh sách kém nhất. Đó là điều ngược hẳn với thứ họ được hứa
/// khi bấm đồng ý.
///
/// 1. <b>Lọc theo thành phố bị bỏ qua.</b> Chú thích của chính query viết "giới hạn theo thành phố,
///    cho cả hai nhóm", nhưng đường tính sẵn không hề nhận tham số đó. Người ở Đà Nẵng lọc theo Đà
///    Nẵng vẫn nhận buổi diễn ở Hà Nội — một lỗi người dùng nhìn thấy ngay.
/// 2. <b>Buổi diễn đã diễn xong vẫn được gợi ý.</b> Hai truy vấn kia đều lọc theo giờ kết thúc thực
///    tế; truy vấn của đường tính sẵn chỉ lọc theo trạng thái, mà trạng thái do job chuyển nên luôn
///    có độ trễ.
/// 3. <b>Buổi diễn mới đăng không bao giờ tới được họ.</b> Tập ứng viên để tính sẵn là 50 buổi đang
///    được quan tâm nhất — buổi chưa ai tương tác thì không nằm trong đó, nên không có dòng gợi ý
///    nào được ghi, nên không ai thấy, nên vĩnh viễn không ai tương tác. Đúng vòng luẩn quẩn
///    MLACP-321 đã phá ở đường tính ngay, nhưng bản vá đó không chạm tới đường này.
///
/// Nguyên tắc chung của cả ba: <b>kết quả tính sẵn là một cách tăng tốc, không phải một bộ luật
/// riêng.</b>
/// </summary>
[Collection("Integration")]
public sealed class ConsentingUsersGetTheSameRulesTests
{
    private readonly ApiFactory _factory;

    public ConsentingUsersGetTheSameRulesTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Rec(int Id, string Name, string LoungeCity);

    private async Task<(int LoungeId, string City)> VenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var city = $"KCity-{Guid.NewGuid():N}"[..20];
        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"KVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Dong Y", Ward = "P1", District = "Q1", City = city }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return (lounge.Id, city);
    }

    private async Task<int> ShowAsync(int loungeId, string name, int? genreId, double daysFromNow)
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
            Status = LoungeShowStatus.Published,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();

        if (genreId is { } g)
        {
            db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = g });
            await db.SaveChangesAsync();
        }
        return show.Id;
    }

    /// <summary>Người đã bấm đồng ý cho phân tích hành vi — nhóm mà cả ba lỗi này nhắm vào.</summary>
    private async Task<int> ConsentingUserAsync(int? favouriteGenreId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"consent-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia Dong Y",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (favouriteGenreId is { } g)
        {
            db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = g });
            await db.SaveChangesAsync();
        }
        return user.Id;
    }

    /// <summary>
    /// Ghi thẳng một dòng kết quả đã tính sẵn còn hạn. Đây chính là thứ job nền sinh ra, nên viết
    /// thẳng vào cho phép kiểm đúng nhánh cache mà không phải chạy cả đường ống ML.
    /// </summary>
    private async Task CacheAsync(int userId, int showId, float score)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Add(new AiRecommendation
        {
            UserId = userId,
            LoungeShowId = showId,
            Algorithm = "content_based",
            ContentScore = score,
            CollabScore = 0f,
            CustomScore = 0f,
            FinalScore = score,
            Reason = "Dựa trên thể loại/mood/không khí bạn yêu thích",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(6)
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<Rec>> RecommendationsAsync(int userId, string? city, int limit = 10)
    {
        var query = city is null ? $"limit={limit}" : $"city={city}&limit={limit}";
        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .GetAsync($"/api/v1/recommendations?{query}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<Rec>>>())!.Data;
    }

    // ---------- 1. lọc theo thành phố ----------

    [Fact]
    public async Task TheCityFilterAppliesToConsentingUsersToo()
    {
        // Người dùng lọc theo thành phố họ đang ở. Kết quả tính sẵn được tính với city = null, nên
        // nếu trả thẳng ra thì họ nhận buổi diễn ở thành phố khác — đi cả trăm cây số để xem.
        var (loungeHere, cityHere) = await VenueAsync();
        var (loungeElsewhere, _) = await VenueAsync();

        var here = await ShowAsync(loungeHere, "O day", SeedHelper.GenreId1, daysFromNow: 20);
        var elsewhere = await ShowAsync(loungeElsewhere, "O tinh khac", SeedHelper.GenreId1, daysFromNow: 12);

        var userId = await ConsentingUserAsync(SeedHelper.GenreId1);
        await CacheAsync(userId, elsewhere, 0.9f);
        await CacheAsync(userId, here, 0.4f);

        var recs = await RecommendationsAsync(userId, cityHere);

        recs.Select(r => r.Id).Should().NotContain(elsewhere,
            "người dùng đã nói rõ họ chỉ quan tâm thành phố này");
        recs.Select(r => r.Id).Should().Contain(here);
        recs.Should().OnlyContain(r => r.LoungeCity == cityHere);
    }

    // ---------- 2. buổi diễn đã xong ----------

    [Fact]
    public async Task AShowThatIsAlreadyOverIsNotRecommended()
    {
        // Trạng thái buổi diễn do job nền chuyển, nên luôn có độ trễ giữa lúc buổi diễn kết thúc
        // thật và lúc cột Status đổi. Hai đường kia đều lọc theo giờ kết thúc thực tế; đường này
        // chỉ tin vào Status, nên trong khoảng trễ đó nó gợi ý một buổi tối đã qua.
        var (loungeId, city) = await VenueAsync();
        var over = await ShowAsync(loungeId, "Da dien xong", SeedHelper.GenreId1, daysFromNow: -3);
        var upcoming = await ShowAsync(loungeId, "Sap dien", SeedHelper.GenreId1, daysFromNow: 20);

        var userId = await ConsentingUserAsync(SeedHelper.GenreId1);
        await CacheAsync(userId, over, 0.9f);
        await CacheAsync(userId, upcoming, 0.4f);

        var recs = await RecommendationsAsync(userId, city);

        recs.Select(r => r.Id).Should().NotContain(over,
            "không ai mua vé vào một buổi tối đã qua");
        recs.Select(r => r.Id).Should().Contain(upcoming);
    }

    // ---------- 3. danh sách bị cụt ----------

    [Fact]
    public async Task WhenTheCacheCannotFillTheListItIsToppedUp()
    {
        // Job nền chỉ ghi lại những buổi có điểm dương, nên người có gu hẹp chỉ được ghi vài dòng.
        // Trả đúng chừng đó là biến một màn hình khám phá mười suất thành một suất — và đúng người
        // đã bật đồng ý AI mới bị, vì chỉ họ mới đi qua đường này.
        var (loungeId, city) = await VenueAsync();
        var cached = await ShowAsync(loungeId, "Co trong cache", SeedHelper.GenreId1, daysFromNow: 20);
        for (var i = 0; i < 4; i++)
            await ShowAsync(loungeId, $"Khong trong cache {i}", SeedHelper.GenreId2, daysFromNow: 21 + i);

        var userId = await ConsentingUserAsync(SeedHelper.GenreId1);
        await CacheAsync(userId, cached, 0.9f);

        var recs = await RecommendationsAsync(userId, city, limit: 5);

        recs.Should().HaveCount(5,
            "cache thiếu thì bù bằng cách tính ngay, không được trả về danh sách cụt");
        recs[0].Id.Should().Be(cached,
            "thứ đã tính sẵn vẫn đứng trước — phần bù chỉ lấp chỗ trống phía sau");
    }

    [Fact]
    public async Task WhatWasComputedInAdvanceStillComesFirst()
    {
        // Nửa còn lại của quy tắc trên: bù thêm không được làm hỏng thứ tự. Kết quả tính sẵn có
        // thêm tín hiệu lọc cộng tác mà đường tính ngay không có, nên nó phải giữ nguyên vị trí đầu.
        var (loungeId, city) = await VenueAsync();
        var a = await ShowAsync(loungeId, "Tinh san A", SeedHelper.GenreId1, daysFromNow: 40);
        var b = await ShowAsync(loungeId, "Tinh san B", SeedHelper.GenreId1, daysFromNow: 41);
        await ShowAsync(loungeId, "Bu them", SeedHelper.GenreId1, daysFromNow: 11);

        var userId = await ConsentingUserAsync(SeedHelper.GenreId1);
        await CacheAsync(userId, a, 0.9f);
        await CacheAsync(userId, b, 0.7f);

        var ids = (await RecommendationsAsync(userId, city, limit: 3)).Select(r => r.Id).ToList();

        ids.Take(2).Should().Equal([a, b],
            "hai suất đầu phải là kết quả đã tính sẵn, đúng thứ tự điểm của chúng");
    }

    // ---------- 4. buổi diễn mới có tới được người đã đồng ý không ----------

    /// <summary>Gemini luôn trả null trong test — lý do gợi ý là phần làm đẹp, không phải phép tính.</summary>
    private sealed class NoTextGeneration : IAiTextGenerationService
    {
        public Task<string?> GenerateJsonAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task ANewlyPublishedShowCanStillReachSomeoneWhoOptedIntoAi()
    {
        // Đây là lỗi ở tận gốc, không sửa được ở khâu trả kết quả: buổi diễn mới không nằm trong 50
        // buổi đang được quan tâm nhất, nên job nền không bao giờ ghi một dòng gợi ý nào cho nó.
        //
        // Dựng đúng tình huống: 55 buổi diễn gần hơn lấp kín 50 chỗ của tập ứng viên, rồi một buổi
        // mới toanh diễn rất xa. Không đủ 50 buổi lấp chỗ thì buổi mới vốn đã lọt vào và bài kiểm
        // tra không kiểm được gì — đúng cái bẫy đã mắc ở MLACP-320 và MLACP-321.
        var (loungeId, _) = await VenueAsync();
        await BulkShowsAsync(loungeId, 55, SeedHelper.GenreId2, firstDayOffset: 5);
        var brandNew = await ShowAsync(loungeId, "Vua dang len", SeedHelper.GenreId1, daysFromNow: 300);

        var userId = await ConsentingUserAsync(SeedHelper.GenreId1);

        var realType = typeof(ApplicationDbContext).Assembly
            .GetType("MusicLounge.Infrastructure.Services.MLNetRecommendationService")
            ?? throw new InvalidOperationException(
                "Không tìm thấy MLNetRecommendationService — lớp đã bị đổi tên? " +
                "Nếu đúng vậy thì bài kiểm tra này đang âm thầm không kiểm gì cả.");

        var withRealAi = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAIRecommendationService>();
                services.AddScoped(typeof(IAIRecommendationService), realType);
                services.RemoveAll<IAiTextGenerationService>();
                services.AddSingleton<IAiTextGenerationService, NoTextGeneration>();
            }));

        using (var scope = withRealAi.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAIRecommendationService>()
                .TriggerRecommendationRefreshAsync(userId);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<AiRecommendation>()
                .AnyAsync(r => r.UserId == userId && r.LoungeShowId == brandNew))
                .Should().BeTrue(
                    "buổi diễn mới hợp gu phải được chấm điểm, nếu không nó không bao giờ có cơ hội " +
                    "được ai quan tâm — và không bao giờ đủ quan tâm để lọt vào tập ứng viên");
        }
    }

    private async Task BulkShowsAsync(int loungeId, int count, int genreId, int firstDayOffset)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var shows = new List<LoungeShow>();
        for (var i = 0; i < count; i++)
        {
            var start = DateTimeOffset.UtcNow.AddDays(firstDayOffset + i * 0.5);
            shows.Add(new LoungeShow
            {
                LoungeId = loungeId,
                Name = $"Gan-{i}-{Guid.NewGuid():N}"[..24],
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            });
        }
        db.LoungeShows.AddRange(shows);
        await db.SaveChangesAsync();

        db.AddRange(shows.Select(s => new LoungeShowGenre { LoungeShowId = s.Id, GenreId = genreId }));
        await db.SaveChangesAsync();
    }
}
