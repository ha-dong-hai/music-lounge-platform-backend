using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.CustomCriteria.DTOs;

public sealed record CustomCriteriaDto(
    Guid Id,
    Guid LoungeId,
    string Name,
    string Key,
    CustomCriteriaDataType DataType,
    string? Options,
    bool IsActive,
    DateTimeOffset CreatedAt);
