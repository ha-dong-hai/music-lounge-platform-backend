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

    private readonly string _webRootPath;

    public LocalFileStorageService()
    {
        _webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
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
}
