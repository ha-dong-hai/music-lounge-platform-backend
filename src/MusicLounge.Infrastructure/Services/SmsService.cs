using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Infrastructure.Services;

// Stub — no SMS gateway integrated yet (same status as FcmService: "chưa tích hợp thật" per
// docs/09-tech-knowledge.md). Logs instead of sending so the phone-verification flow (fields,
// hashing, rate-limit, expiry) is fully wired end-to-end and only needs a real provider (e.g. an
// SMS gateway with Vietnamese carrier support) plugged in behind this interface later — closes
// governance gap #4 (phone_verified was a dead field with no verification flow at all) without
// fabricating a fake third-party integration.
internal sealed class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;

    public SmsService(ILogger<SmsService> logger) => _logger = logger;

    public Task SendPhoneVerificationCodeAsync(string toPhone, string code, CancellationToken ct = default)
    {
        // Error, not Warning. A message the system believed it sent and did not is an incident, not
        // a note: the user is sitting there waiting for a code that will never arrive. Logging it
        // quietly is the same shape as the "promise without a mechanism" defects found repeatedly in
        // this audit — the system behaving as though something happened when it did not.
        _logger.LogError(
            "SMS NOT SENT — no SMS gateway is configured, so this verification code never reached the " +
            "user. Phone={Phone} Code={Code}. Wire a real provider behind ISmsService before relying " +
            "on phone verification in production.",
            toPhone, code);
        return Task.CompletedTask;
    }
}
