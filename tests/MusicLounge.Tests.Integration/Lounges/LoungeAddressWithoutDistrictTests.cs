using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-403. Địa chỉ theo đơn vị hành chính mới (từ 01/7/2025) không còn cấp quận, nhưng tạo/sửa phòng trà trả 400
/// "The District field is required." khi bỏ trống quận: trường này không nullable ở tầng API nên ASP.NET tự coi là bắt
/// buộc, dù validator và cả DB (chuỗi rỗng) đều cho bỏ trống. Mỗi bài tạo chủ phòng trà riêng — một chủ chỉ có một
/// phòng trà, và đổi địa chỉ phòng trà seed dùng chung sẽ làm hỏng test khác.
/// </summary>
[Collection("Integration")]
public sealed class LoungeAddressWithoutDistrictTests
{
    private readonly ApiFactory _factory;

    public LoungeAddressWithoutDistrictTests(ApiFactory factory) => _factory = factory;

    private async Task<int> FreshOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new User { Email = $"m403-{Guid.NewGuid():N}@test.com", FullName = "Trần Minh Quân", Role = UserRole.Owner };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        return owner.Id;
    }

    private async Task<int> ApprovedVenueWithDistrictAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeEntity
        {
            OwnerId = ownerId,
            Name = $"Phòng trà {Guid.NewGuid():N}"[..20],
            Status = LoungeStatus.Approved,
            Address = new VenueAddress
            {
                Street = "142 Trần Quang Khải", Ward = "Phường Tân Định", District = "Quận 1", City = "TP. Hồ Chí Minh"
            }
        };
        db.Add(lounge);
        await db.SaveChangesAsync();
        return lounge.Id;
    }

    private static async Task<JsonElement> DetailAsync(HttpClient client, int loungeId)
    {
        var res = await client.GetAsync($"/api/v1/lounges/{loungeId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task CreatingAVenue_WithAnAddressThatHasNoDistrict_Succeeds()
    {
        var owner = _factory.CreateAuthenticatedClient(await FreshOwnerAsync(), "Owner");

        var res = await owner.PostAsJsonAsync("/api/v1/lounges", new
        {
            Name = $"Phòng trà {Guid.NewGuid():N}"[..20],
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "36 Nguyễn Thị Nghĩa",
            Ward = "Phường Bến Thành",
            City = "TP. Hồ Chí Minh",
            Latitude = (double?)null,
            Longitude = (double?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var detail = await DetailAsync(owner, created.RootElement.GetProperty("data").GetInt32());
        detail.GetProperty("district").GetString().Should().BeEmpty();
        detail.GetProperty("fullAddress").GetString().Should().Be("36 Nguyễn Thị Nghĩa, Phường Bến Thành, TP. Hồ Chí Minh");
    }

    [Fact]
    public async Task ClearingTheDistrict_OnAnExistingVenue_RemovesIt()
    {
        var ownerId = await FreshOwnerAsync();
        var loungeId = await ApprovedVenueWithDistrictAsync(ownerId);
        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await owner.PutAsJsonAsync($"/api/v1/lounges/{loungeId}", new
        {
            Name = "Phòng trà Sương Mai",
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "142 Trần Quang Khải",
            Ward = "Phường Tân Định",
            District = (string?)null,
            City = "TP. Hồ Chí Minh",
            Latitude = (double?)null,
            Longitude = (double?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await DetailAsync(owner, loungeId);
        detail.GetProperty("district").GetString().Should().BeEmpty();
        detail.GetProperty("fullAddress").GetString().Should().Be("142 Trần Quang Khải, Phường Tân Định, TP. Hồ Chí Minh");
    }

    [Fact]
    public async Task UpdatingAVenue_WithoutSendingTheDistrictField_IsAccepted()
    {
        var ownerId = await FreshOwnerAsync();
        var loungeId = await ApprovedVenueWithDistrictAsync(ownerId);
        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await owner.PutAsJsonAsync($"/api/v1/lounges/{loungeId}", new
        {
            Name = "Phòng trà Sương Mai",
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "142 Trần Quang Khải",
            Ward = "Phường Tân Định",
            City = "TP. Hồ Chí Minh",
            Latitude = (double?)null,
            Longitude = (double?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetailAsync(owner, loungeId)).GetProperty("district").GetString().Should().BeEmpty();
    }

    [Fact]
    public async Task AnAddressWithADistrict_IsStillSavedAsBefore()
    {
        var ownerId = await FreshOwnerAsync();
        var loungeId = await ApprovedVenueWithDistrictAsync(ownerId);
        var owner = _factory.CreateAuthenticatedClient(ownerId, "Owner");

        var res = await owner.PutAsJsonAsync($"/api/v1/lounges/{loungeId}", new
        {
            Name = "Phòng trà Sương Mai",
            Description = (string?)null,
            AtmosphereId = (int?)null,
            Street = "25 Tú Xương",
            Ward = "Phường 7",
            District = "Quận 3",
            City = "TP. Hồ Chí Minh",
            Latitude = (double?)null,
            Longitude = (double?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await DetailAsync(owner, loungeId);
        detail.GetProperty("district").GetString().Should().Be("Quận 3");
        detail.GetProperty("fullAddress").GetString().Should().Be("25 Tú Xương, Phường 7, Quận 3, TP. Hồ Chí Minh");
    }
}
