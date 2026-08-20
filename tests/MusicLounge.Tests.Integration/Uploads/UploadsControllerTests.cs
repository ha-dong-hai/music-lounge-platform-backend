using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using MusicLounge.Api.Controllers;

namespace MusicLounge.Tests.Integration.Uploads;

/// <summary>
/// master-backend-techlead review: UploadModel3D had [RequestSizeLimit(MaxModel3DSizeBytes)] as
/// defense-in-depth (rejects an oversized request at the framework level before it's fully
/// buffered), but the sibling UploadImage endpoint relied only on an app-level `file.Length >
/// MaxImageSizeBytes` check performed AFTER model binding had already read the whole body —
/// under Kestrel's default ~28.6MB request body limit, that's up to ~5.7x the intended 5MB cap
/// buffered before rejection. Reflection-based rather than a live oversized-upload test: TestServer's
/// fidelity to Kestrel's actual body-size enforcement isn't guaranteed, so asserting the attribute
/// itself is present with the right value is the deterministic way to verify this fix.
/// </summary>
// No ApiFactory/DB access today, but marked into the shared collection anyway (xunit runs
// [Collection]-less classes in parallel with everything else by default) so a future edit adding
// either doesn't silently start racing the rest of the suite's shared SQLite connection.
[Collection("Integration")]
public sealed class UploadsControllerTests
{
    [Fact]
    public void UploadImage_HasRequestSizeLimitAttribute()
    {
        var method = typeof(UploadsController).GetMethod(nameof(UploadsController.UploadImage))!;
        var attribute = method.GetCustomAttribute<RequestSizeLimitAttribute>();

        attribute.Should().NotBeNull(
            "UploadImage must reject oversized bodies at the framework level, the same way " +
            "UploadModel3D already does, instead of fully buffering the request before the " +
            "app-level file.Length check runs");
    }
}
