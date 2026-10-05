namespace MusicLounge.Application.Common.Interfaces;

public interface ILivestreamService
{
    Task<LivestreamProviderResult> CreateStreamAsync(string name, CancellationToken ct = default);
    Task DeleteStreamAsync(string providerRef, CancellationToken ct = default);

    /// <summary>MLACP-647. Tạo luồng cho buổi phát CÓ PHÍ (<paramref name="paidViewing"/>): nhà cung cấp hỗ trợ link xem có
    /// chữ ký thì tạo luồng chỉ phát được khi có token. Mặc định: như <see cref="CreateStreamAsync(string, CancellationToken)"/>.</summary>
    Task<LivestreamProviderResult> CreateStreamAsync(string name, bool paidViewing, CancellationToken ct = default)
        => CreateStreamAsync(name, ct);

    /// <summary>MLACP-647. Đường dẫn phát đưa cho MỘT người đã được xác nhận quyền xem, còn hạn tới
    /// <paramref name="validUntil"/>. Luồng có chữ ký thì kèm token; còn lại trả nguyên <paramref name="storedHlsUrl"/>.</summary>
    string ViewerPlaybackUrl(string storedHlsUrl, DateTimeOffset validUntil) => storedHlsUrl;
}

public sealed record LivestreamProviderResult(
    string ProviderRef,
    string RtmpUrl,
    string StreamKey,
    string HlsUrl);
