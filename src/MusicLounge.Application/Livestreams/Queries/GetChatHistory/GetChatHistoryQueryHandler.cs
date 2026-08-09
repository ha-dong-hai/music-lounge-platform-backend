using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Queries.GetChatHistory;

internal sealed class GetChatHistoryQueryHandler
    : IRequestHandler<GetChatHistoryQuery, PaginatedResult<ChatMessageDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;

    public GetChatHistoryQueryHandler(
        IUnitOfWork uow,
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser)
    {
        _uow = uow;
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ChatMessageDto>> Handle(GetChatHistoryQuery request, CancellationToken ct)
    {
        // Was any Staff/Admin account regardless of venue — Staff of venue A could read venue B's
        // paid livestream chat history for free. Scope Staff/Owner to the venue that actually owns
        // this livestream, same as GetLivestreamCredentialsQueryHandler.
        var userHasAccess = _currentUser.Role == Roles.Admin;
        if (!userHasAccess)
        {
            var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
                ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);
            var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct)
                ?? throw new NotFoundException(nameof(LoungeShow), livestream.LoungeShowId);
            var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
                ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

            userHasAccess = VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId)
                || await _livestreamRepo.HasViewerAccessAsync(request.LivestreamId, _currentUser.UserId, ct);
        }

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
