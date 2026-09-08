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
/// MLACP-284. Business parameters — including the platform's commission and the withheld tax rate —
/// could previously only be changed by running SQL by hand, with nothing recording who did it or
/// why. SystemConfigHistory had been designed for exactly this (OldValue, NewValue, ChangedBy,
/// ChangedAt, mandatory Note, marked INSERT-only) and was written by nothing.
///
/// The tests that matter here are not "can an Admin edit a row" but the two guards that stop an
/// edit from quietly breaking money: a rate outside 0..1, and commission + tax reaching 100%, which
/// would make the owner's share — computed as the remainder — negative, something the double-entry
/// ledger cannot represent.
/// </summary>
[Collection("Integration")]
public sealed class SystemConfigAdminTests
{
    private readonly ApiFactory _factory;

    public SystemConfigAdminTests(ApiFactory factory) => _factory = factory;

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<string> CurrentValueAsync(string key)
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

    [Fact]
    public async Task ChangingAValue_WritesTheImmutableAuditRow()
    {
        const string key = ConfigKeys.RatingWindowDays;
        var original = await CurrentValueAsync(key);
        var proposed = original == "9" ? "8" : "9";

        var res = await Admin().PutAsJsonAsync($"/api/v1/admin/system-config/{key}",
            new { ConfigValue = proposed, Note = "Rút ngắn cửa sổ đánh giá theo phản hồi vận hành" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            (await db.SystemConfigs.SingleAsync(c => c.ConfigKey == key)).ConfigValue
                .Should().Be(proposed);

            // Sắp xếp phía client: SQLite provider dùng trong test không ORDER BY được
            // DateTimeOffset — giới hạn đã ghi nhận khắp codebase, và cũng là lý do handler thật
            // sắp xếp sau khi lấy về chứ không sắp trong truy vấn.
            var history = (await db.Set<MusicLounge.Domain.Entities.SystemConfigHistory>()
                    .Where(h => h.ConfigKey == key)
                    .ToListAsync())
                .OrderByDescending(h => h.ChangedAt)
                .First();

            history.OldValue.Should().Be(original,
                "the whole point of the audit table is answering \"what was it before\", which is " +
                "unanswerable once the row has been overwritten");
            history.NewValue.Should().Be(proposed);
            history.ChangedBy.Should().Be(SeedHelper.AdminId);
            history.Note.Should().NotBeNullOrWhiteSpace();
        }

        await RestoreAsync(key, original);
    }

    [Fact]
    public async Task ChangingAValue_WithoutAReason_IsRejected()
    {
        var res = await Admin().PutAsJsonAsync(
            $"/api/v1/admin/system-config/{ConfigKeys.RatingWindowDays}",
            new { ConfigValue = "5", Note = "" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the reason is the audit trail, not an optional field");
    }

    [Fact]
    public async Task RateAboveOne_IsRejectedAsAPercentageMistake()
    {
        // 5 is a plausible typo for 5% by someone who has not realised the column stores a fraction.
        var res = await Admin().PutAsJsonAsync(
            $"/api/v1/admin/system-config/{ConfigKeys.PlatformCommissionRate}",
            new { ConfigValue = "5", Note = "Nâng hoa hồng lên 5 phần trăm" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain("0 đến 1");

        (await CurrentValueAsync(ConfigKeys.PlatformCommissionRate)).Should().Be("0.05",
            "a rejected change must leave the live rate untouched");
    }

    [Fact]
    public async Task CommissionPlusTaxReachingOneHundredPercent_IsRejected()
    {
        // tax_rate is seeded at 0.05, so 0.95 commission would leave the owner exactly nothing.
        var res = await Admin().PutAsJsonAsync(
            $"/api/v1/admin/system-config/{ConfigKeys.PlatformCommissionRate}",
            new { ConfigValue = "0.95", Note = "Thử đẩy hoa hồng lên rất cao để kiểm tra giới hạn" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "the owner's share is the remainder after commission and tax — at 100% it is zero, and " +
            "beyond that it is negative, which the ledger cannot record");
        (await res.Content.ReadAsStringAsync()).Should().Contain("Sổ cái kép");

        (await CurrentValueAsync(ConfigKeys.PlatformCommissionRate)).Should().Be("0.05");
    }

    [Fact]
    public async Task History_IsVisibleAndNewestFirst()
    {
        const string key = ConfigKeys.RatingWindowDays;
        var original = await CurrentValueAsync(key);

        await Admin().PutAsJsonAsync($"/api/v1/admin/system-config/{key}",
            new { ConfigValue = "11", Note = "Thay đổi thứ nhất để kiểm tra lịch sử" });
        await Admin().PutAsJsonAsync($"/api/v1/admin/system-config/{key}",
            new { ConfigValue = "12", Note = "Thay đổi thứ hai để kiểm tra thứ tự" });

        var res = await Admin().GetAsync($"/api/v1/admin/system-config/{key}/history");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<Envelope<List<HistoryRow>>>();
        body!.Data.Should().HaveCountGreaterThanOrEqualTo(2);
        body.Data[0].NewValue.Should().Be("12", "newest change first");
        body.Data[0].OldValue.Should().Be("11");
        body.Data[0].ChangedByName.Should().NotBeNullOrWhiteSpace();

        await RestoreAsync(key, original);
    }

    [Fact]
    public async Task NonAdmin_CannotReadOrChangeBusinessParameters()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        (await owner.GetAsync("/api/v1/admin/system-config")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await owner.PutAsJsonAsync($"/api/v1/admin/system-config/{ConfigKeys.PlatformCommissionRate}",
            new { ConfigValue = "0.01", Note = "Chủ phòng trà tự hạ hoa hồng của mình" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record HistoryRow(
        long Id, string ConfigKey, string? OldValue, string NewValue, string Note,
        DateTimeOffset ChangedAt, int ChangedBy, string? ChangedByName);
}
