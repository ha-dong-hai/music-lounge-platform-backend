using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Catalog;

/// <summary>
/// MLACP-581. Thẻ thể loại ở trang chủ mượn ảnh của một buổi hòa nhạc; chủ dự án 03/10/2026 hỏi vì sao trang Admin
/// không có chỗ đặt ảnh cho thể loại. Thể loại giờ có ảnh riêng KHÔNG bắt buộc, Admin đặt/gỡ qua endpoint riêng.
///
/// Mỗi ca tự tạo một thể loại riêng rồi xoá trong finally — không đụng 8 thể loại seed dùng chung giữa các test.
/// </summary>
[Collection("Integration")]
public sealed class MusicGenreImageTests
{
    private readonly ApiFactory _factory;

    public MusicGenreImageTests(ApiFactory factory) => _factory = factory;

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record UploadedUrl(string Url);
    private sealed record Genre(Guid Id, string Name, string? ImageUrl);

    private HttpClient Admin() => _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");

    private async Task<Guid> NewGenreAsync()
    {
        var res = await Admin().PostAsJsonAsync("/api/v1/admin/genres", new { Name = $"TL-{Guid.NewGuid():N}"[..20] });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;
    }

    private async Task<string> UploadAsync()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", $"the-loai-{Guid.NewGuid():N}.png");
        var res = await Admin().PostAsync("/api/v1/uploads/images", form);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Envelope<UploadedUrl>>())!.Data.Url;
    }

    private async Task<Genre> PublicAsync(Guid id)
    {
        var list = (await _factory.CreateClient().GetFromJsonAsync<Envelope<List<Genre>>>("/api/v1/catalog/music-genres"))!.Data;
        list.Should().NotBeEmpty();
        return list.Single(g => g.Id == id);
    }

    private async Task<Genre> AdminViewAsync(Guid id)
        => (await Admin().GetFromJsonAsync<Envelope<List<Genre>>>("/api/v1/admin/genres"))!.Data.Single(g => g.Id == id);

    [Fact]
    public async Task AdminSetsAnImage_ItShowsInPublicCatalogAndAdminList()
    {
        var id = await NewGenreAsync();
        try
        {
            (await PublicAsync(id)).ImageUrl.Should().BeNull("thể loại mới chưa có ảnh riêng");
            var url = await UploadAsync();

            var res = await Admin().PutAsJsonAsync($"/api/v1/admin/genres/{id}/image", new { ImageUrl = url });

            res.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await PublicAsync(id)).ImageUrl.Should().Be(url);
            (await AdminViewAsync(id)).ImageUrl.Should().Be(url);
        }
        finally { await Admin().DeleteAsync($"/api/v1/admin/genres/{id}"); }
    }

    [Fact]
    public async Task RenamingTheGenreKeepsItsImage()
    {
        var id = await NewGenreAsync();
        try
        {
            var url = await UploadAsync();
            (await Admin().PutAsJsonAsync($"/api/v1/admin/genres/{id}/image", new { ImageUrl = url })).EnsureSuccessStatusCode();

            (await Admin().PutAsJsonAsync($"/api/v1/admin/genres/{id}", new { Name = $"Doi-{Guid.NewGuid():N}"[..20], NameEn = "Renamed" }))
                .EnsureSuccessStatusCode();

            (await PublicAsync(id)).ImageUrl.Should().Be(url, "sửa tên không được làm mất ảnh");
        }
        finally { await Admin().DeleteAsync($"/api/v1/admin/genres/{id}"); }
    }

    [Fact]
    public async Task ClearingTheImageFallsBackToNull()
    {
        var id = await NewGenreAsync();
        try
        {
            var url = await UploadAsync();
            (await Admin().PutAsJsonAsync($"/api/v1/admin/genres/{id}/image", new { ImageUrl = url })).EnsureSuccessStatusCode();

            var res = await Admin().DeleteAsync($"/api/v1/admin/genres/{id}/image");

            res.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await PublicAsync(id)).ImageUrl.Should().BeNull();
        }
        finally { await Admin().DeleteAsync($"/api/v1/admin/genres/{id}"); }
    }

    [Fact]
    public async Task AnExternalImageUrlIsRejected()
    {
        var id = await NewGenreAsync();
        try
        {
            var res = await Admin().PutAsJsonAsync($"/api/v1/admin/genres/{id}/image",
                new { ImageUrl = "https://example.com/anh-ngoai.jpg" });

            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PublicAsync(id)).ImageUrl.Should().BeNull();
        }
        finally { await Admin().DeleteAsync($"/api/v1/admin/genres/{id}"); }
    }

    [Fact]
    public async Task AnOwnerCannotSetAGenreImage()
    {
        var id = await NewGenreAsync();
        try
        {
            var url = await UploadAsync();
            var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
                .PutAsJsonAsync($"/api/v1/admin/genres/{id}/image", new { ImageUrl = url });

            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally { await Admin().DeleteAsync($"/api/v1/admin/genres/{id}"); }
    }

    [Fact]
    public async Task UnknownGenreReturns404()
    {
        var url = await UploadAsync();
        var res = await Admin().PutAsJsonAsync($"/api/v1/admin/genres/{Guid.NewGuid()}/image", new { ImageUrl = url });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
