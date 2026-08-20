using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Commands.EndLivestream;

internal sealed class EndLivestreamCommandHandler : IRequestHandler<EndLivestreamCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamServiceFactory _factory;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly ILogger<EndLivestreamCommandHandler> _logger;

    public EndLivestreamCommandHandler(
        IUnitOfWork uow,
        ILivestreamServiceFactory factory,
        ICurrentUserService currentUser,
        ISystemConfigService config,
        ILogger<EndLivestreamCommandHandler> logger)
    {
        _uow = uow;
        _factory = factory;
        _currentUser = currentUser;
        _config = config;
        _logger = logger;
    }

    public async Task<Unit> Handle(EndLivestreamCommand request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        if (livestream.Status != LivestreamStatus.Live)
            throw new DomainException($"Không thể kết thúc livestream ở trạng thái '{livestream.Status}'.");

        // D6: Staff chỉ được end livestream của venue được phân công (lounge_id từ JWT)
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), livestream.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền kết thúc livestream của venue này.");

        var now = DateTimeOffset.UtcNow;

        livestream.Status = LivestreamStatus.Ended;
        livestream.EndedAt = now;
        livestream.ViewerCount = 0;
        _uow.Repository<Livestream, int>().Update(livestream);

        var ratingWindowDays = await _config.GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);
        show.Status = LoungeShowStatus.Ended;
        show.ActualEnd = now;
        show.RatingOpenUntil = now.AddDays(ratingWindowDays);   // §6.13
        _uow.Repository<LoungeShow, int>().Update(show);

        await _uow.SaveChangesAsync(ct);

        // Best-effort cleanup — always use the provider that created this stream
        if (livestream.ProviderRef is not null)
        {
            try
            {
                var provider = _factory.GetProvider(livestream.Provider);
                await provider.DeleteStreamAsync(livestream.ProviderRef, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete {Provider} stream {ProviderRef} for livestream {LivestreamId}. Manual cleanup may be required.",
                    livestream.Provider, livestream.ProviderRef, livestream.Id);
            }
        }

        return Unit.Value;
    }
}
