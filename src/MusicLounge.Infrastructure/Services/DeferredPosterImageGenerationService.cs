using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-458. "Nhà cung cấp" cho chế độ hàng đợi: nó không sinh ảnh, nó chỉ khai báo rằng ảnh sẽ đến muộn.
///
/// Google Flow chỉ nhận lệnh phát ra từ một tab trình duyệt đã đăng nhập, nên máy chủ không gọi thẳng được — một máy trạm
/// bên ngoài sẽ đến nhận đơn qua <c>/poster-jobs</c>. Lớp này tồn tại để chỗ CHỌN nhà cung cấp vẫn nằm nguyên ở
/// <c>DependencyInjection</c> như hai nhà cung cấp kia, thay vì nhét một nhánh "nếu là Flow thì..." vào handler.
/// </summary>
internal sealed class DeferredPosterImageGenerationService : IAiImageGenerationService
{
    public const string TenNhaCungCap = "flow";

    public bool IsDeferred => true;

    public string ProviderName => TenNhaCungCap;

    /// <summary>
    /// Không bao giờ được gọi: handler kiểm <see cref="IsDeferred"/> trước. Ném lỗi thay vì trả mảng rỗng — nếu có đường
    /// gọi mới nào quên kiểm, cái sai đó phải lộ ra ngay chứ không được biến thành một tấm poster trắng.
    /// </summary>
    public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
        => throw new InvalidOperationException(
            "Chế độ hàng đợi không sinh ảnh tại chỗ — đơn phải được ghi vào hàng đợi cho máy trạm xử lý.");
}
