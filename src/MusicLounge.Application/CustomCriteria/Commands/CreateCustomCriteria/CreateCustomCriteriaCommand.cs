using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;

public sealed record CreateCustomCriteriaCommand(
    int LoungeId,
    string Name,
    string Key,
    string DataType,
    string? Options
) : ICommand<int>;
