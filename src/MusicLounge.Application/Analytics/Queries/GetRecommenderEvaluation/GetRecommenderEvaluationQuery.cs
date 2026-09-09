using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Analytics.Queries.GetRecommenderEvaluation;

/// <param name="K">Xét top bao nhiêu gợi ý. Mặc định 10 — đúng số buổi diễn một màn hình hiển thị.</param>
public sealed record GetRecommenderEvaluationQuery(int K = 10)
    : IQuery<RecommenderEvaluationDto>;
