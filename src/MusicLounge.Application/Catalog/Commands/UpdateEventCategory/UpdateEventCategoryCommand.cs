using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.UpdateEventCategory;

public sealed record UpdateEventCategoryCommand(Guid Id, string Name, string? Description, bool IsActive) : ICommand;
