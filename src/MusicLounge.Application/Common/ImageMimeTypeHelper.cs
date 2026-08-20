namespace MusicLounge.Application.Common;

// Small shared helper for the two upload handlers (AddVenueTourScene, AddLoungeGalleryImage) that
// need a mime type for IImageModerationGate from an already-validated "/uploads/xxx.ext" URL —
// LocalFileStorageService's own magic-bytes check already confirmed the extension is trustworthy
// at upload time, so a simple extension lookup here is enough (no need to re-sniff file content).
public static class ImageMimeTypeHelper
{
    public static string FromUrl(string url) => Path.GetExtension(url).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}
