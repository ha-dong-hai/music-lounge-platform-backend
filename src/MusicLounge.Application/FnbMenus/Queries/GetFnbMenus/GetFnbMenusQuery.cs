using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.FnbMenus.DTOs;

namespace MusicLounge.Application.FnbMenus.Queries.GetFnbMenus;

public sealed record GetFnbMenusQuery(int LoungeId, bool ActiveOnly = true)
    : IQuery<IReadOnlyList<FnbMenuDto>>;
