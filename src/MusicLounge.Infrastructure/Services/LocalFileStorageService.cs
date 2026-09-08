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
        => SaveAsync(content, originalFileName, UploadContentRules.ImageFolder, isImage: true, ct);

    public Task<string> SaveModel3DAsync(Stream content, string originalFileName, CancellationToken ct = default)
        => SaveAsync(content, originalFileName, UploadContentRules.Model3DFolder, isImage: false, ct);

    private async Task<string> SaveAsync(
        Stream content, string originalFileName, string subFolder, bool isImage, CancellationToken ct)
    {
        // Shared with FirebaseFileStorageService — see UploadContentRules for why the signature
        // check must not live inside one implementation.
        var extension = isImage
            ? await UploadContentRules.ValidateImageAsync(content, originalFileName, ct)
            : await UploadContentRules.ValidateModel3DAsync(content, originalFileName, ct);

        var uploadsDir = Path.Combine(_webRootPath, subFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return $"/{subFolder}/{fileName}";
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

        var contentType = UploadContentRules.ContentTypeFor(path);
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
