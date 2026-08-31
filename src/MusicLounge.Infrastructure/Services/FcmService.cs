using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Settings;
using FcmNotification = FirebaseAdmin.Messaging.Notification;

namespace MusicLounge.Infrastructure.Services;

// Same "chưa tích hợp thật" fallback philosophy as SmsService: appsettings.json/Production leave
// Firebase:CredentialsPath empty until that environment's own service-account secret is
// provisioned, so this degrades to logging (in-app Notification row still recorded by
// NotificationService regardless) instead of throwing when nothing is configured. Once a real
// docs/secrets/Firebase/*.json is present (already true in Development.Local.json), pushes go out
// for real via the Admin SDK's HTTP v1 API.
internal sealed class FcmService : IFcmService
{
    private static readonly object InitLock = new();
    private static bool _initAttempted;

    private readonly IUnitOfWork _uow;
    private readonly FirebaseSettings _settings;
    private readonly ILogger<FcmService> _logger;

    public FcmService(IUnitOfWork uow, IOptions<FirebaseSettings> settings, ILogger<FcmService> logger)
    {
        _uow = uow;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendAsync(int userId, string title, string body, CancellationToken ct = default)
        => SendAsync(userId, title, body, new Dictionary<string, string>(), ct);

    public async Task SendAsync(
        int userId, string title, string body, Dictionary<string, string> data, CancellationToken ct = default)
    {
        if (!TryEnsureInitialized())
        {
            _logger.LogWarning(
                "FCM push NOT SENT (Firebase:CredentialsPath not configured, in-app notification still recorded) — userId={UserId} | {Title}: {Body}",
                userId, title, body);
            return;
        }

        var tokenRepo = _uow.Repository<DeviceToken, int>();
        var devices = await tokenRepo.FindAsync(d => d.UserId == userId, ct);
        if (devices.Count == 0)
        {
            _logger.LogInformation(
                "FCM push skipped (no registered device) — userId={UserId} | {Title}: {Body}", userId, title, body);
            return;
        }

        // FCM's server SDK is migrating from per-token addressing (Tokens, now obsolete) to
        // Firebase Installation IDs (Fids) — the client-side registration string itself is
        // unchanged, only the server-side field name/semantics moved. See
        // https://firerun.io/blog/firebase-fcm-token-fid-deprecation-2026/.
        var message = new MulticastMessage
        {
            Fids = devices.Select(d => d.Token).ToList(),
            Notification = new FcmNotification { Title = title, Body = body },
            Data = data
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, ct);

        if (response.FailureCount > 0)
        {
            // A token FCM reports as Unregistered will never succeed again (app uninstalled, token
            // rotated) — prune it now so future sends don't keep paying the per-token HTTP round
            // trip for a device that's gone, same self-cleaning reasoning as any dead-letter queue.
            var deadTokens = response.Responses
                .Select((r, i) => (Response: r, Token: devices[i].Token))
                .Where(x => !x.Response.IsSuccess
                    && x.Response.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                .Select(x => x.Token)
                .ToHashSet();

            if (deadTokens.Count > 0)
            {
                foreach (var device in devices.Where(d => deadTokens.Contains(d.Token)))
                    tokenRepo.Remove(device);
                await _uow.SaveChangesAsync(ct);
            }

            _logger.LogWarning(
                "FCM push partially failed — userId={UserId} success={Success} failure={Failure} prunedTokens={Pruned}",
                userId, response.SuccessCount, response.FailureCount, deadTokens.Count);
        }
    }

    // FirebaseApp.Create throws if called twice — a shared static guard (this service is
    // registered scoped, so the constructor runs on every request) makes the actual init
    // exactly-once per process, matching FirebaseApp's own singleton-per-process contract.
    private bool TryEnsureInitialized()
    {
        if (FirebaseApp.DefaultInstance is not null) return true;
        if (string.IsNullOrWhiteSpace(_settings.CredentialsPath)) return false;

        lock (InitLock)
        {
            if (FirebaseApp.DefaultInstance is not null) return true;
            if (_initAttempted) return false;
            _initAttempted = true;

            if (!File.Exists(_settings.CredentialsPath))
            {
                _logger.LogWarning(
                    "Firebase:CredentialsPath is set but the file does not exist: {Path}", _settings.CredentialsPath);
                return false;
            }

            FirebaseApp.Create(new AppOptions
            {
                Credential = CredentialFactory.FromFile(_settings.CredentialsPath, "service_account"),
                ProjectId = _settings.ProjectId
            });
            return true;
        }
    }
}
