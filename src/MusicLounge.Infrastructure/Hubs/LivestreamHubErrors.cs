using Microsoft.AspNetCore.SignalR;
using MusicLounge.Application.Common.Exceptions;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Infrastructure.Hubs;

/// <summary>
/// MLACP-643. Lỗi nghiệp vụ khi gọi một phương thức của <see cref="LivestreamHub"/> phải tới người xem bằng ĐÚNG câu của
/// máy chủ. SignalR giấu nội dung mọi ngoại lệ trừ <see cref="HubException"/> — client chỉ nhận "An unexpected error
/// occurred invoking 'SendMessage' on the server.", nên web in câu chung "Không gửi được tin nhắn, thử lại." cho cả ba
/// trường hợp khác hẳn nhau: gửi quá nhanh (1 tin / 2 giây), chủ phòng trà đã tắt chat, buổi phát đã dừng (đo 05/10/2026).
///
/// <para>Chỉ lỗi NGHIỆP VỤ (câu đã viết cho người dùng) được chuyển; lỗi hệ thống giữ nguyên để không lộ chi tiết nội bộ.</para>
/// </summary>
public static class LivestreamHubErrors
{
    /// <summary>Câu cho người xem nếu <paramref name="ex"/> là lỗi nghiệp vụ, ngược lại null.</summary>
    public static string? ViewerMessage(Exception ex) => ex switch
    {
        ValidationException v => v.Errors.Values.SelectMany(m => m).FirstOrDefault() ?? ValidationException.ThongBaoChung,
        DomainException or NotFoundException or ForbiddenException or ConflictException => ex.Message,
        _ => null
    };

    /// <summary>Chạy <paramref name="action"/>; lỗi nghiệp vụ được ném lại thành <see cref="HubException"/> mang câu đó.</summary>
    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ViewerMessage(ex) is { } message)
        {
            throw new HubException(message);
        }
    }
}
