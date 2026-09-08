namespace MusicLounge.Application.Analytics.DTOs;

public sealed record AiRecommendationPerformanceDto(
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    int RecommendedPairCount,
    int ClickThroughCount,
    decimal ClickThroughRatePercent,
    int ConversionCount,
    decimal ConversionRatePercent);
