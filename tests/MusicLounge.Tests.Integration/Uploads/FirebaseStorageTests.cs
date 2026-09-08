using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Uploads;

/// <summary>
/// MLACP-293. Uploads lived on the web process's own disk, which App Service does not promise to
/// keep across a restart, a redeploy, or the platform moving the instance — so venue photos, tour
/// panoramas, 3D models and submitted ID documents were one deployment away from vanishing while
/// the database went on holding paths to them.
///
/// What can be asserted here without real Google credentials is the part that would actually go
/// wrong in this change: that an environment with no Firebase secret still works, that the file
/// signature check survived being pulled out of one implementation and shared by two, and that a
/// stored URL cannot be turned into a reference to some other object in the bucket.
/// </summary>
[Collection("Integration")]
public sealed class FirebaseStorageTests
{
    private readonly ApiFactory _factory;

    public FirebaseStorageTests(ApiFactory factory) => _factory = factory;

    // ---------- an environment with no secret has to keep working ----------

    [Fact]
    public void WithNoFirebaseSecret_TheAppFallsBackToLocalDisk_RatherThanFailingToStart()
    {
        // Every developer machine and every CI run is this case. Throwing instead would turn a
        // missing optional secret into a dead test suite — the same reasoning FcmService and
        // SmsService already apply to their own missing credentials.
        FileStorageSelector.UseFirebase(new FirebaseSettings()).Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFileStorageService>()
            .Should().BeOfType<LocalFileStorageService>();
    }

    [Theory]
    [InlineData("", "", false)]
    [InlineData("/secrets/firebase.json", "", false)]
    [InlineData("", "musiclounge.appspot.com", false)]
    [InlineData("   ", "  ", false)]
    [InlineData("/secrets/firebase.json", "musiclounge.appspot.com", true)]
    public void FirebaseIsUsedOnlyWhenBothTheCredentialAndTheBucketAreConfigured(
        string credentialsPath, string bucket, bool expected)
    {
        // Half-configured is the dangerous state: a bucket with no credential would authenticate as
        // nobody, and a credential with no bucket has nowhere to put anything. Either way the
        // failure would appear at the first upload, not at startup.
        FileStorageSelector.UseFirebase(
                new FirebaseSettings { CredentialsPath = credentialsPath, StorageBucket = bucket })
            .Should().Be(expected);
    }

    // ---------- the signature check must survive being shared ----------

    [Fact]
    public async Task RenamedFile_IsStillRejected_NowThatTheRuleIsSharedByBothBackends()
    {
        // This check is the only thing stopping a renamed executable reaching whatever the platform
        // serves. It used to live inside the local-disk implementation; had the Firebase one been
        // written with its own upload logic, switching storage would have removed it — a security
        // regression caused by a deployment setting rather than by any edit to the check.
        using var renamed = new MemoryStream(Encoding.UTF8.GetBytes("this is not a png at all"));

        var act = async () => await UploadContentRules.ValidateImageAsync(renamed, "payload.png", default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*không khớp*");
    }

    [Fact]
    public async Task RealFileSignatures_AreAccepted_AndTheExtensionIsNormalised()
    {
        using var png = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        (await UploadContentRules.ValidateImageAsync(png, "Photo.PNG", default)).Should().Be(".png");

        using var glb = new MemoryStream([0x67, 0x6C, 0x54, 0x46, 0x02, 0, 0, 0, 0, 0, 0, 0]);
        (await UploadContentRules.ValidateModel3DAsync(glb, "Venue.GLB", default)).Should().Be(".glb");
    }

    [Fact]
    public async Task AnImageExtensionIsNotAcceptedForA3DModel_AndViceVersa()
    {
        using var png = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);
        var asModel = async () => await UploadContentRules.ValidateModel3DAsync(png, "photo.png", default);
        await asModel.Should().ThrowAsync<DomainException>();

        using var glb = new MemoryStream([0x67, 0x6C, 0x54, 0x46, 0x02, 0, 0, 0, 0, 0, 0, 0]);
        var asImage = async () => await UploadContentRules.ValidateImageAsync(glb, "venue.glb", default);
        await asImage.Should().ThrowAsync<DomainException>();
    }

    // ---------- a stored URL must not be able to name an arbitrary object ----------

    [Fact]
    public void ADownloadUrlResolvesBackToExactlyTheObjectItWasBuiltFrom()
    {
        const string bucket = "musiclounge.appspot.com";
        const string objectName = "uploads/models/abc123.glb";

        var url = FirebaseFileStorageService.BuildDownloadUrl(bucket, objectName, Guid.NewGuid().ToString());

        url.Should().StartWith($"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/");
        url.Should().Contain("alt=media").And.Contain("token=");
        // The slashes in the object name are escaped, which is what makes the /o/ segment parseable.
        url.Should().Contain("uploads%2Fmodels%2Fabc123.glb");

        FirebaseFileStorageService.ResolveObjectName(url).Should().Be(objectName);
    }

    [Fact]
    public void RowsWrittenBeforeThisChange_StillResolveToTheObjectTheyWouldHaveBecome()
    {
        // Object names deliberately mirror the old local paths, so a row holding "/uploads/x.png"
        // points at the object that file would have been uploaded as — and if it was never
        // migrated, the caller gets a clean "upload it again" instead of an unexplained failure.
        FirebaseFileStorageService.ResolveObjectName("/uploads/x.png").Should().Be("uploads/x.png");
        FirebaseFileStorageService.ResolveObjectName("/uploads/models/v.glb")
            .Should().Be("uploads/models/v.glb");
    }

    [Theory]
    [InlineData("/uploads/../../appsettings.json")]
    [InlineData("/uploads/../private-uploads/citizen-card.png")]
    public void ATraversalAttemptCannotNameAnObjectOutsideTheUploadsPrefix(string crafted)
    {
        // These columns are read back out of the database, so the value is not automatically ours.
        var resolved = FirebaseFileStorageService.ResolveObjectName(crafted);

        resolved.Should().StartWith("uploads/");
        resolved.Should().NotContain("..");
        resolved.Should().NotContain("private-uploads/",
            "the private prefix is where ID documents live — a public read path must never reach it");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://firebasestorage.googleapis.com/v0/b/bucket/o/?alt=media")]
    public void AnEmptyOrShapelessReferenceIsRefused(string stored)
    {
        var act = () => FirebaseFileStorageService.ResolveObjectName(stored);

        act.Should().Throw<DomainException>();
    }
}
