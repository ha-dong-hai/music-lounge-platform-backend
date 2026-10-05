using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF4;

/// <summary>
/// MLACP-651. GET /performers là danh mục DÙNG CHUNG giữa mọi phòng trà (chọn nghệ sĩ diễn khách), nhưng email liên lạc
/// của nghệ sĩ là dữ liệu cá nhân do người tạo hồ sơ nhập. Đo 05/10/2026: chủ phòng trà Aqua đọc được email của nghệ sĩ
/// do Ánh Dương quản lý — qua cả danh sách lẫn chi tiết. Sửa hồ sơ và tài khoản ngân hàng của nghệ sĩ đó thì đã bị chặn
/// (403); chỉ riêng việc ĐỌC email là lọt.
/// </summary>
[Collection("Integration")]
public sealed class PerformerContactEmailPrivacyTests
{
    private readonly ApiFactory _factory;

    public PerformerContactEmailPrivacyTests(ApiFactory factory) => _factory = factory;

    private async Task<(Guid Id, string Name, string Email)> CreatedBySeedOwnerAsync()
    {
        var name = $"Nghe si 651 {Guid.NewGuid():N}"[..24];
        var email = $"ns651-{Guid.NewGuid():N}@test.com";
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner").PostAsJsonAsync("/api/v1/performers", new
        {
            Name = name, AvatarUrl = (string?)null, Bio = (string?)null, Type = "Solo", GenreIds = Array.Empty<Guid>(), ContactEmail = email
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetGuid();
        return (id, name, email);
    }

    private async Task<string?> EmailInListAsync(HttpClient client, string name, Guid id)
    {
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/performers?search={Uri.EscapeDataString(name)}&pageSize=10");
        var item = json.GetProperty("data").GetProperty("items").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == id);
        return item.GetProperty("contactEmail").ValueKind == JsonValueKind.Null ? null : item.GetProperty("contactEmail").GetString();
    }

    private async Task<string?> EmailInDetailAsync(HttpClient client, Guid id)
    {
        var data = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/performers/{id}")).GetProperty("data");
        return data.GetProperty("contactEmail").ValueKind == JsonValueKind.Null ? null : data.GetProperty("contactEmail").GetString();
    }

    [Fact]
    public async Task AnotherVenuesOwner_SeesThePerformer_ButNotTheirEmail()
    {
        var p = await CreatedBySeedOwnerAsync();
        var other = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        (await EmailInListAsync(other, p.Name, p.Id)).Should().BeNull("the shared catalog is for picking guest performers, not for reading their contact details");
        (await EmailInDetailAsync(other, p.Id)).Should().BeNull();
    }

    [Fact]
    public async Task TheOwnerWhoManagesThePerformer_AndAdmins_StillSeeTheEmail()
    {
        var p = await CreatedBySeedOwnerAsync();

        (await EmailInListAsync(_factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner"), p.Name, p.Id))
            .Should().Be(p.Email, "the edit form reads it back — losing it would wipe the address on save");
        (await EmailInDetailAsync(_factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner"), p.Id)).Should().Be(p.Email);
        (await EmailInDetailAsync(_factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin"), p.Id)).Should().Be(p.Email);
    }
}
