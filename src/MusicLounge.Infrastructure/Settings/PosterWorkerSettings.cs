namespace MusicLounge.Infrastructure.Settings;

/// <summary>
/// MLACP-458. Máy trạm sinh poster qua Google Flow.
///
/// Nằm ở <c>appsettings</c> chứ không ở <c>system_config</c>: <see cref="ApiKey"/> là bí mật xác thực, và dự án đã chốt
/// ngưỡng/bí mật an ninh thì ở <c>appsettings</c> (trên Azure là biến môi trường), chỉ tham số nghiệp vụ mới nằm trong
/// <c>system_config</c> để người vận hành chỉnh qua giao diện Admin.
/// </summary>
public sealed class PosterWorkerSettings
{
    /// <summary>
    /// Bật chế độ hàng đợi. Cố ý tách khỏi việc có khoá hay không: có thể đã cấu hình sẵn khoá cho máy trạm nhưng tạm
    /// quay về nhà cung cấp gọi thẳng, và ngược lại — bật nhầm mà quên khoá thì tính năng phải HỎNG TO chứ không được
    /// mở toang ba endpoint không mật khẩu.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Khoá máy trạm dùng để gọi ba endpoint <c>/poster-jobs</c>. Trống nghĩa là không ai gọi được.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
