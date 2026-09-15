using System.Net;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-405. Các API nhận id bắt buộc qua query dưới dạng <c>int</c>: thiếu tham số thì id thành 0 và đi thẳng xuống handler,
/// trả 404 "không tìm thấy … 0" hoặc âm thầm trả danh sách rỗng — người gọi tưởng dữ liệu không tồn tại, trong khi lỗi thật
/// là thiếu tham số. Mỗi bài gọi đúng một API mà không truyền id, bằng đúng vai trò được phép gọi API đó.
/// </summary>
[Collection("Integration")]
public sealed class RequiredQueryIdTests
{
    private readonly ApiFactory _factory;

    public RequiredQueryIdTests(ApiFactory factory) => _factory = factory;

    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

    private static async Task ShouldBeMissingIdAsync(HttpResponseMessage res, string field)
    {
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").TryGetProperty(field, out _)
            .Should().BeTrue($"lỗi phải nêu đúng trường {field} bị thiếu");
    }

    [Fact]
    public async Task OwnerAnalytics_WithoutLoungeId_IsABadRequest()
        => await ShouldBeMissingIdAsync(await Owner().GetAsync("/api/v1/analytics/my-lounge"), "LoungeId");

    [Fact]
    public async Task BankAccounts_WithoutOwnerId_IsABadRequest()
        => await ShouldBeMissingIdAsync(await Owner().GetAsync("/api/v1/bank-accounts?ownerType=Lounge"), "OwnerId");

    [Fact]
    public async Task CustomCriteria_WithoutLoungeId_IsABadRequest()
        => await ShouldBeMissingIdAsync(await Owner().GetAsync("/api/v1/custom-criteria"), "LoungeId");

    [Fact]
    public async Task FnbOrders_WithoutLoungeId_IsABadRequest()
        => await ShouldBeMissingIdAsync(await Owner().GetAsync("/api/v1/fnb-orders"), "LoungeId");

    [Fact]
    public async Task FnbMenus_WithoutLoungeId_IsABadRequest_NotAnEmptyMenu()
        => await ShouldBeMissingIdAsync(await _factory.CreateClient().GetAsync("/api/v1/fnb-menus"), "LoungeId");

    [Fact]
    public async Task FnbMenuItems_WithoutMenuId_IsABadRequest_NotAnEmptyMenu()
        => await ShouldBeMissingIdAsync(await _factory.CreateClient().GetAsync("/api/v1/fnb-menu-items"), "MenuId");

    [Fact]
    public async Task TicketTiers_WithoutShowId_IsABadRequest()
        => await ShouldBeMissingIdAsync(await _factory.CreateClient().GetAsync("/api/v1/ticket-tiers"), "ShowId");
}
