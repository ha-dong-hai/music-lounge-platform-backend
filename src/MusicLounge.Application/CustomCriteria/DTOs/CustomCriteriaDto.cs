using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.CustomCriteria.DTOs;

public sealed record CustomCriteriaDto(
    int Id,
    int LoungeId,
    string Name,
    string Key,
    CustomCriteriaDataType DataType,
    string? Options,
    bool IsActive,
    DateTimeOffset CreatedAt);
