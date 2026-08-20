using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// Luu file len dia cuc bo (wwwroot/uploads), phuc vu qua app.UseStaticFiles() (Program.cs).
/// Khong can credential ngoai — day la lua chon mac dinh cho moi truong dev/self-host hien tai.
/// De doi sang S3/Azure Blob sau nay, chi can them implementation moi cua IFileStorageService.
/// </summary>
internal sealed class LocalFileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    // Chi nhan .glb (binary, tu chua het buffer/texture) — .gltf (JSON) thuong tham chieu file
    // .bin/texture rieng qua duong dan tuong doi, ma flow upload 1-file nay khong the mang theo
    // cac file di kem do, nen se load loi am tham (roi fallback ve scene mau) neu cho phep.
    private static readonly HashSet<string> AllowedModel3DExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".glb" };

    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".webp"] = "image/webp", [".gif"] = "image/gif"
    };

    private readonly string _webRootPath;
    private readonly string _privateRootPath;

    public LocalFileStorageService()
    {
        _webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        // Outside wwwroot on purpose — UseStaticFiles() only serves the wwwroot tree, so anything
        // stored here can only ever reach a client through an authenticated controller action that
        // explicitly reads it back via OpenPrivateFileAsync, never by guessing/leaking a URL.
        _privateRootPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "private-uploads");
    }

    public Task<string> SaveImageAsync(Stream content, string originalFileName, CancellationToken ct = default)
        => SaveAsync(content, originalFileName, AllowedImageExtensions, "uploads",
            "Chỉ chấp nhận ảnh định dạng jpg, jpeg, png, webp, gif.", ct);

    public Task<string> SaveModel3DAsync(Stream content, string originalFileName, CancellationToken ct = default)
        => SaveAsync(content, originalFileName, AllowedModel3DExtensions, "uploads/models",
            "Chỉ chấp nhận file mô hình 3D định dạng .glb (binary, tự chứa toàn bộ dữ liệu).", ct);

    private async Task<string> SaveAsync(
        Stream content, string originalFileName, HashSet<string> allowedExtensions,
        string subFolder, string errorMessage, CancellationToken ct)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            throw new DomainException(errorMessage);

        // Extension alone is just a filename hint an attacker fully controls — a renamed
        // executable/script with a ".jpg" name would sail through the check above and land in
        // wwwroot/uploads, publicly served by UseStaticFiles(). Sniff the actual file signature
        // before trusting the claimed type.
        if (!await HasValidMagicBytesAsync(content, extension, ct))
            throw new DomainException(
                "Nội dung file không khớp với định dạng đã khai báo — file có thể bị đổi tên hoặc hỏng.");

        var uploadsDir = Path.Combine(_webRootPath, subFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return $"/{subFolder}/{fileName}";
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static async Task<bool> HasValidMagicBytesAsync(Stream content, string extension, CancellationToken ct)
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

    public Task<string> RelocateToPrivateAsync(string publicUrl, CancellationToken ct = default)
    {
        // publicUrl is always our own "/uploads/xxxx.ext" shape from SaveImageAsync above — never
        // trust it as an arbitrary path. Strip to the bare filename so a crafted value like
        // "/uploads/../../appsettings.json" can't be used to relocate (and thus read back via
        // OpenPrivateFileAsync) a file outside the uploads folder.
        var fileName = Path.GetFileName(publicUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Đường dẫn ảnh không hợp lệ.");

        var sourcePath = Path.Combine(_webRootPath, "uploads", fileName);
        if (!File.Exists(sourcePath))
            throw new DomainException("Không tìm thấy ảnh đã upload — vui lòng upload lại.");

        Directory.CreateDirectory(_privateRootPath);
        var privateRef = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        File.Move(sourcePath, Path.Combine(_privateRootPath, privateRef), overwrite: false);

        return Task.FromResult(privateRef);
    }

    public Task<(Stream Content, string ContentType)> OpenPrivateFileAsync(
        string privateRef, CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(privateRef);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Đường dẫn ảnh không hợp lệ.");

        var path = Path.Combine(_privateRootPath, fileName);
        if (!File.Exists(path))
            throw new DomainException("Không tìm thấy ảnh.");

        var contentType = ContentTypesByExtension.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult((stream, contentType));
    }

    public async Task<byte[]> ReadPublicImageAsync(string publicUrl, CancellationToken ct = default)
    {
        // Same "strip to bare filename" defense as RelocateToPrivateAsync — publicUrl always has
        // our own "/uploads/xxxx.ext" shape from SaveImageAsync, never trusted as an arbitrary path.
        var fileName = Path.GetFileName(publicUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Đường dẫn ảnh không hợp lệ.");

        var path = Path.Combine(_webRootPath, "uploads", fileName);
        if (!File.Exists(path))
            throw new DomainException("Không tìm thấy ảnh đã upload — vui lòng upload lại.");

        return await File.ReadAllBytesAsync(path, ct);
    }
}
