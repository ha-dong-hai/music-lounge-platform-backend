using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Lounges;

/// <summary>
/// MLACP-292. Three columns that existed, were read, and could never be written: a venue's business
/// licence, its 3D model, and a show's playback mode. Alongside them sat
/// IFileStorageService.SaveModel3DAsync and UploadModel3DValidator — both fully implemented, both
/// called by nothing. Same class of defect as the D13 refund-policy columns in MLACP-288.
///
/// The business licence needed more than an assignment. BusinessLicenseUrl is already returned by
/// LoungeListItemDto, and GET /lounges is AllowAnonymous — harmless only for as long as the column
/// stayed null. Writing a public upload URL into it would have made this very change the thing that
/// published every venue's business licence, so the file is moved into private storage on the way
/// in, exactly as a citizen card is.
/// </summary>
[Collection("Integration")]
public sealed class DeadColumnWritePathTests
{
    private readonly ApiFactory _factory;

    public DeadColumnWritePathTests(ApiFactory factory) => _factory = factory;

    private HttpClient Owner() => _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner");

    private static MultipartFormDataContent FileForm(byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    /// <summary>Uploads a real file the honest way and returns the public URL the API hands back.</summary>
    private async Task<string> UploadPublicImageAsync()
    {
        using var content = FileForm(Png(), $"licence-{Guid.NewGuid():N}.png", "image/png");
        var res = await Owner().PostAsync("/api/v1/uploads/images", content);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<Envelope<UploadResponse>>())!.Data.Url;
    }

    private async Task<MusicLounge.Domain.Entities.MusicLounge> ReadLoungeAsync(int loungeId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<MusicLounge.Domain.Entities.MusicLounge>().SingleAsync(l => l.Id == loungeId);
    }

    private async Task<int> SeedShowAsync(LoungeShowFormat format, LoungeShowStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var show = new LoungeShow
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"PlaybackShow-{Guid.NewGuid():N}",
            Description = "Integration test show",
            Format = format,
            Status = status,
            ScheduledStart = DateTimeOffset.UtcNow.AddDays(7),
            ScheduledEnd = DateTimeOffset.UtcNow.AddDays(7).AddHours(3)
        };
        db.LoungeShows.Add(show);
        await db.SaveChangesAsync();
        return show.Id;
    }

    // ---------- giấy phép kinh doanh ----------

    [Fact]
    public async Task BusinessLicence_IsMovedOutOfPublicStorage_NotJustRecorded()
    {
        var publicUrl = await UploadPublicImageAsync();
        publicUrl.Should().StartWith("/uploads/", "test premise: it starts life publicly servable");

        var res = await Owner().PutAsJsonAsync(
            $"/api/v1/lounges/{SeedHelper.LoungeId}/business-license",
            new { DocumentUrl = publicUrl });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = (await ReadLoungeAsync(SeedHelper.LoungeId)).BusinessLicenseUrl;

        stored.Should().NotBeNullOrWhiteSpace();
        stored.Should().NotBe(publicUrl);
        stored.Should().NotStartWith("/uploads/",
            "GET /lounges is anonymous and returns this column, so a public path here would hand " +
            "every venue's business licence to anyone who lists venues");
    }

    [Fact]
    public async Task BusinessLicence_IsReadableByItsOwnerAndByAdmin_ButNotByAnotherVenueOwner()
    {
        await Owner().PutAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license",
            new { DocumentUrl = await UploadPublicImageAsync() });

        var mine = await Owner().GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mine.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();

        var admin = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license");
        admin.StatusCode.Should().Be(HttpStatusCode.OK,
            "an Admin has to see the licence to approve the venue at all");

        var otherOwner = await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
            .GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license");
        otherOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var anonymous = await _factory.CreateClient()
            .GetAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PublicVenueList_KeepsItsShape_AndLeaksNoPubliclyFetchableLicence()
    {
        await Owner().PutAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license",
            new { DocumentUrl = await UploadPublicImageAsync() });

        var res = await _factory.CreateClient().GetAsync("/api/v1/lounges?pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<Envelope<Paged<LoungeListItem>>>();
        var seeded = body!.Data.Items.SingleOrDefault(l => l.Id == SeedHelper.LoungeId);

        seeded.Should().NotBeNull("removing the field would be a response-shape change for clients");
        seeded!.BusinessLicenseUrl.Should().NotStartWith("/uploads/",
            "whatever this column now holds must not be something a stranger can simply fetch");
    }

    [Fact]
    public async Task AnotherVenueOwner_CannotSetSomeoneElsesLicence()
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OtherOwnerId, "Owner")
            .PutAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/business-license",
                new { DocumentUrl = "/uploads/whatever.png" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- mô hình 3D ----------

    [Fact]
    public async Task Model3D_CanBeSetAndThenCleared()
    {
        var set = await Owner().PutAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/model-3d",
            new { ModelUrl = "/uploads/venue-model.glb" });
        set.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadLoungeAsync(SeedHelper.LoungeId)).Model3DUrl.Should().Be("/uploads/venue-model.glb");

        var clear = await Owner().PutAsJsonAsync($"/api/v1/lounges/{SeedHelper.LoungeId}/model-3d",
            new { ModelUrl = (string?)null });
        clear.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadLoungeAsync(SeedHelper.LoungeId)).Model3DUrl.Should().BeNull(
            "clearing it is how a venue goes back to the code-drawn sample scene");
    }

    [Fact]
    public async Task UploadingA3DModel_AcceptsARealGlb_AndRejectsSomethingRenamedToLookLikeOne()
    {
        // The validator and the storage method for this both already existed and had never been
        // reachable; this is the first test that can prove either of them runs.
        byte[] glb = [0x67, 0x6C, 0x54, 0x46, 0x02, 0, 0, 0, 0, 0, 0, 0];   // "glTF"
        using var real = FileForm(glb, "venue.glb", "model/gltf-binary");
        (await Owner().PostAsync("/api/v1/uploads/models", real)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        using var fake = FileForm(Encoding.UTF8.GetBytes("not a model at all"), "venue.glb",
            "model/gltf-binary");
        (await Owner().PostAsync("/api/v1/uploads/models", fake)).StatusCode
            .Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- hình thức phát ----------

    [Fact]
    public async Task PlaybackMode_CanBeSetOnAnOnlineShow_AndTheDetailPageShowsIt()
    {
        var showId = await SeedShowAsync(LoungeShowFormat.Online, LoungeShowStatus.Published);

        var res = await Owner().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/playback-mode",
            new { PlaybackMode = "ThreeD" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await _factory.CreateClient().GetAsync($"/api/v1/lounge-shows/{showId}");
        var body = await detail.Content.ReadFromJsonAsync<Envelope<ShowDetail>>();
        body!.Data.PlaybackMode.Should().Be("ThreeD",
            "the client reads this to decide which player to build, which is the whole point of " +
            "being able to set it");
    }

    [Fact]
    public async Task PlaybackMode_ThreeD_IsRejectedForAnOfflineShow()
    {
        var showId = await SeedShowAsync(LoungeShowFormat.Offline, LoungeShowStatus.Published);

        var res = await Owner().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/playback-mode",
            new { PlaybackMode = "ThreeD" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "an offline show has no stream to render in 3D");
    }

    [Fact]
    public async Task PlaybackMode_IsRejectedOnceTheShowHasReachedItsEnd()
    {
        var showId = await SeedShowAsync(LoungeShowFormat.Online, LoungeShowStatus.Cancelled);

        var res = await Owner().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/playback-mode",
            new { PlaybackMode = "TwoD" });

        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PlaybackMode_RejectsAValueThatIsNotAPlaybackMode()
    {
        var showId = await SeedShowAsync(LoungeShowFormat.Online, LoungeShowStatus.Published);

        var res = await Owner().PutAsJsonAsync($"/api/v1/lounge-shows/{showId}/playback-mode",
            new { PlaybackMode = "FourD" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("ThreeD",
            "the message should name the values that would work");
    }

    private sealed record Envelope<T>(bool Success, T Data);
    private sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    private sealed record UploadResponse(string Url);
    private sealed record LoungeListItem(int Id, string Name, string? BusinessLicenseUrl);
    private sealed record ShowDetail(int Id, string Name, string PlaybackMode);
}
