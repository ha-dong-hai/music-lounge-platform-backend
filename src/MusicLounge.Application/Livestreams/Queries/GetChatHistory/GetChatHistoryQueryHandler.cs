using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Livestreams.Queries.GetChatHistory;

internal sealed class GetChatHistoryQueryHandler
    : IRequestHandler<GetChatHistoryQuery, PaginatedResult<ChatMessageDto>>
{
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;

    public GetChatHistoryQueryHandler(
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser)
    {
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ChatMessageDto>> Handle(GetChatHistoryQuery request, CancellationToken ct)
    {
        var userHasAccess = _currentUser.Role is Roles.Admin or Roles.Staff
            || await _livestreamRepo.HasViewerAccessAsync(request.LivestreamId, _currentUser.UserId, ct);

        if (!userHasAccess)
            throw new ForbiddenException("Bạn cần có vé livestream để xem nội dung này.");

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (messages, totalCount) = await _livestreamRepo.GetChatMessagesAsync(
            request.LivestreamId, page, pageSize, ct);

        var items = messages
            .Select(m => new ChatMessageDto(m.Id, m.UserId, m.User.FullName, m.Message, m.SentAt))
            .ToList();

        return new PaginatedResult<ChatMessageDto>(items, page, pageSize, totalCount);
    }
}
