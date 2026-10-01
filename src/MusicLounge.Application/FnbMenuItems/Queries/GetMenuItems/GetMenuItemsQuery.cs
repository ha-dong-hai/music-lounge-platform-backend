using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.FnbMenuItems.DTOs;

namespace MusicLounge.Application.FnbMenuItems.Queries.GetMenuItems;

public sealed record GetMenuItemsQuery(Guid MenuId, bool AvailableOnly = true)
    : IQuery<IReadOnlyList<FnbMenuItemDto>>;
