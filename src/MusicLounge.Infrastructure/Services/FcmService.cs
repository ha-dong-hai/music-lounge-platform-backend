using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

internal sealed class FcmService : IFcmService
{
    private readonly ILogger<FcmService> _logger;

    public FcmService(ILogger<FcmService> logger) => _logger = logger;

    // NotificationService always writes the in-app Notification row regardless of what happens
    // here — this stub only affects the push-to-device leg. It intentionally does NOT throw:
    // this runs as a Hangfire background job (EnqueueFcmNotification), and failing it would pile
    // up failed-job retries / alerts for something that isn't actually broken, just not wired up
    // yet (no Firebase service account configured). LogWarning (not Information) instead — this
    // codebase's own Serilog config only escalates EF/Hangfire framework noise to Warning, so a
    // human watching logs at Warning+ actually sees this and won't mistake "[stub]" for a real
    // send at a glance.
    public Task SendAsync(int userId, string title, string body, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "FCM push NOT SENT (provider not configured, in-app notification still recorded) — userId={UserId} | {Title}: {Body}",
            userId, title, body);
        return Task.CompletedTask;
    }

    public Task SendAsync(int userId, string title, string body,
        Dictionary<string, string> data, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "FCM push NOT SENT (provider not configured, in-app notification still recorded) — userId={UserId} | {Title}: {Body} | data={@Data}",
            userId, title, body, data);
        return Task.CompletedTask;
    }
}
