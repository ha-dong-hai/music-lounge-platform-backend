using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;

namespace MusicLounge.Application.Users.Queries.GetOwnerTransactionHistory;

public sealed record GetOwnerTransactionHistoryQuery(
    string? Type,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize
) : IQuery<PaginatedResult<OwnerTransactionDto>>;
