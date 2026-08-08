using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Livestreams.Commands.TerminateLivestream;

internal sealed class TerminateLivestreamCommandHandler : IRequestHandler<TerminateLivestreamCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamServiceFactory _factory;
    private readonly ILivestreamHubService _hubService;
    private readonly ILogger<TerminateLivestreamCommandHandler> _logger;

    public TerminateLivestreamCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILivestreamServiceFactory factory,
        ILivestreamHubService hubService,
        ILogger<TerminateLivestreamCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _factory = factory;
        _hubService = hubService;
        _logger = logger;
    }

    public async Task<Unit> Handle(TerminateLivestreamCommand request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        if (livestream.Status != LivestreamStatus.Live)
            throw new DomainException("Chỉ có thể terminate livestream đang phát sóng (status = Live).");

        // Best-effort: stop the provider stream
        try
        {
            var provider = _factory.GetProvider(livestream.Provider ?? _factory.ActiveProviderKey);
            await provider.DeleteStreamAsync(livestream.ProviderRef!, ct);
        }
        catch
        {
            // Provider cleanup is best-effort — DB status update is the authority
        }

        var now = DateTimeOffset.UtcNow;

        livestream.Status = LivestreamStatus.Terminated;
        livestream.EndedAt = now;
        livestream.TerminatedById = _currentUser.UserId;
        livestream.TerminatedReason = request.Reason;
        _uow.Repository<Livestream, int>().Update(livestream);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct);
        if (show is not null)
        {
            show.Status = LoungeShowStatus.Ended;
            show.ActualEnd = now;
            show.RatingOpenUntil = now.AddDays(7);   // §6.13 — show da dien (du bi cat ngang) van cho rate
            _uow.Repository<LoungeShow, int>().Update(show);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Livestream terminated: LivestreamId={LivestreamId} ShowId={ShowId} Reason={Reason} by UserId={UserId} at {At}",
            request.LivestreamId, livestream.LoungeShowId, request.Reason, _currentUser.UserId, now);

        // Notify all connected viewers so their clients stop retrying the HLS endpoint
        await _hubService.BroadcastLivestreamTerminatedAsync(request.LivestreamId, request.Reason, ct);

        return Unit.Value;
    }
}
