using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// Giữ lại các bản ghi log để test khẳng định được trên chúng.
///
/// Dựng cái này vì MLACP-313 sửa một lỗi mà bộ test không có cách nào nhìn thấy: mọi ngoại lệ
/// nghiệp vụ đều được ghi vào nhật ký ở mức Error với mã 500, trong khi người gọi nhận đúng 404.
/// Phía response đã được hàng trăm test phủ kín, nên chúng xanh hết — cái sai nằm hoàn toàn ở
/// phần không ai nhìn.
///
/// Nối vào được là nhờ Program.cs cấu hình Serilog bằng <c>ReadFrom.Services(services)</c>: Serilog
/// lấy mọi <see cref="ILogEventSink"/> đăng ký trong DI. Đăng ký thêm một cái trong test host là
/// đủ, không phải cấu hình lại logging.
///
/// Kho chứa để static vì Serilog tự dựng sink qua DI của chính nó, còn test thì cần đọc lại từ
/// bên ngoài. Các test trong collection "Integration" chạy tuần tự nên cặp Clear/Snapshot không
/// giẫm lên nhau.
/// </summary>
internal sealed class CapturingLogSink : ILogEventSink
{
    private static readonly ConcurrentQueue<LogEvent> Captured = new();

    public void Emit(LogEvent logEvent) => Captured.Enqueue(logEvent);

    public static void Clear()
    {
        while (Captured.TryDequeue(out _)) { }
    }

    public static IReadOnlyList<LogEvent> Snapshot() => Captured.ToArray();

    /// <summary>
    /// Bản ghi "kết thúc request" của Serilog cho một đường dẫn — bản ghi mang mức log và mã trạng
    /// thái cuối cùng, tức đúng thứ MLACP-313 nói tới.
    /// </summary>
    public static IReadOnlyList<LogEvent> RequestCompletionsFor(string pathFragment)
        => Snapshot()
            .Where(e => e.Properties.ContainsKey("StatusCode")
                        && e.Properties.TryGetValue("RequestPath", out var path)
                        && path.ToString().Contains(pathFragment, StringComparison.Ordinal))
            .ToList();

    public static string StatusCodeOf(LogEvent e) => e.Properties["StatusCode"].ToString();
}
