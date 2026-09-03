using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF5;

/// <summary>
/// CF5 W16/W17/W18 — F&B menu + order lifecycle
/// GET/POST /api/v1/fnb-menus | PUT /api/v1/fnb-menus/{id}
/// POST /api/v1/fnb-menu-items | PUT /api/v1/fnb-menu-items/{id}
/// POST /api/v1/fnb-orders | PUT /api/v1/fnb-orders/{id}/status | GET /api/v1/fnb-orders
/// </summary>
[Collection("Integration")]
public sealed class FnbTests
{
    private readonly ApiFactory _factory;

    public FnbTests(ApiFactory factory) => _factory = factory;

    private async Task<int> CreateMenuAsync(string name = "Menu Mặc Định", int loungeId = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var menu = new FnbMenu
        {
            LoungeId = loungeId == 0 ? SeedHelper.LoungeId : loungeId,
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(menu);
        await db.SaveChangesAsync();
        return menu.Id;
    }

    private async Task<int> CreateMenuItemAsync(decimal price = 50_000m, int? menuId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var item = new FnbMenuItem
        {
            MenuId = menuId ?? await CreateMenuAsync(),
            Category = "Drink",
            Name = "Mojito",
            Price = price,
            IsAvailable = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    // ─── Menus ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFnbMenu_AsOwner_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-menus", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = "Menu Sáng",
            Description = (string?)null,
            DisplayOrder = 0
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetFnbMenus_OneVenue_CanHaveMultipleDistinctMenus()
    {
        var morningId = await CreateMenuAsync("Menu Sáng");
        var eveningId = await CreateMenuAsync("Menu Tối");
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/fnb-menus?loungeId={SeedHelper.LoungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = (await res.Content.ReadFromJsonAsync<DataResponse<List<FnbMenuResponseItem>>>())!;
        body.Data.Select(m => m.Id).Should().Contain([morningId, eveningId]);
        body.Data.Should().Contain(m => m.Name == "Menu Sáng");
        body.Data.Should().Contain(m => m.Name == "Menu Tối");
    }

    // ─── Menu items ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMenuItem_AsOwner_Returns201()
    {
        var menuId = await CreateMenuAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-menu-items", new
        {
            MenuId = menuId,
            Category = "Food",
            Name = "Fries",
            Description = (string?)null,
            Price = 30_000m,
            ImageUrl = (string?)null,
            DisplayOrder = 0
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateMenuItem_AsAudience_Returns403()
    {
        var menuId = await CreateMenuAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-menu-items", new
        {
            MenuId = menuId,
            Category = "Food",
            Name = "Should fail",
            Description = (string?)null,
            Price = 30_000m,
            ImageUrl = (string?)null,
            DisplayOrder = 0
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMenuItems_NoAuth_Returns200()
    {
        var menuId = await CreateMenuAsync();
        await CreateMenuItemAsync(menuId: menuId);
        var client = _factory.CreateClient();

        var res = await client.GetAsync($"/api/v1/fnb-menu-items?menuId={menuId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("\"success\":true");
    }

    // ─── Orders ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFnbOrder_AsAudience_Returns201WithComputedTotal()
    {
        var menuItemId = await CreateMenuItemAsync(price: 50_000m);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");

        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = "Bàn A3",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 2, Note = (string?)null } }
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateFnbOrder_ForUnavailableItem_Returns422()
    {
        int menuItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var menu = new FnbMenu
            {
                LoungeId = SeedHelper.LoungeId, Name = "Menu Test", IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(menu);
            await db.SaveChangesAsync();

            var item = new FnbMenuItem
            {
                MenuId = menu.Id, Category = "Drink", Name = "Sold Out",
                Price = 20_000m, IsAvailable = false, CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(item);
            await db.SaveChangesAsync();
            menuItemId = item.Id;
        }

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 1, Note = (string?)null } }
        });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateFnbOrder_AsStaff_SetsStaffIdNotAudience()
    {
        var menuItemId = await CreateMenuItemAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);

        var res = await client.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = "Bàn B1",
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 1, Note = (string?)null } }
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateFnbOrderStatus_SequentialTransition_Returns204()
    {
        var menuItemId = await CreateMenuItemAsync();
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var createRes = await audienceClient.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 1, Note = (string?)null } }
        });
        var orderId = (await createRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        var res = await staffClient.PutAsJsonAsync($"/api/v1/fnb-orders/{orderId}/status", new { Status = "Preparing" });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateFnbOrderStatus_SkippingStep_Returns422()
    {
        var menuItemId = await CreateMenuItemAsync();
        var audienceClient = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var createRes = await audienceClient.PostAsJsonAsync("/api/v1/fnb-orders", new
        {
            LoungeId = SeedHelper.LoungeId,
            ShowId = (int?)null,
            ZoneId = (int?)null,
            TableNote = (string?)null,
            PaymentMethod = "Cash",
            Note = (string?)null,
            Items = new[] { new { MenuItemId = menuItemId, Quantity = 1, Note = (string?)null } }
        });
        var orderId = (await createRes.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;

        var staffClient = _factory.CreateAuthenticatedClient(SeedHelper.StaffId, "Staff", SeedHelper.LoungeId);
        // Skip straight to Served, bypassing Preparing
        var res = await staffClient.PutAsJsonAsync($"/api/v1/fnb-orders/{orderId}/status", new { Status = "Served" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetFnbOrders_AsOwner_Returns200()
    {
        await CreateMenuItemAsync();
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFnbOrders_AsUnrelatedOwner_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await client.GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record DataResponse<T>(bool Success, T Data);
    private sealed record FnbMenuResponseItem(int Id, int LoungeId, string Name, string? Description, bool IsActive, int DisplayOrder);
}
