using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// MLACP-391. Từ MLACP-381 Admin được thêm/gỡ nhân viên của phòng trà bất kỳ. Nhân viên có quyền soát vé và bán tại
/// quầy, nên chủ phòng trà — người chịu trách nhiệm vận hành — phải được báo khi người khác làm việc đó thay họ. Và
/// người gỡ phải được lưu lại (<c>DeactivatedBy</c>) như người thêm (<c>AssignedBy</c>), không chỉ nằm trong log.
/// Mỗi bài một tài khoản ứng viên riêng.
/// </summary>
[Collection("Integration")]
public sealed class StaffChangesByAdminTests
{
    private readonly ApiFactory _factory;

    public StaffChangesByAdminTests(ApiFactory factory) => _factory = factory;

    private sealed record DataResponse<T>(bool Success, T Data);

    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<(int UserId, string Name)> CandidateAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var name = $"NV391 {Guid.NewGuid():N}";
        var user = new User { Email = $"staff391-{Guid.NewGuid():N}@test.com", FullName = name, Role = UserRole.Audience };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, name);
    }

    private static async Task<int> AssignAsync(HttpClient client, int userId)
    {
        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/staff", new { UserId = userId });
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<DataResponse<int>>())!.Data;
    }

    private static async Task RemoveAsync(HttpClient client, int staffId)
        => (await client.DeleteAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/staff/{staffId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    /// <summary>Thông báo cho chủ phòng trà nhắc đúng ứng viên này (tên có GUID nên không lẫn với bài khác).</summary>
    private async Task<List<Notification>> OwnerNoticesAboutAsync(string candidateName)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == SeedHelper.OwnerId && n.Type == NotificationType.VenueStaffChanged
                        && n.Body.Contains(candidateName))
            .ToListAsync();
    }

    private async Task<LoungeStaff> StaffRowAsync(int staffId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<LoungeStaff>().AsNoTracking()
            .SingleAsync(s => s.Id == staffId);
    }

    [Fact]
    public async Task AnAdminAddingStaff_TellsTheVenueOwner()
    {
        var (userId, name) = await CandidateAsync();

        await AssignAsync(Admin(), userId);

        (await OwnerNoticesAboutAsync(name)).Should().ContainSingle(
                "someone now scans tickets and sells at the counter for the owner's venue")
            .Which.Title.Should().Contain("thêm nhân viên");
    }

    [Fact]
    public async Task TheOwnerAddingTheirOwnStaff_SendsThemNoNotice()
    {
        var (userId, name) = await CandidateAsync();

        await AssignAsync(Owner(), userId);

        (await OwnerNoticesAboutAsync(name)).Should().BeEmpty("the owner did it themselves");
    }

    [Fact]
    public async Task AnAdminRemovingStaff_IsRecordedAsTheRemover_AndTheOwnerIsTold()
    {
        var (userId, name) = await CandidateAsync();
        var staffId = await AssignAsync(Owner(), userId);

        await RemoveAsync(Admin(), staffId);

        (await StaffRowAsync(staffId)).DeactivatedBy.Should().Be(SeedHelper.AdminId);
        (await OwnerNoticesAboutAsync(name)).Should().ContainSingle().Which.Title.Should().Contain("gỡ");

        var list = await Owner().GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/staff");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").EnumerateArray()
            .Single(e => e.GetProperty("id").GetInt32() == staffId)
            .GetProperty("deactivatedBy").GetInt32()
            .Should().Be(SeedHelper.AdminId, "the owner can see who removed the account from their staff list");
    }

    [Fact]
    public async Task TheOwnerRemovingStaff_IsRecordedAsTheRemover_WithNoNotice()
    {
        var (userId, name) = await CandidateAsync();
        var staffId = await AssignAsync(Owner(), userId);

        await RemoveAsync(Owner(), staffId);

        (await StaffRowAsync(staffId)).DeactivatedBy.Should().Be(SeedHelper.OwnerId);
        (await OwnerNoticesAboutAsync(name)).Should().BeEmpty();
    }
}
