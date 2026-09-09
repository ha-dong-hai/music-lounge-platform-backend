using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
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
/// MLACP-315. Chạy hệ gợi ý THẬT — <c>MLNetRecommendationService</c> — chứ không phải bản giả lập.
///
/// <b>Vì sao bộ test này phải tồn tại.</b> <c>ApiFactory</c> thay <c>IAIRecommendationService</c>
/// bằng <c>FakeAiService</c> cho toàn bộ test, nên hệ gợi ý thật — thứ được mô tả là tính năng AI
/// của đồ án, gồm ML.NET Matrix Factorization, chấm điểm nội dung theo Jaccard, và tiêu chí riêng
/// của từng phòng trà — chưa từng được chạy dưới test lấy một lần.
///
/// Một tính năng AI mà không có gì chứng minh nó chạy được thì lúc bảo vệ không có gì để nói. Đó là
/// khoảng trống nguy hiểm nhất trong cả hệ, vì mọi test khác đều xanh mà vẫn không đụng tới nó.
///
/// Bộ test này đăng ký lại lớp thật (lấy qua reflection vì lớp đó là <c>internal</c>) và gọi thẳng
/// vào nó, nên nếu đường ống ML gãy ở bất kỳ đâu thì ở đây sẽ đỏ.
/// </summary>
[Collection("Integration")]
public sealed class RealRecommendationPipelineTests
{
    private readonly ApiFactory _factory;

    public RealRecommendationPipelineTests(ApiFactory factory) => _factory = factory;

    /// <summary>Gemini luôn trả null trong test — dịch vụ đó vốn fail-open, và lý do gợi ý là phần
    /// làm đẹp chứ không phải phần tính toán.</summary>
    private sealed class NoTextGeneration : IAiTextGenerationService
    {
        public Task<string?> GenerateJsonAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Dựng một host dùng hệ gợi ý thật thay cho bản giả lập. Lớp thật là <c>internal</c> nên lấy
    /// bằng reflection — cách này không phải đụng vào file dự án để mở <c>InternalsVisibleTo</c>.
    /// </summary>
    private WebApplicationFactoryScope RealAiScope()
    {
        var realType = typeof(ApplicationDbContext).Assembly
            .GetType("MusicLounge.Infrastructure.Services.MLNetRecommendationService")
            ?? throw new InvalidOperationException(
                "Không tìm thấy MLNetRecommendationService — lớp đã bị đổi tên? " +
                "Nếu đúng vậy thì bộ test này đang âm thầm không kiểm gì cả.");

        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAIRecommendationService>();
                services.AddScoped(typeof(IAIRecommendationService), realType);
                services.RemoveAll<IAiTextGenerationService>();
                services.AddSingleton<IAiTextGenerationService, NoTextGeneration>();
            }));

        return new WebApplicationFactoryScope(factory);
    }

    private sealed class WebApplicationFactoryScope(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) : IDisposable
    {
        public IServiceScope Scope { get; } = factory.Services.CreateScope();
        public void Dispose() => Scope.Dispose();
    }

    /// <summary>
    /// Một khán giả đã đồng ý cho dùng dữ liệu, có gu nhạc rõ ràng, và một loạt buổi diễn trong đó
    /// có buổi khớp gu và buổi không khớp.
    /// </summary>
    private async Task<(int UserId, int MatchingShowId, int UnrelatedShowId)> SeedTasteAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            Email = $"ai-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia Co Gu",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = SeedHelper.GenreId1 });
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeVenue
        {
            OwnerId = SeedHelper.OwnerId,
            Name = $"AiVenue-{Guid.NewGuid():N}",
            Description = "Integration test venue",
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Goi Y", Ward = "P1", District = "Q1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        async Task<int> MakeShow(string name, int? genreId)
        {
            var start = DateTimeOffset.UtcNow.AddDays(12);
            var show = new LoungeShow
            {
                LoungeId = lounge.Id,
                Name = name,
                Description = "Integration test show",
                Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = start,
                ScheduledEnd = start.AddHours(3)
            };
            db.LoungeShows.Add(show);
            await db.SaveChangesAsync();

            if (genreId is int g)
            {
                db.Add(new LoungeShowGenre { LoungeShowId = show.Id, GenreId = g });
                await db.SaveChangesAsync();
            }

            // Hệ gợi ý chỉ lấy 50 buổi diễn đang được quan tâm nhất làm tập ứng viên, rồi mới chấm
            // điểm hợp gu trên tập đó. Trong một lần chạy có hàng trăm buổi diễn do các test khác
            // tạo ra, buổi diễn không có tín hiệu nào sẽ không lọt vào 50 chỗ đó và không bao giờ
            // được chấm — nên phải bán vé cho nó trước.
            //
            // Bán bằng nhau cho cả hai buổi để bước này không tự tạo ra chênh lệch: thứ được kiểm
            // là điểm hợp gu, không phải mức độ được quan tâm.
            var tier = new TicketTier
            {
                LoungeShowId = show.Id, Name = "Thuong",
                AccessType = AccessType.Physical, TotalCapacity = 200
            };
            db.Add(tier);
            await db.SaveChangesAsync();

            var price = new TicketPrice
            {
                TierId = tier.Id, Name = "Chuan", Price = 200_000m, Quota = 200, IsActive = true,
                SaleStart = DateTimeOffset.UtcNow.AddDays(-10),
                SaleEnd = DateTimeOffset.UtcNow.AddDays(5),
                PurchaseChannel = PurchaseChannel.Online
            };
            db.Add(price);
            await db.SaveChangesAsync();

            for (var i = 0; i < 12; i++)
            {
                db.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    ShowId = show.Id,
                    TierId = tier.Id,
                    PriceId = price.Id,
                    BuyerId = SeedHelper.AudienceId,
                    Status = TicketStatus.Confirmed,
                    QrCode = $"QR-{Guid.NewGuid():N}",
                    PurchaseChannel = PurchaseChannel.Online,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                });
            }
            await db.SaveChangesAsync();

            return show.Id;
        }

        var matching = await MakeShow($"Dung gu-{Guid.NewGuid():N}", SeedHelper.GenreId1);
        var unrelated = await MakeShow($"Khac gu-{Guid.NewGuid():N}", SeedHelper.GenreId2);

        return (user.Id, matching, unrelated);
    }

    [Fact]
    public async Task TheRealEngineRuns_AndWritesRecommendations()
    {
        // Điều tối thiểu phải chứng minh được: đường ống AI thật chạy hết mà không gãy, và có kết
        // quả ghi ra. Trước bộ test này, không có gì trong repo nói được điều đó.
        var (userId, _, _) = await SeedTasteAsync();

        using var host = RealAiScope();
        var ai = host.Scope.ServiceProvider.GetRequiredService<IAIRecommendationService>();
        ai.GetType().Name.Should().Be("MLNetRecommendationService",
            "nếu ở đây vẫn là bản giả lập thì bộ test này không kiểm gì cả");

        await ai.TriggerRecommendationRefreshAsync(userId);

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var written = await db.Set<AiRecommendation>().AsNoTracking()
            .Where(r => r.UserId == userId).ToListAsync();

        written.Should().NotBeEmpty("hệ gợi ý phải cho ra kết quả, không phải im lặng");
        written.Should().OnlyContain(r => r.ExpiresAt > r.CreatedAt);
    }

    [Fact]
    public async Task ItPrefersWhatMatchesTheListenersTaste()
    {
        // Đây là điều tính năng hứa hẹn với người dùng. Nếu buổi diễn đúng gu không được chấm cao
        // hơn buổi không liên quan thì phần chấm điểm nội dung không hoạt động, dù nó vẫn chạy.
        var (userId, matching, unrelated) = await SeedTasteAsync();

        using var host = RealAiScope();
        await host.Scope.ServiceProvider.GetRequiredService<IAIRecommendationService>()
            .TriggerRecommendationRefreshAsync(userId);

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recs = await db.Set<AiRecommendation>().AsNoTracking()
            .Where(r => r.UserId == userId).ToListAsync();

        var matchScore = recs.SingleOrDefault(r => r.LoungeShowId == matching)?.ContentScore;
        var otherScore = recs.SingleOrDefault(r => r.LoungeShowId == unrelated)?.ContentScore ?? 0;

        matchScore.Should().NotBeNull("buổi diễn đúng gu phải nằm trong kết quả");
        matchScore.Should().BeGreaterThan(otherScore);
    }

    [Fact]
    public async Task WithTooLittleHistory_ItSkipsCollaborativeFilteringInsteadOfGuessing()
    {
        // Matrix Factorization cần vài chục lượt đánh giá trải trên nhiều người và nhiều buổi diễn
        // mới học được gì có nghĩa. Dữ liệu ít hơn thế thì nó phải BỎ QUA phần đó và dùng điểm nội
        // dung — chứ không phải sinh ra một con số trông như đã học được điều gì.
        //
        // Đây đúng là tình trạng của hệ thống hiện nay, nên nhánh này mới là nhánh chạy thật.
        var (userId, _, _) = await SeedTasteAsync();

        using var host = RealAiScope();
        await host.Scope.ServiceProvider.GetRequiredService<IAIRecommendationService>()
            .TriggerRecommendationRefreshAsync(userId);

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recs = await db.Set<AiRecommendation>().AsNoTracking()
            .Where(r => r.UserId == userId).ToListAsync();

        recs.Should().NotBeEmpty();
        recs.Should().OnlyContain(r => r.CollabScore == 0,
            "chưa đủ dữ liệu thì phần lọc cộng tác phải bằng 0 một cách có chủ đích");
        recs.Should().Contain(r => r.FinalScore > 0,
            "và kết quả vẫn phải dùng được nhờ phần chấm điểm nội dung");
    }

    [Fact]
    public async Task EachRecommendationSaysWhichTierProducedIt_AndItsScoresMatchThatTier()
    {
        // Hệ gợi ý có hai công thức, không phải một, và trường Algorithm ghi lại cái nào đã chạy:
        //
        //   content_based — khi chưa đủ dữ liệu hành vi để chạy lọc cộng tác. Điểm cuối chính là
        //                   điểm nội dung. KHÔNG nhân 0.5, vì nhân vào sẽ hạ thấp mọi gợi ý một
        //                   cách vô nghĩa khi đó là tín hiệu duy nhất đang có.
        //   hybrid        — đúng công thức trong tài liệu: content*0.5 + collab*0.3 + custom*0.2.
        //
        // Cả hai đều được cộng thêm 0.15 nếu người dùng đang theo dõi phòng trà đó.
        //
        // Ghim lại vì đây là kiến trúc 3 tầng mà tài liệu đồ án mô tả, và vì trường Algorithm là
        // thứ duy nhất cho biết một gợi ý được sinh ra bằng cách nào — nếu nó ghi sai thì không ai
        // giải thích được kết quả.
        var (userId, _, _) = await SeedTasteAsync();

        using var host = RealAiScope();
        await host.Scope.ServiceProvider.GetRequiredService<IAIRecommendationService>()
            .TriggerRecommendationRefreshAsync(userId);

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recs = await db.Set<AiRecommendation>().AsNoTracking()
            .Where(r => r.UserId == userId).ToListAsync();

        recs.Should().NotBeEmpty();
        recs.Should().OnlyContain(r => r.Algorithm == "content_based" || r.Algorithm == "hybrid",
            "mỗi gợi ý phải nói được nó do tầng nào sinh ra");

        const float followBoost = 0.15f;

        foreach (var r in recs)
        {
            var expected = r.Algorithm == "hybrid"
                ? r.ContentScore * 0.5f + r.CollabScore * 0.3f + r.CustomScore * 0.2f
                : r.ContentScore;

            r.FinalScore.Should().BeInRange(expected - 0.001f, expected + followBoost + 0.001f,
                $"điểm cuối của tầng {r.Algorithm} phải khớp công thức của chính tầng đó");
        }
    }
}
