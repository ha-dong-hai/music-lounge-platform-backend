using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;

public sealed record CreateCustomCriteriaCommand(
    Guid LoungeId,
    string Name,
    string Key,
    string DataType,
    string? Options
) : ICommand<Guid>;
