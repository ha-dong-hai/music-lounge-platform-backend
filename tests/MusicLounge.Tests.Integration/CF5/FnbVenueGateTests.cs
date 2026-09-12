using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.CF5;

/// <summary>
/// MLACP-380. <c>VenueLifecycle.CanOperate</c> đã chặn vé/donation (MLACP-354) khi phòng trà bị tạm đình chỉ hay
/// khoá vĩnh viễn, nhưng F&amp;B bị bỏ sót — khách vẫn đặt món và trả tiền được cho một phòng trà đang bị khoá vì
/// vi phạm.
///
/// <para>Mỗi bài một phòng trà riêng (gói riêng, không đụng phòng trà dùng chung của <see cref="SeedHelper"/>).</para>
/// </summary>
[Collection("Integration")]
public sealed class FnbVenueGateTests
{
    private const string BuyerMessage = "tạm ngừng giao dịch trên nền tảng";

    private readonly ApiFactory _factory;

    public FnbVenueGateTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record Venue(int LoungeId, int OwnerId, int MenuItemId);

    private async Task<Venue> CreateVenueAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var owner = new User { Email = $"v380-{Guid.NewGuid():N}@test.com", FullName = "Test Venue Owner" };
        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var lounge = new MusicLoungeEntity
        {
            OwnerId = owner.Id,
            Name = $"Gate380 {Guid.NewGuid():N}"[..14],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Lê Lợi", District = "1", City = "HCM" }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();

        var menu = new FnbMenu { LoungeId = lounge.Id, Name = "Menu", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Add(menu);
        await db.SaveChangesAsync();

        var item = new FnbMenuItem
        {
            MenuId = menu.Id, Category = "Drink", Name = "Mojito", Price = 50_000m, IsAvailable = true
        };
        db.Add(item);
        await db.SaveChangesAsync();

        return new Venue(lounge.Id, owner.Id, item.Id);
    }

    private async Task SetVenueStatusAsync(int loungeId, LoungeStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = await db.Lounges.SingleAsync(l => l.Id == loungeId);
        lounge.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<int> CreateOrderAsync(Venue venue, HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = venue.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = venue.MenuItemId, Quantity = 1, Note = (string?)null } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;
    }

    // ── Tạo đơn ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LoungeStatus.Suspended)]
    [InlineData(LoungeStatus.Locked)]
    public async Task CreateFnbOrder_AsAudience_WhenVenueCannotOperate_Returns422(LoungeStatus status)
    {
        var venue = await CreateVenueAsync();
        await SetVenueStatusAsync(venue.LoungeId, status);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = venue.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = venue.MenuItemId, Quantity = 1, Note = (string?)null } }
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await res.Content.ReadAsStringAsync()).Should().Contain(BuyerMessage);
    }

    [Fact]
    public async Task CreateFnbOrder_ByStaff_WhenVenueSuspended_Returns422WithStaffMessage()
    {
        var venue = await CreateVenueAsync();
        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Suspended);
        var client = _factory.CreateAuthenticatedClient(venue.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = venue.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = venue.MenuItemId, Quantity = 1, Note = (string?)null } }
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("tạm đình chỉ", "nhân viên/chủ phòng trà phải biết vì sao — khác câu cho người mua");
        body.Should().NotContain(BuyerMessage);
    }

    [Fact]
    public async Task CreateFnbOrder_WhenVenueWarned_StillWorks()
    {
        // Cảnh cáo là một vết ghi lại, không phải lệnh dừng — VenueLifecycle.Operating gồm Warned.
        var venue = await CreateVenueAsync();
        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Warned);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = venue.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = venue.MenuItemId, Quantity = 1, Note = (string?)null } }
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Trả tiền cho đơn đã đặt trước khi bị đình chỉ ────────────────────────

    [Fact]
    public async Task InitiateFnbOrderPayment_OrderedBeforeSuspension_CannotPay()
    {
        var venue = await CreateVenueAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var orderId = await CreateOrderAsync(venue, client);

        await SetVenueStatusAsync(venue.LoungeId, LoungeStatus.Suspended);

        var res = await client.PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "chốt ở bước tạo đơn không đủ — tiền thu ở bước này");
        (await res.Content.ReadAsStringAsync()).Should().Contain(BuyerMessage);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Set<Payment>().AnyAsync(p => p.ReferenceType == "FnbOrder" && p.ReferenceId == orderId.ToString()))
            .Should().BeFalse("không được mở một giao dịch thanh toán nào");
    }

    [Fact]
    public async Task InitiateFnbOrderPayment_VenueOperating_Works()
    {
        var venue = await CreateVenueAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var orderId = await CreateOrderAsync(venue, client);

        var res = await client.PostAsync($"/api/v1/fnb-orders/{orderId}/pay", null);

        res.IsSuccessStatusCode.Should().BeTrue("chốt mới không được chặn phòng trà đang hoạt động");
    }
}
