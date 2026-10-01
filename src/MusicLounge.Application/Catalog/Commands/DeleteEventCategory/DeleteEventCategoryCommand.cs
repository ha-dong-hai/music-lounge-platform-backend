using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Catalog.Commands.DeleteEventCategory;

public sealed record DeleteEventCategoryCommand(Guid Id) : ICommand;
