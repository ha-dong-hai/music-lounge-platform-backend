using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-698. Mốc "đăng lịch trước ngày diễn" (<c>publish_min_business_days_lead_time</c>, NĐ 144/2020 Điều 10) được phép
/// đặt 0 để dựng một buổi hòa nhạc trong ngày trên sandbox. Trước đây mọi tham số số nguyên đều bị từ chối ở 0, nên muốn
/// thử trọn luồng bán vé → soát vé → quyết toán phải chờ 7 ngày làm việc.
///
/// <para>Ngoại lệ chỉ cho ĐÚNG khoá này: các tham số số nguyên khác vẫn phải lớn hơn 0, và số âm vẫn bị từ chối.</para>
///
/// <para><c>system_config</c> dùng chung giữa các test → mỗi ca trả nguyên trạng trong <c>finally</c> và xoá cache.</para>
/// </summary>
[Collection("Integration")]
public sealed class PublishLeadTimeZeroTests
{
    private const string Key = ConfigKeys.PublishMinBusinessDaysLeadTime;
    private readonly ApiFactory _factory;

    public PublishLeadTimeZeroTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

    private Task<HttpResponseMessage> SetAsync(string key, string value)
        => Admin().PutAsJsonAsync($"/api/v1/admin/system-config/{key}",
            new { ConfigValue = value, Note = "Hạ mốc để dựng buổi diễn thử trong ngày trên sandbox" });

    private async Task<string> CurrentAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue;
    }

    private async Task RestoreAsync(string key, string value)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key);
        row.ConfigValue = value;
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<ISystemConfigService>().Invalidate(key);
    }

    /// <summary>Buổi diễn sắp tới gần (không đủ 7 ngày làm việc), đủ mọi điều kiện nộp duyệt KHÁC.</summary>
    private async Task<Guid> ShowStartingSoonAsync(DateTimeOffset start)
    {
        var owner = Owner();
        var res = await owner.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"Đêm nhạc thử {Guid.NewGuid():N}",
            Description = "Buổi diễn dựng trong ngày",
            Format = "Offline",
            ScheduledStart = start,
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (Guid?)null,
            OfflineQuota = 100,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<Guid>(),
            MoodIds = Array.Empty<Guid>(),
            AtmosphereIds = Array.Empty<Guid>(),
            Performances = new[]
            {
                new { PerformerId = (Guid?)null, PerformerName = "Ban nhạc thử", Role = "Main", OrderIndex = 1, SetTime = (string?)null, AcceptsDonation = true }
            }
        });
        res.EnsureSuccessStatusCode();
        var showId = (await res.Content.ReadFromJsonAsync<DataResponse<Guid>>())!.Data;

        (await owner.PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/legal-approval",
            new { LegalApprovalReference = "SoVHTT-TEST-0698" })).EnsureSuccessStatusCode();

        (await owner.PostAsJsonAsync("/api/v1/ticket-tiers", new
        {
            ShowId = showId,
            Name = "Standard",
            Description = (string?)null,
            AccessType = "Physical",
            ZoneId = KhuThu.ChoBuoi(_factory, showId),
            TotalCapacity = 100,
            Prices = new[]
            {
                new
                {
                    Name = "Giá chuẩn", Price = 150_000m, Quota = (int?)50, PurchaseChannel = "Both",
                    SaleStart = DateTimeOffset.UtcNow, SaleEnd = start.AddHours(-1)
                }
            }
        })).EnsureSuccessStatusCode();
        return showId;
    }

    [Fact]
    public async Task WithTheStatutorySevenDays_AShowTomorrowCannotBeSubmitted()
    {
        (await CurrentAsync(Key)).Should().Be("7", "ca này kiểm mốc mặc định của seed");
        // +3 ngày: vẫn thiếu xa 7 ngày làm việc, và cách xa khung giờ của ca "đặt 0" (+1 ngày) để không trùng lịch phòng trà.
        var showId = await ShowStartingSoonAsync(DateTimeOffset.UtcNow.AddDays(3).AddHours(3));

        var res = await Owner().PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("ngày làm việc");
    }

    [Fact]
    public async Task ZeroIsAccepted_AndThenAShowTomorrowCanBeSubmitted()
    {
        var original = await CurrentAsync(Key);
        try
        {
            (await SetAsync(Key, "0")).StatusCode.Should().Be(HttpStatusCode.NoContent,
                "0 = không yêu cầu đăng trước; chủ dự án cần mức này để thử trong ngày");
            (await CurrentAsync(Key)).Should().Be("0");

            var showId = await ShowStartingSoonAsync(DateTimeOffset.UtcNow.AddDays(1).AddHours(9));
            var res = await Owner().PostAsync($"/api/v1/lounge-shows/{showId}/submit", null);

            res.StatusCode.Should().Be(HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
        }
        finally
        {
            await RestoreAsync(Key, original);
        }
    }

    [Fact]
    public async Task ANegativeLeadTime_IsStillRejected()
    {
        var res = await SetAsync(Key, "-1");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CurrentAsync(Key)).Should().Be("7", "thay đổi bị từ chối thì giá trị đang chạy không đổi");
    }

    [Fact]
    public async Task ZeroIsStillRejected_ForEveryOtherIntegerParameter()
    {
        const string other = ConfigKeys.RatingWindowDays;
        var original = await CurrentAsync(other);

        var res = await SetAsync(other, "0");

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "0 ngày đánh giá sẽ lặng lẽ tắt tính năng — ngoại lệ chỉ dành cho khoá có tên trong ZeroAllowedKeys");
        (await res.Content.ReadAsStringAsync()).Should().Contain("lớn hơn 0");
        (await CurrentAsync(other)).Should().Be(original);
    }

    private sealed record DataResponse<T>(bool Success, T Data);
}
