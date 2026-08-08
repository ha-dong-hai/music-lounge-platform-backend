using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Uploads;

/// <summary>
/// POST /api/v1/uploads/images — LocalFileStorageService now checks the actual file signature
/// (magic bytes), not just the claimed extension. Without this, a renamed executable/script with a
/// ".jpg" name would sail past the extension check and land in the publicly-served wwwroot/uploads
/// tree via UseStaticFiles().
/// </summary>
[Collection("Integration")]
public sealed class UploadImageTests
{
    private readonly ApiFactory _factory;

    public UploadImageTests(ApiFactory factory) => _factory = factory;

    private static MultipartFormDataContent BuildFileContent(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task UploadImage_ValidPngMagicBytes_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        using var content = BuildFileContent(pngBytes, "real.png");

        var res = await client.PostAsync("/api/v1/uploads/images", content);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadImage_PngExtensionWithNonImageContent_Returns422()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        var fakeBytes = Encoding.UTF8.GetBytes("this is not actually an image, just renamed to .png");
        using var content = BuildFileContent(fakeBytes, "malicious.png");

        var res = await client.PostAsync("/api/v1/uploads/images", content);

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UploadImage_NoFile_Returns400()
    {
        // UploadImageValidator (moved out of the controller) rejects a missing file.
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
        using var content = new MultipartFormDataContent();

        var res = await client.PostAsync("/api/v1/uploads/images", content);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
