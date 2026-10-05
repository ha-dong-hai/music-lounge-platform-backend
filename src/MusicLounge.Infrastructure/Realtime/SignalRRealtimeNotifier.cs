using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Hubs;

namespace MusicLounge.Infrastructure.Realtime;

/// <summary>MLACP-669. Gửi sự kiện qua <see cref="NotificationHub"/>. Hai tên sự kiện phía web nghe:
/// <c>notification</c> (cho một người) và <c>queue-changed</c> (cho mọi Admin).</summary>
internal sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub>? _hub;
    private readonly ILogger<SignalRRealtimeNotifier> _logger;

    // IHubContext lấy theo kiểu TUỲ CHỌN: RealtimeOutbox là tham số của ApplicationDbContext, nên một host dùng
    // AddInfrastructure mà không AddSignalR (công cụ dựng dữ liệu, script) mà bắt buộc IHubContext thì ngay cả việc tạo
    // DbContext cũng hỏng. Không có hub = không có ai nghe = bỏ qua.
    public SignalRRealtimeNotifier(IServiceProvider services, ILogger<SignalRRealtimeNotifier> logger)
    {
        _hub = services.GetService(typeof(IHubContext<NotificationHub>)) as IHubContext<NotificationHub>;
        _logger = logger;
    }

    public async Task PublishAsync(IReadOnlyCollection<RealtimeEvent> events, CancellationToken ct = default)
    {
        if (_hub is null) return;
        foreach (var e in events)
        {
            try
            {
                var payload = new { topic = e.Topic, referenceType = e.ReferenceType, referenceId = e.ReferenceId };
                if (e.UserId is { } userId)
                    await _hub.Clients.Group(NotificationHub.UserGroup(userId)).SendAsync("notification", payload, ct);
                else
                    await _hub.Clients.Group(NotificationHub.AdminsGroup).SendAsync("queue-changed", payload, ct);
            }
            catch (Exception ex)
            {
                // Dữ liệu đã commit; mất một sự kiện chỉ nghĩa là màn hình cập nhật khi người dùng quay lại tab.
                _logger.LogWarning(ex, "Realtime event not sent: {Topic} user={UserId}", e.Topic, e.UserId);
            }
        }
    }
}
