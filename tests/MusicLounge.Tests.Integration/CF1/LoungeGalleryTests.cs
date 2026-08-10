using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.CF1;

/// <summary>
/// Multi-photo gallery for a venue's detail page — distinct from PrimaryImageUrl (single hero
/// image, listings) and VenueTourScene (360° panoramas, subscription-gated). Free for every Owner.
/// POST/DELETE /api/v1/lounges/{id}/gallery[/{imageId}] | GET /api/v1/lounges/{id} (GalleryImages)
/// </summary>
[Collection("Integration")]
public sealed class LoungeGalleryTests
{
    private readonly ApiFactory _factory;

    public LoungeGalleryTests(ApiFactory factory) => _factory = factory;

    private sealed record IdResponse(bool Success, int Data);
    private sealed record UploadResponse(bool Success, UploadedUrl Data);
    private sealed record UploadedUrl(string Url);

    // AddLoungeGalleryImageCommandHandler now reads the image back off disk
    // (IImageModerationGate) — needs a real uploaded file, a fake "https://cdn.example.com/..."
    // URL 404s at that read.
    private async Task<string> UploadRealImageAsync(HttpClient client)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", $"gallery-{Guid.NewGuid():N}.png");

        var res = await client.PostAsync("/api/v1/uploads/images", form);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<UploadResponse>();
        return body!.Data.Url;
    }

    [Fact]
    public async Task AddGalleryImage_ByOwner_Returns201()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client);

        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery", new
        {
            ImageUrl = imageUrl,
            Caption = "Không gian tầng 1"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddGalleryImage_ByNonOwnerOfVenue_Returns403()
    {
        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");

        var res = await otherOwnerClient.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery", new
        {
            ImageUrl = "https://cdn.example.com/hijacked.jpg",
            Caption = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddGalleryImage_NoSubscriptionRequired_UnlikeTourScenes()
    {
        int freshOwnerId, freshLoungeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new MusicLounge.Domain.Entities.User
            {
                Id = 9400, Email = "gallery-owner@test.com", FullName = "Gallery Owner"
            };
            db.Users.Add(owner);
            var lounge = new MusicLounge.Domain.Entities.MusicLounge
            {
                Id = 9400, OwnerId = 9400, Name = "Gallery Lounge",
                Address = new MusicLounge.Domain.ValueObjects.VenueAddress { Street = "1 Test", District = "1", City = "HCM" }
            };
            db.Lounges.Add(lounge);
            await db.SaveChangesAsync();
            freshOwnerId = owner.Id;
            freshLoungeId = lounge.Id;
        }

        // No subscription seeded at all for this owner — unlike tour scenes, the gallery must
        // still work, since it's free marketing content, not a gated interactive feature.
        var client = _factory.CreateAuthenticatedClient(freshOwnerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client);
        var res = await client.PostAsJsonAsync($"/api/v1/lounges/{freshLoungeId}/gallery", new
        {
            ImageUrl = imageUrl,
            Caption = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetLoungeDetail_IncludesGalleryImagesInOrder()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var firstUrl = await UploadRealImageAsync(client);
        var secondUrl = await UploadRealImageAsync(client);
        await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery",
            new { ImageUrl = firstUrl, Caption = "First" });
        await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery",
            new { ImageUrl = secondUrl, Caption = "Second" });

        var res = await client.GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(firstUrl).And.Contain(secondUrl)
            .And.Contain("\"caption\":\"First\"").And.Contain("\"caption\":\"Second\"");
    }

    [Fact]
    public async Task RemoveGalleryImage_ByOwner_Returns204AndRemovesFromDetail()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var imageUrl = await UploadRealImageAsync(client);
        var addRes = await client.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery",
            new { ImageUrl = imageUrl, Caption = (string?)null });
        var imageId = (await addRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        var deleteRes = await client.DeleteAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery/{imageId}");

        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await client.GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}");
        (await detail.Content.ReadAsStringAsync()).Should().NotContain(imageUrl);
    }

    [Fact]
    public async Task RemoveGalleryImage_ByNonOwner_Returns403()
    {
        var ownerClient = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");
        var imageUrl = await UploadRealImageAsync(ownerClient);
        var addRes = await ownerClient.PostAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery",
            new { ImageUrl = imageUrl, Caption = (string?)null });
        var imageId = (await addRes.Content.ReadFromJsonAsync<IdResponse>())!.Data;

        var otherOwnerClient = _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner");
        var res = await otherOwnerClient.DeleteAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/gallery/{imageId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
