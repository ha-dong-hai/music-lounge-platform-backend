using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// <c>GET /me/citizen-card</c>: chủ phòng trà đọc được trạng thái xác minh danh tính của chính mình. Trước đây nộp
/// xong là mất dấu — không biết được duyệt hay bị từ chối, và vì sao — trong khi đó là điều kiện để được bán.
/// Test đổi trạng thái trên user dùng chung nên trả nguyên trạng trong <c>finally</c>.
/// </summary>
[Collection("Integration")]
public sealed class MyCitizenCardStatusTests
{
    private readonly ApiFactory _factory;

    public MyCitizenCardStatusTests(ApiFactory factory) => _factory = factory;

    private sealed record TrangThai(string? CardNumber, KycReviewStatus? Status, string? Note, DateTimeOffset? SubmittedAt);

    private async Task<TrangThai> DatAsync(TrangThai moi)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
        var u = await db.Set<User>().FindAsync(SeedHelper.OwnerId);
        var cu = new TrangThai(u!.CitizenCardNumber, u.CitizenCardReviewStatus, u.CitizenCardReviewNote, u.CitizenCardSubmittedAt);
        u.CitizenCardNumber = moi.CardNumber is null ? null : pii.Encrypt(moi.CardNumber);
        u.CitizenCardReviewStatus = moi.Status;
        u.CitizenCardReviewNote = moi.Note;
        u.CitizenCardSubmittedAt = moi.SubmittedAt;
        await db.SaveChangesAsync();
        return cu;
    }

    private async Task KhoiPhucAsync(TrangThai cu)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var u = await db.Set<User>().FindAsync(SeedHelper.OwnerId);
        u!.CitizenCardNumber = cu.CardNumber;
        u.CitizenCardReviewStatus = cu.Status;
        u.CitizenCardReviewNote = cu.Note;
        u.CitizenCardSubmittedAt = cu.SubmittedAt;
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> DocAsync()
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId)
            .GetAsync("/api/v1/me/citizen-card");
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task ChuaNop_KhongDuocBan_VaNoiRoPhaiLamGi()
    {
        var cu = await DatAsync(new TrangThai(null, null, null, null));
        try
        {
            var d = await DocAsync();
            d.GetProperty("reviewStatus").ValueKind.Should().Be(JsonValueKind.Null);
            d.GetProperty("canSell").GetBoolean().Should().BeFalse();
            d.GetProperty("explanation").GetString().Should().Contain("chưa nộp");
        }
        finally { await KhoiPhucAsync(cu); }
    }

    [Fact]
    public async Task BiTuChoi_ThayDungLyDo_VaSoCccdBiChe()
    {
        var cu = await DatAsync(new TrangThai("079201012345", KycReviewStatus.Rejected, "Ảnh mặt sau bị mờ, không đọc được.", DateTimeOffset.UtcNow));
        try
        {
            var d = await DocAsync();
            d.GetProperty("reviewStatus").GetString().Should().Be("Rejected");
            d.GetProperty("reviewNote").GetString().Should().Be("Ảnh mặt sau bị mờ, không đọc được.");
            d.GetProperty("explanation").GetString().Should().Contain("Ảnh mặt sau bị mờ");
            d.GetProperty("canSell").GetBoolean().Should().BeFalse();
            var so = d.GetProperty("numberMasked").GetString();
            so.Should().EndWith("2345").And.NotContain("0792010", "chỉ được lộ 4 số cuối");
        }
        finally { await KhoiPhucAsync(cu); }
    }

    [Fact]
    public async Task DaDuyet_DuocBan()
    {
        var cu = await DatAsync(new TrangThai("079201012345", KycReviewStatus.Approved, null, DateTimeOffset.UtcNow));
        try
        {
            var d = await DocAsync();
            d.GetProperty("reviewStatus").GetString().Should().Be("Approved");
            d.GetProperty("canSell").GetBoolean().Should().BeTrue("đúng luật mà các cổng bán dùng — SellerIdentity.IsVerified");
        }
        finally { await KhoiPhucAsync(cu); }
    }
}
