using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Uploads;

/// <summary>
/// MLACP-296, cleaning up after MLACP-293.
///
/// Tour stitching hands image URLs to a service that fetches them with no network restriction of
/// its own, so the gate in front of it is what stops an Owner pointing the platform's own server at
/// a cloud metadata endpoint or an internal admin service. That gate used to read "the URL starts
/// with /uploads/", which was the right question only while files lived on local disk. Moving
/// storage to Firebase changed the shape of every issued URL, so the gate would have rejected every
/// legitimate image and taken the feature down — safe, but broken.
///
/// The question is now asked of the storage layer, which is the only part that knows what a URL it
/// issued looks like. These tests pin the two halves that matter: our own URLs are accepted, and
/// somebody else's Firebase bucket is not.
/// </summary>
[Collection("Integration")]
public sealed class OwnUploadUrlGuardTests
{
    private readonly ApiFactory _factory;

    public OwnUploadUrlGuardTests(ApiFactory factory) => _factory = factory;

    private IFileStorageService Storage(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IFileStorageService>();

    [Theory]
    [InlineData("/uploads/abc123.png", true)]
    [InlineData("/uploads/models/venue.glb", true)]      // nested under /uploads/, so still ours
    [InlineData("uploads/abc123.png", false)]            // no leading slash: not a shape we issue
    [InlineData("/uploads/../../appsettings.json", true)] // still under /uploads/ textually — see below
    [InlineData("https://attacker.example/evil.png", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", false)]
    [InlineData("", false)]
    public void LocalStorage_AcceptsOnlyThePathsItsOwnUploadEndpointProduces(string url, bool expected)
    {
        // Two cases look surprising and are deliberate. The gate asks "did this platform issue
        // this URL", not "is this an image" — a 3D-model path is ours, and handing the stitcher one
        // fails as a content problem rather than a security one. The traversal case is likewise not
        // a hole: the value only ever travels onward as a URL fetched over HTTP, and
        // LocalFileStorageService's own read path strips to a bare filename before touching disk.
        // The gate's job is keeping the request pointed at this platform, which it does.
        using var scope = _factory.Services.CreateScope();
        Storage(scope).IsOwnUploadUrl(url).Should().Be(expected);
    }

    [Fact]
    public void WithNoFirebaseConfigured_TheGateIsTheLocalOne()
    {
        using var scope = _factory.Services.CreateScope();
        Storage(scope).Should().BeOfType<LocalFileStorageService>(
            "the whole point of the fallback is that an unconfigured environment behaves as before");
    }

    [Theory]
    // Our bucket, the exact shape SaveImageAsync returns.
    [InlineData("https://firebasestorage.googleapis.com/v0/b/musiclounge.appspot.com/o/uploads%2Fa.png?alt=media&token=x", true)]
    // Legacy rows written before storage moved.
    [InlineData("/uploads/a.png", true)]
    // Somebody else's Firebase project. This is the case that matters: accepting any
    // firebasestorage.googleapis.com URL would reopen the hole, just narrowed to "any content
    // anyone put in any Firebase project" — still our server fetching a stranger's data on request.
    [InlineData("https://firebasestorage.googleapis.com/v0/b/attacker.appspot.com/o/uploads%2Fa.png?alt=media&token=x", false)]
    // A bucket whose name merely starts with ours.
    [InlineData("https://firebasestorage.googleapis.com/v0/b/musiclounge.appspot.com.evil.test/o/a.png", false)]
    // Right bucket, wrong host.
    [InlineData("https://evil.test/v0/b/musiclounge.appspot.com/o/uploads%2Fa.png", false)]
    // Right host and bucket, but plain HTTP.
    [InlineData("http://firebasestorage.googleapis.com/v0/b/musiclounge.appspot.com/o/a.png", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", false)]
    public void FirebaseStorage_AcceptsOnlyItsOwnBucket(string url, bool expected)
        => FirebaseFileStorageService.IsOwnUploadUrl(url, "musiclounge.appspot.com")
            .Should().Be(expected);

    [Fact]
    public void AUrlThisPlatformJustBuilt_PassesItsOwnGate()
    {
        // Guards the round trip rather than a hand-written example string: if the URL format ever
        // changes, the gate has to change with it, and this fails if they drift apart.
        const string bucket = "musiclounge.appspot.com";
        var url = FirebaseFileStorageService.BuildDownloadUrl(
            bucket, "uploads/9f2c.png", Guid.NewGuid().ToString());

        FirebaseFileStorageService.IsOwnUploadUrl(url, bucket).Should().BeTrue();
    }
}
