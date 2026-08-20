using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// Admin user-account management — GET/POST /api/v1/admin/users...
/// </summary>
[Collection("Integration")]
public sealed class AdminUserManagementTests
{
    private readonly ApiFactory _factory;

    public AdminUserManagementTests(ApiFactory factory) => _factory = factory;

    // ─── GetUsers ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_AsAdmin_Returns200WithSeededUsers()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/admin/users?page=1&pageSize=50");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UsersListResponse>();
        body!.Data.Items.Should().Contain(u => u.Id == SeedHelper.AudienceId);
    }

    [Fact]
    public async Task GetUsers_SearchByFullName_ReturnsOnlyMatching()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/admin/users?searchText=Audience");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UsersListResponse>();
        body!.Data.Items.Should().OnlyContain(u =>
            u.FullName.Contains("Audience", StringComparison.OrdinalIgnoreCase)
            || u.Email.Contains("Audience", StringComparison.OrdinalIgnoreCase));
        body.Data.Items.Should().Contain(u => u.Id == SeedHelper.AudienceId);
    }

    [Fact]
    public async Task GetUsers_FilterByRole_ReturnsOnlyThatRole()
    {
        var ownerRoleUserId = await CreateUserWithRoleAsync(UserRole.Owner);

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await client.GetAsync("/api/v1/admin/users?role=Owner&pageSize=100");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UsersListResponse>();
        body!.Data.Items.Should().OnlyContain(u => u.Role == "Owner");
        body.Data.Items.Should().Contain(u => u.Id == ownerRoleUserId);
    }

    [Fact]
    public async Task GetUsers_FilterByIsActiveFalse_ExcludesActiveUsers()
    {
        var inactiveUserId = await CreateUserWithRoleAsync(UserRole.Audience, isActive: false);

        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await client.GetAsync("/api/v1/admin/users?isActive=false&pageSize=100");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UsersListResponse>();
        body!.Data.Items.Should().OnlyContain(u => !u.IsActive);
        body.Data.Items.Should().Contain(u => u.Id == inactiveUserId);
        body.Data.Items.Should().NotContain(u => u.Id == SeedHelper.AudienceId); // seeded user is active
    }

    [Fact]
    public async Task GetUsers_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.GetAsync("/api/v1/admin/users");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── GetUserDetail ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserDetail_ExistingId_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync($"/api/v1/admin/users/{SeedHelper.AudienceId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UserDetailResponse>();
        body!.Data.Id.Should().Be(SeedHelper.AudienceId);
    }

    [Fact]
    public async Task GetUserDetail_NonExistentId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.GetAsync("/api/v1/admin/users/999999");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Deactivate / Reactivate ─────────────────────────────────────────────

    [Fact]
    public async Task DeactivateUserAccount_AsAdmin_Returns204AndSetsIsActiveFalse()
    {
        var userId = await CreateUserWithRoleAsync(UserRole.Audience);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsync($"/api/v1/admin/users/{userId}/deactivate", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ReactivateUserAccount_AsAdmin_Returns204AndSetsIsActiveTrue()
    {
        var userId = await CreateUserWithRoleAsync(UserRole.Audience, isActive: false);
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

        var res = await client.PostAsync($"/api/v1/admin/users/{userId}/reactivate", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Users.SingleAsync(u => u.Id == userId)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateUserAccount_AsNonAdmin_Returns403()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

        var res = await client.PostAsync($"/api/v1/admin/users/{SeedHelper.AudienceId}/deactivate", null);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<int> CreateUserWithRoleAsync(UserRole role, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"role-test-{Guid.NewGuid():N}@test.com",
            FullName = $"Role Test {role}",
            Role = role,
            IsActive = isActive,
            EmailVerifiedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private sealed record UsersListResponse(bool Success, PaginatedUsers Data);

    private sealed record PaginatedUsers(List<UserAdminItem> Items, int Page, int PageSize, int TotalCount);

    private sealed record UserDetailResponse(bool Success, UserAdminItem Data);

    private sealed record UserAdminItem(
        int Id, string Email, string FullName, string? Phone, string? AvatarUrl,
        string Role, bool IsActive, bool IsEmailVerified, DateTime CreatedAt);
}
