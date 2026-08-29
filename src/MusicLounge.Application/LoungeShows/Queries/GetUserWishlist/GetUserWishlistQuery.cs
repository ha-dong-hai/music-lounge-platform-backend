using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.LoungeShows.DTOs;

namespace MusicLounge.Application.LoungeShows.Queries.GetUserWishlist;

public sealed record GetUserWishlistQuery(int Page = 1, int PageSize = 10)
    : IQuery<PaginatedResult<LoungeShowListItemDto>>;
