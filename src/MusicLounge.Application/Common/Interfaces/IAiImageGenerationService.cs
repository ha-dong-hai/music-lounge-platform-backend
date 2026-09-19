namespace MusicLounge.Application.Common.Interfaces;

// Unlike IAiModerationService, this is NOT fail-open — a poster generation call is a direct,
// user-visible action the Owner explicitly requested, so a failure must surface as a real error
// (caught by GeneratePosterCommandHandler and logged as a Failed AiPosterGeneration row that does
// NOT count against the Owner's monthly quota), not silently swallowed.
public interface IAiImageGenerationService
{
    Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default);

    /// <summary>
    /// MLACP-458. <c>true</c> nghĩa là nhà cung cấp KHÔNG trả ảnh trong cùng lượt gọi: đơn được ghi vào hàng đợi và một
    /// máy trạm bên ngoài sẽ đến lấy (Google Flow chỉ nhận lệnh từ trình duyệt đã đăng nhập, nên máy chủ không gọi thẳng
    /// được, và một lượt sinh ảnh mất 50–90 giây — quá lâu để giữ một request HTTP).
    ///
    /// Đặt ở đây thay vì để handler đọc cấu hình: tầng Application không được biết tên nhà cung cấp nào cả, đúng như lý do
    /// việc CHỌN nhà cung cấp nằm ở <c>DependencyInjection</c> chứ không nằm trong handler.
    /// </summary>
    bool IsDeferred => false;

    /// <summary>Tên nhà cung cấp để ghi vào nhật ký (<c>AiPosterGeneration.Provider</c>) — chỉ dùng để đối chiếu về sau.</summary>
    string ProviderName => "unknown";
}
