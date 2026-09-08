using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.CreateEventCategory;

public sealed record CreateEventCategoryCommand(string Name, string? Description) : ICommand<int>;
