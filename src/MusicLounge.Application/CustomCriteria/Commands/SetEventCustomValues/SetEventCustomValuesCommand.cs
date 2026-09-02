using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.CustomCriteria.Commands.SetEventCustomValues;

public sealed record EventCustomValueInput(int CriteriaId, string Value);

public sealed record SetEventCustomValuesCommand(
    int ShowId,
    IReadOnlyList<EventCustomValueInput> Values
) : ICommand;
