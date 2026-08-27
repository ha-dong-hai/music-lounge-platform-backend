using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Livestreams.DTOs;

namespace MusicLounge.Application.Livestreams.Queries.GetChatHistory;

public sealed record GetChatHistoryQuery(
    int LivestreamId,
    int Page = 1,
    int PageSize = 50) : IQuery<PaginatedResult<ChatMessageDto>>;
