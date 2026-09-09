using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Analytics;

/// <summary>
/// MLACP-328. Không đặt lịch một việc nền mà hệ thống đã biết trước là vô ích.
///
/// Khi người dùng đã bật đồng ý AI mà chưa có kết quả tính sẵn còn hạn, handler đặt lịch tính nền
/// cho lần sau. Nhưng chính công việc đó có một nhánh thoát sớm: dưới 5 dòng nhật ký hành vi, chưa
/// khai sở thích nào, không theo dõi phòng trà nào — thì nó return luôn, không ghi dòng nào.
///
/// Hai điều đó cộng lại thành một vòng không có điểm dừng:
///
/// <code>
/// không ghi dòng nào  ->  cache vẫn rỗng
/// cache rỗng          ->  request sau lại đặt lịch tiếp
/// job lại thoát sớm   ->  lại không ghi gì
/// </code>
///
/// Mỗi lần mở màn hình gợi ý là thêm một việc vào hàng đợi, mãi mãi. Và nó rơi đúng vào nhóm người
/// mới: đã bấm đồng ý cho phân tích hành vi nhưng chưa kịp khai sở thích. <b>Người càng hợp tác thì
/// càng tạo rác.</b>
///
/// <b>Cách đo.</b> Thay <see cref="IBackgroundJobService"/> bằng một bản ghi lại lời gọi, nên bài
/// kiểm tra đo đúng thứ handler làm. Bản đầu tiên tôi viết đo qua hàng đợi Hangfire và <b>không bắt
/// được gì</b> — cả bốn trường hợp đều ra 0, tức bài "phải bằng 0" xanh mà chẳng kiểm gì cả. Ba bài
/// còn lại đỏ là thứ đã lộ ra chuyện đó.
/// </summary>
[Collection("Integration")]
public sealed class RefreshIsOnlyQueuedWhenItCanHelpTests
{
    private readonly ApiFactory _factory;

    public RefreshIsOnlyQueuedWhenItCanHelpTests(ApiFactory factory) => _factory = factory;

    /// <summary>Ghi lại đúng một điều: đã đặt lịch tính lại gợi ý cho ai, bao nhiêu lần.</summary>
    private sealed class RecordingJobService : IBackgroundJobService
    {
        public List<int> RefreshesQueued { get; } = [];

        public void EnqueueRecommendationRefresh(int userId) => RefreshesQueued.Add(userId);

        public void EnqueueLogUserBehaviour(int userId, int showId, BehaviourAction action) { }
        public void EnqueueLivestreamCheckIn(int userId, int showId) { }
        public void EnqueueLivestreamReconnectTimeout(int livestreamId, DateTimeOffset disconnectedAt, TimeSpan delay) { }
        public void EnqueueFcmNotification(int userId, string title, string body, string? referenceType = null, string? referenceId = null) { }
        public void EnqueuePasswordResetEmail(string toEmail, string toName, string resetLink) { }
        public void EnqueueEmailVerificationCode(string toEmail, string toName, string code) { }
        public void EnqueuePhoneVerificationCode(string toPhone, string code) { }
        public void EnqueueModerationAiScoring(int moderationId) { }
        public void EnqueueStitchVenueTourScene(int attemptId, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name) { }
        public void TriggerRecurringJobNow(string recurringJobId) { }
        public IReadOnlyList<string> GetRecurringJobIds() => [];
    }

    /// <summary>
    /// Gọi endpoint gợi ý <paramref name="times"/> lần và trả về số lần handler đặt lịch tính lại
    /// cho đúng người đó.
    /// </summary>
    private async Task<int> RefreshesQueuedByAsync(int userId, int times = 1)
    {
        var recorder = new RecordingJobService();

        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackgroundJobService>();
                services.AddSingleton<IBackgroundJobService>(recorder);
            }));

        // WithWebHostBuilder trả về WebApplicationFactory chứ không phải ApiFactory, nên phải tự
        // gắn header xác thực thay vì gọi CreateAuthenticatedClient.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "Audience");

        for (var i = 0; i < times; i++)
        {
            var res = await client.GetAsync("/api/v1/recommendations?limit=5");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return recorder.RefreshesQueued.Count(id => id == userId);
    }

    private async Task<int> ConsentingUserAsync(bool declaresTaste)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            Email = $"queue-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia Dong Y",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (declaresTaste)
        {
            db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = SeedHelper.GenreId1 });
            await db.SaveChangesAsync();
        }

        return user.Id;
    }

    [Fact]
    public async Task ANewAccountWithNothingToLearnFromQueuesNothing()
    {
        // Đã bật đồng ý AI nhưng chưa khai gì, chưa theo dõi ai, chưa có hành vi nào được ghi. Công
        // việc nền chạy lên sẽ không tính ra được gì — nên không được đặt lịch ngay từ đầu. Ba lần
        // gọi để thấy rõ nó lặp chứ không phải chỉ thừa một lần.
        var userId = await ConsentingUserAsync(declaresTaste: false);

        (await RefreshesQueuedByAsync(userId, times: 3)).Should().Be(0,
            "hệ thống đã biết trước việc đó không tính ra được gì, đặt lịch bao nhiêu lần cũng vậy");
    }

    [Fact]
    public async Task AnAccountThatDeclaredATasteStillGetsItsRefreshQueued()
    {
        // Nửa còn lại, và là nửa dễ làm quá tay: người CÓ dữ liệu để tính thì vẫn phải được đặt
        // lịch. Chặn nhầm nhóm này là làm hỏng hẳn đường tính sẵn — họ sẽ không bao giờ nhận được
        // kết quả có lọc cộng tác và lời giải thích do AI viết.
        var userId = await ConsentingUserAsync(declaresTaste: true);

        (await RefreshesQueuedByAsync(userId)).Should().Be(1,
            "người đã khai sở thích thì công việc nền có thứ để tính");
    }

    [Fact]
    public async Task AnAccountWithEnoughBehaviourLoggedStillGetsItsRefreshQueued()
    {
        // Người chưa khai sở thích nhưng đã có đủ nhật ký hành vi thì đi nhánh hybrid — cũng phải
        // được đặt lịch. Nếu chỉ nhìn "đã khai sở thích chưa" thì sẽ chặn nhầm đúng nhóm này.
        var userId = await ConsentingUserAsync(declaresTaste: false);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var i = 0; i < 5; i++)
            {
                db.Add(new UserBehaviourLog
                {
                    UserId = userId,
                    LoungeShowId = SeedHelper.OfflineShowId,
                    Action = BehaviourAction.ViewEvent,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-i)
                });
            }
            await db.SaveChangesAsync();
        }

        (await RefreshesQueuedByAsync(userId)).Should().Be(1,
            "đủ nhật ký hành vi thì nhánh hybrid có thứ để huấn luyện");
    }

    [Fact]
    public async Task AnAccountThatOnlyFollowsAVenueStillGetsItsRefreshQueued()
    {
        // Theo dõi một phòng trà cũng là một tín hiệu đủ để tính nội dung — công việc nền dùng nó
        // để cộng điểm thưởng. Chặn nhóm này là bỏ sót một nhánh thật.
        var userId = await ConsentingUserAsync(declaresTaste: false);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new Follow { UserId = userId, LoungeId = SeedHelper.LoungeId });
            await db.SaveChangesAsync();
        }

        (await RefreshesQueuedByAsync(userId)).Should().Be(1,
            "theo dõi phòng trà là một tín hiệu công việc nền dùng được");
    }
}
