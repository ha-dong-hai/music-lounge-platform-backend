using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// What counts as an acceptable upload, in one place, because there is now more than one
/// <see cref="Application.Common.Interfaces.IFileStorageService"/> implementation and these rules
/// are the only thing standing between the platform and a renamed executable.
///
/// Extracted for MLACP-293. The check that matters is the signature sniff: a filename extension is
/// a hint the caller fully controls, so a script renamed to ".jpg" passes an extension check and
/// then sits in whatever the platform serves publicly. That protection was written into the
/// local-disk implementation. Had the Firebase implementation been written with its own upload
/// logic, switching storage over would have silently removed it — a security regression caused by a
/// deployment setting rather than by any code change to the check itself.
/// </summary>
internal static class UploadContentRules
{
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    // Chỉ nhận .glb (binary, tự chứa hết buffer/texture) — .gltf (JSON) thường tham chiếu file
    // .bin/texture riêng qua đường dẫn tương đối, mà flow upload 1-file này không thể mang theo
    // các file đi kèm đó, nên sẽ load lỗi âm thầm (rồi fallback về scene mẫu) nếu cho phép.
    private static readonly HashSet<string> AllowedModel3DExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".glb" };

    private static readonly Dictionary<string, string> ContentTypesByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
            [".webp"] = "image/webp", [".gif"] = "image/gif", [".glb"] = "model/gltf-binary"
        };

    public const string ImageFolder = "uploads";
    public const string Model3DFolder = "uploads/models";

    /// <summary>Prefix for files that must never have a public URL of any kind — ID documents.</summary>
    public const string PrivateFolder = "private-uploads";

    private const string ImageError = "Chỉ chấp nhận ảnh định dạng jpg, jpeg, png, webp, gif.";
    private const string Model3DError =
        "Chỉ chấp nhận file mô hình 3D định dạng .glb (binary, tự chứa toàn bộ dữ liệu).";

    public static string ContentTypeFor(string fileNameOrExtension)
        => ContentTypesByExtension.GetValueOrDefault(
            Path.GetExtension(fileNameOrExtension), "application/octet-stream");

    /// <summary>
    /// Validates the upload and returns the normalised extension to store it under. Throws
    /// <see cref="DomainException"/> — mapped to 422 — when the file is not what it claims to be.
    /// </summary>
    public static async Task<string> ValidateImageAsync(
        Stream content, string originalFileName, CancellationToken ct)
        => await ValidateAsync(content, originalFileName, AllowedImageExtensions, ImageError, ct);

    public static async Task<string> ValidateModel3DAsync(
        Stream content, string originalFileName, CancellationToken ct)
        => await ValidateAsync(content, originalFileName, AllowedModel3DExtensions, Model3DError, ct);

    private static async Task<string> ValidateAsync(
        Stream content, string originalFileName, HashSet<string> allowedExtensions,
        string errorMessage, CancellationToken ct)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            throw new DomainException(errorMessage);

        if (!await HasValidMagicBytesAsync(content, extension, ct))
            throw new DomainException(
                "Nội dung file không khớp với định dạng đã khai báo — file có thể bị đổi tên hoặc hỏng.");

        return extension.ToLowerInvariant();
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static async Task<bool> HasValidMagicBytesAsync(
        Stream content, string extension, CancellationToken ct)
    {
        if (!content.CanSeek)
            throw new DomainException("Không thể xử lý file này.");

        var header = new byte[12];
        var bytesRead = await content.ReadAsync(header, ct);
        content.Seek(0, SeekOrigin.Begin);

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 && header.AsSpan(0, 8).SequenceEqual(PngSignature),
            ".gif" => bytesRead >= 6 &&
                (header.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || header.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
            ".webp" => bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".glb" => bytesRead >= 4 && header.AsSpan(0, 4).SequenceEqual("glTF"u8),
            _ => false
        };
    }
}
