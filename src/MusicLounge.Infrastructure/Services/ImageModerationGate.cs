using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Infrastructure.Services;

internal sealed class ImageModerationGate : IImageModerationGate
{
    private readonly IImageModerationService _moderation;
    private readonly ISystemConfigService _config;

    public ImageModerationGate(IImageModerationService moderation, ISystemConfigService config)
    {
        _moderation = moderation;
        _config = config;
    }

    public async Task<AiModerationResult?> CheckOrThrowAsync(
        byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        var result = await _moderation.CheckAsync(imageBytes, mimeType, ct);
        if (result is null) return null; // fail-open: unscored never blocks an upload

        var blockThreshold = await _config.GetIntAsync(ConfigKeys.ImageModerationBlockThresholdPercent, 85, ct) / 100.0;
        if (result.Score >= blockThreshold)
            throw new DomainException(
                $"Ảnh không phù hợp để đăng công khai" +
                (string.IsNullOrWhiteSpace(result.FlagReason) ? "." : $": {result.FlagReason}"));

        var reviewThreshold = await _config.GetIntAsync(ConfigKeys.ImageModerationReviewThresholdPercent, 50, ct) / 100.0;
        return result.Score >= reviewThreshold ? result : null;
    }
}
