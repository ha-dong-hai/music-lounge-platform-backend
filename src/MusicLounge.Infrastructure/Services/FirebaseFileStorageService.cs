using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Settings;
using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// Stores uploads in Firebase Storage instead of the web process's own disk.
///
/// The reason this exists is not tidiness. App Service does not promise the local filesystem
/// survives a restart, a redeploy, or the platform moving the instance — so every venue photo, tour
/// panorama, 3D model and submitted ID document was one deployment away from disappearing while the
/// database went on holding a path to it. The failure would surface as broken images, long after
/// anyone could connect it to a deploy.
///
/// Two kinds of file, deliberately stored differently:
///
/// PUBLIC (venue photos, gallery, tour scenes, 3D models) get a Firebase download token written into
/// object metadata, which is what makes the returned firebasestorage.googleapis.com URL fetchable.
/// The bucket itself is never made world-readable, so nothing can be enumerated — a URL works only
/// if you were given it.
///
/// PRIVATE (citizen cards, business licences) are written under a separate prefix with NO download
/// token at all, so no URL exists that would serve them. The only way back to the bytes is
/// <see cref="OpenPrivateFileAsync"/>, which runs on the server's own credentials behind an
/// endpoint that checks who is asking. That is strictly stronger than the local-disk arrangement it
/// replaces, where privacy rested on the file sitting outside the static-file root.
/// </summary>
internal sealed class FirebaseFileStorageService : IFileStorageService
{
    private readonly StorageClient _storage;
    /// <summary>
    /// Khoá metadata mà Firebase Storage dùng để cấp quyền tải bằng URL. Một hằng số dùng chung cho
    /// cả nơi ĐẶT (SaveAsync) lẫn nơi XOÁ (RelocateToPrivateAsync): gõ tay hai chuỗi ở hai nơi thì
    /// chỗ xoá sẽ lặng lẽ không xoá gì, và không có gì báo lỗi — đúng kiểu hỏng đã xảy ra ở MLACP-486.
    /// </summary>
    private const string DownloadTokenKey = "firebaseStorageDownloadTokens";

    private readonly string _bucket;

    public FirebaseFileStorageService(IOptions<FirebaseSettings> settings)
    {
        var config = settings.Value;
        _bucket = config.StorageBucket;

        // Only ever constructed when DependencyInjection has already established that both settings
        // are present — see the registration there for why an unconfigured environment gets the
        // local-disk implementation rather than an exception at startup.
        //
        // Nêu thẳng loại credential thay vì để thư viện tự dò từ nội dung file. Google đã đánh dấu
        // GoogleCredential.FromFile là lỗi thời đúng vì lý do này: lời gọi tự dò sẽ nạp bất kỳ loại
        // credential nào file khai, nên ai thay được file đó thì ép được ứng dụng đi lấy token từ
        // một nguồn khác. Ở đây chỉ có duy nhất một loại hợp lệ, nên nói rõ ra là đúng.
        // FcmService đã dùng đúng lời gọi này từ trước — chỗ này bị bỏ sót.
        var credential = CredentialFactory.FromFile(config.CredentialsPath, "service_account")
            .CreateScoped(Google.Apis.Storage.v1.StorageService.Scope.DevstorageFullControl);
        _storage = StorageClient.Create(credential);
    }

    public Task<string> SaveImageAsync(
        Stream content, string originalFileName, CancellationToken ct = default)
        => SaveAsync(content, originalFileName, UploadContentRules.ImageFolder, isImage: true, ct);

    public Task<string> SaveModel3DAsync(
        Stream content, string originalFileName, CancellationToken ct = default)
        => SaveAsync(content, originalFileName, UploadContentRules.Model3DFolder, isImage: false, ct);

    private async Task<string> SaveAsync(
        Stream content, string originalFileName, string folder, bool isImage, CancellationToken ct)
    {
        // The same rules the local implementation applies, from the same place, so moving storage
        // to Firebase cannot quietly drop the signature check that stops a renamed executable.
        var extension = isImage
            ? await UploadContentRules.ValidateImageAsync(content, originalFileName, ct)
            : await UploadContentRules.ValidateModel3DAsync(content, originalFileName, ct);

        var objectName = $"{folder}/{Guid.NewGuid():N}{extension}";
        var downloadToken = Guid.NewGuid().ToString();

        await _storage.UploadObjectAsync(
            new GcsObject
            {
                Bucket = _bucket,
                Name = objectName,
                ContentType = UploadContentRules.ContentTypeFor(extension),
                // The one piece of metadata Firebase looks at: without a token here the download
                // URL below returns 403. Cùng hằng số với chỗ xoá token ở RelocateToPrivateAsync —
                // hai nơi gõ tay hai chuỗi khác nhau thì chỗ xoá sẽ lặng lẽ không xoá gì (MLACP-486).
                Metadata = new Dictionary<string, string> { [DownloadTokenKey] = downloadToken }
            },
            content, options: null, cancellationToken: ct);

        return BuildDownloadUrl(_bucket, objectName, downloadToken);
    }

    public async Task<string> RelocateToPrivateAsync(
        string publicUrl, CancellationToken ct = default)
    {
        var sourceObject = ResolveObjectName(publicUrl);
        var privateName = $"{UploadContentRules.PrivateFolder}/{Guid.NewGuid():N}{Path.GetExtension(sourceObject)}";

        try
        {
            // Copy rồi delete, vì GCS không có rename.
            await _storage.CopyObjectAsync(_bucket, sourceObject, _bucket, privateName,
                new CopyObjectOptions { DestinationPredefinedAcl = PredefinedObjectAcl.Private },
                ct);

            // MLACP-486: PHẢI xoá token bằng một lượt patch riêng.
            //
            // Bản trước chỉ copy rồi tin rằng bản đích "không có token", vì chú thích cũ hiểu
            // objects.copy là tạo một object trắng. Sai: tài liệu GCS ghi rõ "editable metadata from
            // the source object is applied to the destination object, with the exception of any
            // ACLs, object holds, or retention configuration" — tức firebaseStorageDownloadTokens
            // ĐI THEO sang bản riêng tư.
            //
            // Đo thật 23/09/2026 trên bucket production: upload ảnh → chuyển sang riêng tư → gọi
            // object private-uploads/… bằng CHÍNH token cũ trả HTTP 200 và tải đủ 927KB; không kèm
            // token thì 403. Nghĩa là ACL Private một mình KHÔNG chặn được đường tải bằng token.
            //
            // Đặt null cho một khoá metadata trong patch chính là cú pháp xoá khoá đó của GCS.
            await _storage.PatchObjectAsync(
                new GcsObject
                {
                    Bucket = _bucket,
                    Name = privateName,
                    Metadata = new Dictionary<string, string?> { [DownloadTokenKey] = null }!,
                },
                options: null, cancellationToken: ct);

            // Xoá bản gốc SAU CÙNG: nếu patch hỏng thì bản gốc còn nguyên và lời gọi ném lỗi, thay
            // vì để lại một object riêng tư vẫn mang token mà không ai biết.
            await _storage.DeleteObjectAsync(_bucket, sourceObject, options: null, cancellationToken: ct);
        }
        catch (Google.GoogleApiException)
        {
            throw new DomainException("Không tìm thấy ảnh đã upload — vui lòng upload lại.");
        }

        // Deliberately just the object name: callers store this and must not be able to hand it to
        // a browser and get the file.
        return privateName;
    }

    public async Task DeletePrivateFileAsync(string? privateRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(privateRef))
            return;

        // Pinned to the private prefix for the same reason OpenPrivateFileAsync pins it: the value
        // comes from a database column, and an erasure request must not be turnable into "delete any
        // object in the bucket" by a tampered row.
        var objectName = privateRef.StartsWith(UploadContentRules.PrivateFolder + "/", StringComparison.Ordinal)
            ? privateRef
            : $"{UploadContentRules.PrivateFolder}/{Path.GetFileName(privateRef)}";

        try
        {
            await _storage.DeleteObjectAsync(_bucket, objectName, options: null, cancellationToken: ct);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone. An erasure request must not fail because the file was removed by hand.
        }
    }

    public async Task<(Stream Content, string ContentType)> OpenPrivateFileAsync(
        string privateRef, CancellationToken ct = default)
    {
        // A stored reference should already be a private object name, but it arrives from a
        // database column, so it is pinned to the private prefix here rather than trusted. Without
        // this, a tampered row could name any object in the bucket and this endpoint would serve it.
        var objectName = privateRef.StartsWith(UploadContentRules.PrivateFolder + "/", StringComparison.Ordinal)
            ? privateRef
            : $"{UploadContentRules.PrivateFolder}/{Path.GetFileName(privateRef)}";

        var buffer = new MemoryStream();
        try
        {
            await _storage.DownloadObjectAsync(_bucket, objectName, buffer, options: null, cancellationToken: ct);
        }
        catch (Google.GoogleApiException)
        {
            throw new DomainException("Không tìm thấy ảnh.");
        }

        buffer.Seek(0, SeekOrigin.Begin);
        return (buffer, UploadContentRules.ContentTypeFor(objectName));
    }

    public async Task<byte[]> ReadPublicImageAsync(string publicUrl, CancellationToken ct = default)
    {
        var buffer = new MemoryStream();
        try
        {
            await _storage.DownloadObjectAsync(
                _bucket, ResolveObjectName(publicUrl), buffer, options: null, cancellationToken: ct);
        }
        catch (Google.GoogleApiException)
        {
            throw new DomainException("Không tìm thấy ảnh đã upload — vui lòng upload lại.");
        }

        return buffer.ToArray();
    }

    public bool IsOwnUploadUrl(string url) => IsOwnUploadUrl(url, _bucket);

    /// <summary>
    /// Chấp nhận đúng hai dạng: đường dẫn cục bộ cũ (dữ liệu ghi trước khi chuyển sang Firebase),
    /// và download URL của CHÍNH bucket này.
    ///
    /// Việc so bucket là phần quan trọng nhất. Chấp nhận mọi URL firebasestorage.googleapis.com sẽ
    /// mở lại đúng cái lỗ mà hàng rào này sinh ra để bịt, chỉ hẹp lại còn "bất kỳ nội dung nào ai đó
    /// đặt trong bất kỳ project Firebase nào" — vẫn là bắt server của mình đi tải hộ dữ liệu người
    /// lạ đưa.
    /// </summary>
    internal static bool IsOwnUploadUrl(string url, string bucket)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (url.StartsWith($"/{UploadContentRules.ImageFolder}/", StringComparison.Ordinal))
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return false;

        return parsed.Scheme == Uri.UriSchemeHttps
               && parsed.Host.Equals("firebasestorage.googleapis.com", StringComparison.OrdinalIgnoreCase)
               && parsed.AbsolutePath.StartsWith($"/v0/b/{bucket}/o/", StringComparison.Ordinal);
    }

    internal static string BuildDownloadUrl(string bucket, string objectName, string token)
        => $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/" +
           $"{Uri.EscapeDataString(objectName)}?alt=media&token={token}";

    /// <summary>
    /// Turns whatever is stored in a URL column back into a bucket object name.
    ///
    /// Handles the local-disk shape ("/uploads/x.png") as well as a Firebase download URL, because
    /// rows written before this change still hold the old shape. Object names are chosen to mirror
    /// the old paths exactly, so an old row resolves to the object it would have been uploaded as —
    /// and if that object was never migrated, the caller gets a clean "upload it again" rather than
    /// an unexplained failure.
    /// </summary>
    internal static string ResolveObjectName(string storedUrl)
    {
        if (string.IsNullOrWhiteSpace(storedUrl))
            throw new DomainException("Đường dẫn ảnh không hợp lệ.");

        const string marker = "/o/";
        var markerIndex = storedUrl.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            var encoded = storedUrl[(markerIndex + marker.Length)..];
            var queryIndex = encoded.IndexOf('?');
            if (queryIndex >= 0)
                encoded = encoded[..queryIndex];

            var name = Uri.UnescapeDataString(encoded);
            return string.IsNullOrWhiteSpace(name)
                ? throw new DomainException("Đường dẫn ảnh không hợp lệ.")
                : name;
        }

        // Legacy local path. Rebuilt from the folder and bare filename rather than by trimming the
        // string, so a crafted value like "/uploads/../../secrets.json" cannot name an object
        // outside the uploads prefix — the same defence the local implementation applies.
        var fileName = Path.GetFileName(storedUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Đường dẫn ảnh không hợp lệ.");

        var folder = storedUrl.Contains(UploadContentRules.Model3DFolder, StringComparison.OrdinalIgnoreCase)
            ? UploadContentRules.Model3DFolder
            : UploadContentRules.ImageFolder;

        return $"{folder}/{fileName}";
    }
}
