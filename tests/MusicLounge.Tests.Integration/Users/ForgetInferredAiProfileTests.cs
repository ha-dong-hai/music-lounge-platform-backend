using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// MLACP-318. Rút lại sự đồng ý và xoá tài khoản phải thật sự xoá những gì AI đã suy ra.
///
/// Ba lỗ hổng, đều đã kiểm chứng bằng code trước khi sửa:
///
/// 1. Tắt <c>AiConsent</c> chỉ làm hệ thống NGƯNG GHI hành vi mới. Những gì đã suy ra vẫn nằm
///    nguyên — và điểm số hành vi của người đó vẫn nằm trong tập huấn luyện của mô hình lọc cộng
///    tác phục vụ NGƯỜI KHÁC, vì truy vấn huấn luyện đọc toàn bộ bảng không lọc consent.
/// 2. Đường xoá dữ liệu cá nhân cố ý bỏ lại <c>UserEventScore</c>, với chú thích nói nó "không chứa
///    nội dung định danh". Sai: bảng khoá theo (người, buổi diễn) và cột Breakdown lưu JSON ghi rõ
///    người này đã dự buổi nào, chấm mấy sao, có donate hay không.
/// 3. Job tính lại điểm dựng hồ sơ cho CẢ người chưa bao giờ đồng ý, vì bốn trong sáu nguồn của nó
///    đọc thẳng từ giao dịch chứ không qua nhật ký hành vi.
///
/// Ranh giới xuyên suốt: xoá cái hệ thống SUY RA, giữ nguyên cái người dùng TỰ KHAI. Sở thích họ
/// chọn ở onboarding là dữ liệu của họ, không liên quan tới việc có cho phân tích hành vi hay không.
/// </summary>
[Collection("Integration")]
public sealed class ForgetInferredAiProfileTests
{
    private readonly ApiFactory _factory;

    public ForgetInferredAiProfileTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// Một người đã bật đồng ý AI, đã khai sở thích, và hệ thống đã suy ra đủ thứ về họ.
    /// </summary>
    private async Task<int> UserWithBothDeclaredAndInferredDataAsync(bool consent = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            Email = $"forget-{Guid.NewGuid():N}@test.com",
            FullName = "Khan Gia",
            Role = UserRole.Audience,
            AuthProvider = "local",
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            AiConsent = consent
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // TỰ KHAI — phải sống sót.
        db.Add(new UserFavouriteGenre { UserId = user.Id, GenreId = SeedHelper.GenreId1 });
        db.Add(new UserFavouriteMood { UserId = user.Id, MoodId = SeedHelper.MoodId1 });

        // SUY RA — phải bị xoá.
        db.Add(new UserEventScore
        {
            UserId = user.Id,
            ShowId = SeedHelper.OfflineShowId,
            Score = 4.5m,
            Breakdown = """{"attended":true,"rating":5,"donated":true,"wishlist":true,"view":9}""",
            ComputedAt = DateTimeOffset.UtcNow
        });
        db.Add(new AiRecommendation
        {
            UserId = user.Id,
            LoungeShowId = SeedHelper.OfflineShowId,
            FinalScore = 0.8f,
            Algorithm = "hybrid",
            Reason = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(6)
        });
        await db.SaveChangesAsync();

        return user.Id;
    }

    private async Task<(int Scores, int Recommendations, int Genres, int Moods)> CountAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (
            await db.Set<UserEventScore>().CountAsync(s => s.UserId == userId),
            await db.Set<AiRecommendation>().CountAsync(r => r.UserId == userId),
            await db.Set<UserFavouriteGenre>().CountAsync(g => g.UserId == userId),
            await db.Set<UserFavouriteMood>().CountAsync(m => m.UserId == userId));
    }

    // ---------- rút lại sự đồng ý ----------

    [Fact]
    public async Task WithdrawingConsent_ErasesWhatTheSystemInferred()
    {
        // Trước đây tắt ô này chỉ làm hệ thống ngưng ghi hành vi MỚI. Hồ sơ cũ nằm nguyên, và vẫn
        // tiếp tục được dùng để huấn luyện mô hình phục vụ người khác.
        var userId = await UserWithBothDeclaredAndInferredDataAsync();
        (await CountAsync(userId)).Scores.Should().Be(1, "tiền đề: hệ thống đã suy ra điều gì đó");

        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .PutAsJsonAsync("/api/v1/me/preferences", new
            {
                GenreIds = new[] { SeedHelper.GenreId1 },
                MoodIds = new[] { SeedHelper.MoodId1 },
                AtmosphereIds = Array.Empty<int>(),
                EnableAiConsent = false
            });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await CountAsync(userId);
        after.Scores.Should().Be(0, "điểm số hành vi là thứ hệ thống suy ra, phải biến mất");
        after.Recommendations.Should().Be(0);
    }

    [Fact]
    public async Task WithdrawingConsent_KeepsWhatTheUserDeclared()
    {
        // Đây là nửa còn lại, và là nửa dễ làm sai nhất. Sở thích người dùng tự chọn ở onboarding là
        // dữ liệu của họ, giao cho hệ thống có chủ đích. Nó không liên quan gì tới việc họ có cho
        // phân tích hành vi hay không — xoá luôn là hiểu sai ý người dùng theo hướng ngược lại.
        var userId = await UserWithBothDeclaredAndInferredDataAsync();

        await _factory.CreateAuthenticatedClient(userId, "Audience")
            .PutAsJsonAsync("/api/v1/me/preferences", new
            {
                GenreIds = new[] { SeedHelper.GenreId1 },
                MoodIds = new[] { SeedHelper.MoodId1 },
                AtmosphereIds = Array.Empty<int>(),
                EnableAiConsent = false
            });

        var after = await CountAsync(userId);
        after.Genres.Should().Be(1, "sở thích tự khai phải còn nguyên");
        after.Moods.Should().Be(1);
    }

    [Fact]
    public async Task JustUpdatingPreferencesWithoutTouchingConsent_DoesNotWipeAnything()
    {
        // Người dùng đổi sở thích mà vẫn giữ đồng ý thì không có lý do gì để xoá hồ sơ — làm vậy sẽ
        // khiến gợi ý của họ tệ đi sau mỗi lần chỉnh sở thích, đúng chiều ngược lại với ý định.
        var userId = await UserWithBothDeclaredAndInferredDataAsync();

        await _factory.CreateAuthenticatedClient(userId, "Audience")
            .PutAsJsonAsync("/api/v1/me/preferences", new
            {
                GenreIds = new[] { SeedHelper.GenreId2 },
                MoodIds = Array.Empty<int>(),
                AtmosphereIds = Array.Empty<int>(),
                EnableAiConsent = true
            });

        (await CountAsync(userId)).Scores.Should().Be(1);
    }

    // ---------- xoá tài khoản ----------

    [Fact]
    public async Task ErasingAnAccount_AlsoErasesTheBehaviouralProfile()
    {
        // Cột Breakdown lưu người này đã dự buổi nào, chấm mấy sao, có donate hay không. Đó đúng là
        // thứ mà một yêu cầu xoá dữ liệu sinh ra để xoá — và trước đây nó được cố ý bỏ lại.
        var userId = await UserWithBothDeclaredAndInferredDataAsync();

        var res = await _factory.CreateAuthenticatedClient(userId, "Audience")
            .PostAsJsonAsync("/api/v1/me/data-erasure", new { CurrentPassword = (string?)null });
        res.IsSuccessStatusCode.Should().BeTrue();

        var after = await CountAsync(userId);
        after.Scores.Should().Be(0);
        after.Recommendations.Should().Be(0);
        after.Genres.Should().Be(0, "xoá tài khoản thì xoá tất, khác với chỉ rút lại đồng ý");
    }

    // ---------- job tính lại điểm ----------

    [Fact]
    public async Task TheNightlyJob_DoesNotProfilePeopleWhoNeverAgreed()
    {
        // Bốn trong sáu nguồn của job đọc thẳng từ giao dịch (lưu quan tâm, đi xem, donate, đánh
        // giá) nên không bị chặn bởi consent. Kết quả là job dựng hồ sơ cho cả người chưa bao giờ
        // đồng ý, rồi hồ sơ đó đi thẳng vào tập huấn luyện của mô hình lọc cộng tác.
        //
        // Đếm tổng hợp trên giao dịch thì không cần xin phép — đó là đếm. Nhưng bảng này khoá theo
        // (người, buổi diễn) và dùng để suy ra sở thích, tức đúng là lập hồ sơ.
        int userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Email = $"noconsent-{Guid.NewGuid():N}@test.com",
                FullName = "Khong Dong Y",
                Role = UserRole.Audience,
                AuthProvider = "local",
                EmailVerifiedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                AiConsent = false
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;

            // Một giao dịch thật: lưu buổi diễn vào danh sách quan tâm. Nguồn này không qua consent.
            db.Add(new ShowWishlist
            {
                UserId = userId,
                LoungeShowId = SeedHelper.OfflineShowId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<RecomputeUserEventScoresJob>()
                .ExecuteAsync(new JobCancellationToken(false));
        }

        (await CountAsync(userId)).Scores.Should().Be(0,
            "người chưa đồng ý thì không được lập hồ sơ, dù giao dịch của họ có tồn tại");
    }

    [Fact]
    public async Task TheNightlyJob_CleansUpProfilesLeftBehindByAWithdrawal()
    {
        // Lưới đỡ thứ hai. Job này trước đây chỉ upsert, không bao giờ xoá — nên một hồ sơ còn sót
        // lại vì bất kỳ lý do gì sẽ nằm đó vĩnh viễn và vẫn được dùng.
        var userId = await UserWithBothDeclaredAndInferredDataAsync(consent: false);
        (await CountAsync(userId)).Scores.Should().Be(1, "tiền đề: có một hồ sơ còn sót lại");

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<RecomputeUserEventScoresJob>()
                .ExecuteAsync(new JobCancellationToken(false));
        }

        (await CountAsync(userId)).Scores.Should().Be(0);
    }
}
