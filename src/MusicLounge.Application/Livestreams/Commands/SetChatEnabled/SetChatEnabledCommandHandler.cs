using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Commands.SetChatEnabled;

internal sealed class SetChatEnabledCommandHandler : IRequestHandler<SetChatEnabledCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamHubService _hub;
    private readonly ILogger<SetChatEnabledCommandHandler> _logger;

    public SetChatEnabledCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILivestreamHubService hub, ILogger<SetChatEnabledCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _hub = hub;
        _logger = logger;
    }

    public async Task<Unit> Handle(SetChatEnabledCommand request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, Guid>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        var show = await _uow.Repository<LoungeShow, Guid>().GetByIdAsync(livestream.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), livestream.LoungeShowId);
        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền bật/tắt chat cho livestream này.");

        livestream.ChatEnabled = request.Enabled;
        _uow.Repository<Livestream, Guid>().Update(livestream);
        await _uow.SaveChangesAsync(ct);

        // MLACP-643: báo người đang xem ngay. Gọi sau SaveChanges, cùng cách các lệnh livestream khác phát sự kiện; lỗi
        // phát (mất kết nối hub) không được làm hỏng lệnh — chặn ở máy chủ vẫn đúng vì SendChatMessage đọc lại ChatEnabled.
        try { await _hub.BroadcastChatEnabledChangedAsync(livestream.Id, request.Enabled, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Không phát được sự kiện bật/tắt chat cho livestream {LivestreamId}", livestream.Id);
        }

        return Unit.Value;
    }
}
