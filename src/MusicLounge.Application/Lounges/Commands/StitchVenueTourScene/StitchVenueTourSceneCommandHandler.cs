using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.StitchVenueTourScene;

// Alternative to AddVenueTourSceneCommand for Owners who don't have a native 360° capture app —
// takes several overlapping photos shot from one spot while rotating, stitches them into one
// panorama via the standalone panorama-stitcher microservice, then creates a VenueTourScene from
// the result exactly like AddVenueTourSceneCommand would. Counts against the SAME
// MaxTourScenesSnapshot quota (it's still one more scene either way), plus its own anti-abuse cap
// (tour_stitch_max_attempts_per_lounge) since — unlike the AI vendor calls elsewhere in this
// codebase — a stitch attempt burns OUR OWN server's CPU, not a paid third party's.
//
// Runs the actual stitch in the background (StitchVenueTourSceneJob) rather than inline: a stitch
// can take 15-30+ seconds and occasionally brushes the panorama-stitcher HttpClient's 120s
// timeout on harder photo sets, which would otherwise block the Owner's HTTP request for that
// whole window. This handler does the upfront checks and creates a Pending attempt row
// synchronously (so quota/anti-abuse limits are enforced before returning), then enqueues the
// job and returns the attempt id immediately — the Owner polls GetVenueTourStitchAttemptQuery for
// the outcome instead of waiting on this request.
internal sealed class StitchVenueTourSceneCommandHandler : IRequestHandler<StitchVenueTourSceneCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly IBackgroundJobService _backgroundJobs;

    public StitchVenueTourSceneCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config,
        IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<int> Handle(StitchVenueTourSceneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var now = DateTimeOffset.UtcNow;
        var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
        var activeSub = activeStatusSubs
            .Where(s => s.ExpiresAt > now)
            .OrderByDescending(s => s.StartedAt).FirstOrDefault();

        var sceneRepo = _uow.Repository<VenueTourScene, int>();
        var existingScenes = await sceneRepo.FindAsync(s => s.LoungeId == request.LoungeId, ct);
        var maxScenes = activeSub?.MaxTourScenesSnapshot ?? 0;
        if (existingScenes.Count >= maxScenes)
            throw new DomainException(
                maxScenes == 0
                    ? "Gói subscription hiện tại không hỗ trợ tour ảo 360° — vui lòng nâng cấp gói."
                    : $"Tour đã đạt giới hạn {maxScenes} scene của gói subscription hiện tại.");

        var attemptRepo = _uow.Repository<VenueTourStitchAttempt, int>();

        // Anti-abuse: every attempt (success, failure, or now-pending) on THIS lounge counts — a
        // stitch runs on our own server's CPU, unlike the AI vendor calls elsewhere in this
        // codebase. Counting Pending too (not just terminal states) stops a burst of concurrent
        // requests from all slipping past the cap before any of them finishes.
        var maxAttempts = await _config.GetIntAsync(ConfigKeys.TourStitchMaxAttemptsPerLounge, 20, ct);
        var attemptsForLounge = await attemptRepo.CountAsync(a => a.LoungeId == request.LoungeId, ct);
        if (attemptsForLounge >= maxAttempts)
            throw new DomainException(
                $"Venue này đã đạt giới hạn {maxAttempts} lần ghép ảnh. Vui lòng liên hệ hỗ trợ nếu cần thêm.");

        var attempt = new VenueTourStitchAttempt
        {
            LoungeId = request.LoungeId,
            Status = VenueTourStitchStatus.Pending,
            CreatedAt = now
        };
        attemptRepo.Add(attempt);
        await _uow.SaveChangesAsync(ct);

        _backgroundJobs.EnqueueStitchVenueTourScene(attempt.Id, request.LoungeId, request.SourceImageUrls, request.Name);

        return attempt.Id;
    }
}
