using System.Text;
using FluentAssertions;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;

namespace MusicLounge.Tests.Integration.Uploads;

/// <summary>
/// MLACP-302, cleaning up the second thing MLACP-293 broke.
///
/// The mime type handed to image moderation was derived from the URL's file extension. That was the
/// right question only while files sat on local disk as "/uploads/abc.png". A Firebase URL ends
/// "...abc.png?alt=media&amp;token=..." and Path.GetExtension returns ".png?alt=media&amp;token=..."
/// — matching nothing, so it fell through to application/octet-stream.
///
/// The damage is not the wrong string. Gemini rejects that mime, the moderation service catches the
/// error and returns null, and the gate is deliberately fail-open ("unscored never blocks an
/// upload") — so the image gets published unmoderated. A content-safety control switched off by a
/// storage setting.
///
/// The gate's fail-open behaviour is left exactly as it was. It is a deliberate product decision for
/// when the moderation service is down, which is a different thing from failing open because the
/// system handed it garbage.
/// </summary>
public sealed class ModerationMimeTypeTests
{
    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static byte[] Gif() => Encoding.ASCII.GetBytes("GIF89a______");
    private static byte[] Webp()
    {
        var b = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(b, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(b, 8);
        return b;
    }

    [Fact]
    public void TheMimeTypeComesFromTheBytes_SoItDoesNotCareWhereTheFileIsStored()
    {
        // The whole point: no URL is involved any more, so a Firebase download URL and a local path
        // cannot produce different answers for the same image.
        ImageMimeTypeHelper.ForModeration(Png()).Should().Be("image/png");
        ImageMimeTypeHelper.ForModeration(Jpeg()).Should().Be("image/jpeg");
        ImageMimeTypeHelper.ForModeration(Gif()).Should().Be("image/gif");
        ImageMimeTypeHelper.ForModeration(Webp()).Should().Be("image/webp");
    }

    [Fact]
    public void ARealImage_NeverResolvesToOctetStream()
    {
        // octet-stream is precisely the value that made Gemini refuse the request, which made the
        // gate fail open. Any real image reaching that value is the bug reappearing.
        foreach (var image in new[] { Png(), Jpeg(), Gif(), Webp() })
            ImageMimeTypeHelper.ForModeration(image).Should().NotBe("application/octet-stream");
    }

    [Fact]
    public void SomethingThatIsNotAnImage_IsReportedAsUnknown_NotGuessed()
    {
        var notAnImage = Encoding.UTF8.GetBytes("<?php system($_GET['c']); ?>");

        ImageMimeTypeHelper.FromContent(notAnImage).Should().BeNull();
        ImageMimeTypeHelper.ForModeration(notAnImage).Should().Be("application/octet-stream",
            "letting the moderation service refuse it is better than claiming it is a PNG");
    }

    [Fact]
    public void TruncatedContent_DoesNotThrow()
    {
        // The bytes arrive from storage; a short or empty read must not take the request down.
        ImageMimeTypeHelper.FromContent([]).Should().BeNull();
        ImageMimeTypeHelper.FromContent([0x89, 0x50]).Should().BeNull();
        ImageMimeTypeHelper.ForModeration([]).Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task TheUploadValidator_StillUsesTheSameTable_AndStillRejectsARenamedFile()
    {
        // The signature table now has one home, shared by upload validation and moderation. This is
        // the check that stops a renamed executable, so moving it must not have loosened it.
        using var renamed = new MemoryStream(Encoding.UTF8.GetBytes("not a png at all, just renamed"));
        var act = async () => await UploadContentRules.ValidateImageAsync(renamed, "payload.png", default);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*không khớp*");

        using var real = new MemoryStream(Png());
        (await UploadContentRules.ValidateImageAsync(real, "photo.png", default)).Should().Be(".png");
    }

    [Fact]
    public async Task ContentThatIsARealImage_ButNotTheDeclaredOne_IsAlsoRejected()
    {
        // Not a new capability — the previous implementation rejected this too, by checking the
        // declared extension's own signature. Pinned because the rewrite routes the check through a
        // shared table and a cross-format mismatch is the case most likely to be loosened by that.
        using var jpegNamedPng = new MemoryStream(Jpeg());

        var act = async () =>
            await UploadContentRules.ValidateImageAsync(jpegNamedPng, "actually-a-jpeg.png", default);

        await act.Should().ThrowAsync<DomainException>();
    }
}
