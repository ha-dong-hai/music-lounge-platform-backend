using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using MetadataExtractor.Formats.Xmp;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-433. Dung MetadataExtractor (Apache-2.0, ban .NET cua thu vien metadata-extractor) thay vi tu doc byte: chi
/// doc phan dau file nen nhe, va da xu ly san cac bien the de sai khi tu viet — JPEG progressive (SOF2), WebP ca ba
/// dang VP8 / VP8L / VP8X, the EXIF Orientation.
///
/// Chi nhan cac dinh dang ma khau upload cho qua (UploadContentRules): JPEG, PNG, GIF, WebP.
/// </summary>
internal sealed class MetadataImageSizeReader : IImageSizeReader
{
    private const string GPanoNamespace = "http://ns.google.com/photos/1.0/panorama/";

    public ImageSize? ReadDisplaySize(byte[] content)
    {
        if (DocMetadata(content) is not { } directories) return null;

        var size = Doc<JpegDirectory>(directories, JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight)
                   ?? Doc<PngDirectory>(directories, PngDirectory.TagImageWidth, PngDirectory.TagImageHeight)
                   ?? Doc<WebPDirectory>(directories, WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight)
                   ?? Doc<GifHeaderDirectory>(directories, GifHeaderDirectory.TagImageWidth, GifHeaderDirectory.TagImageHeight);
        if (size is not { } s) return null;

        // Orientation 5-8 = xoay 90 do (co the kem lat): chieu hien thi doi cho rong/cao.
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        return ifd0 is not null
               && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation)
               && orientation is >= 5 and <= 8
            ? new ImageSize(s.Height, s.Width)
            : s;
    }

    public double? ReadGPanoHorizontalCoverageDegrees(byte[] content)
    {
        if (DocMetadata(content) is not { } directories) return null;

        // XmpCore phan tich RDF day du, nen doc duoc ca hai kieu ghi GPano ngoai doi: thuoc tinh
        // (GPano:FullPanoWidthPixels="...") va phan tu con (<GPano:FullPanoWidthPixels>...</...>) — anh panorama that
        // trong kho upload local dung kieu phan tu con.
        foreach (var xmp in directories.OfType<XmpDirectory>())
        {
            var meta = xmp.XmpMeta;
            if (meta is null
                || !meta.DoesPropertyExist(GPanoNamespace, "FullPanoWidthPixels")
                || !meta.DoesPropertyExist(GPanoNamespace, "CroppedAreaImageWidthPixels"))
                continue;

            int full, cropped;
            try
            {
                full = meta.GetPropertyInteger(GPanoNamespace, "FullPanoWidthPixels");
                cropped = meta.GetPropertyInteger(GPanoNamespace, "CroppedAreaImageWidthPixels");
            }
            catch (XmpCore.XmpException)
            {
                continue; // gia tri khong phai so — metadata hong, coi nhu khong co
            }

            if (full > 0 && cropped > 0)
                return Math.Min(360.0, 360.0 * cropped / full);
        }

        return null;
    }

    private static IReadOnlyList<MetadataExtractor.Directory>? DocMetadata(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            return ImageMetadataReader.ReadMetadata(stream);
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException)
        {
            return null;
        }
    }

    private static ImageSize? Doc<T>(IEnumerable<MetadataExtractor.Directory> directories, int tagWidth, int tagHeight)
        where T : MetadataExtractor.Directory
    {
        var dir = directories.OfType<T>().FirstOrDefault();
        return dir is not null
               && dir.TryGetInt32(tagWidth, out var width) && width > 0
               && dir.TryGetInt32(tagHeight, out var height) && height > 0
            ? new ImageSize(width, height)
            : null;
    }
}
