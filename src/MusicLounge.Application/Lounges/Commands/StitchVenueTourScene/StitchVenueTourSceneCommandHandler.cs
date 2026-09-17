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
    private readonly IPanoramaStitchingService _stitcher;
    private readonly IAsyncKeyedLock _lock;

    public StitchVenueTourSceneCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config,
        IBackgroundJobService backgroundJobs, IPanoramaStitchingService stitcher, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _backgroundJobs = backgroundJobs;
        _stitcher = stitcher;
        _lock = @lock;
    }

    public async Task<int> Handle(StitchVenueTourSceneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        // MLACP-436: khoa theo phong tra cho ca doan dem luot/dem canh -> tao luot. Command nay khong co transaction
        // (INoTransactionCommand) nen khoa nha ngay khi ra khoi ham — sau khi SaveChangesAsync da commit luot moi.
        await using var _ = await _lock.AcquireAsync(VenueTourRules.LockKey(request.LoungeId), ct);

        var now = DateTimeOffset.UtcNow;
        var subscriptions = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
        var existingScenes = await _uow.Repository<VenueTourScene, int>().FindAsync(s => s.LoungeId == request.LoungeId, ct);
        // Kiem so bo luc tao luot — job kiem LAI ngay truoc khi tao canh, vi job co the nam cho vai phut.
        if (VenueTourRules.QuotaViolation(existingScenes.Count, VenueTourRules.MaxScenes(subscriptions, now)) is { } loi)
            throw new DomainException(loi);

        var attemptRepo = _uow.Repository<VenueTourStitchAttempt, int>();

        // Anti-abuse: every attempt (success, failure, or now-pending) on THIS lounge counts — a
        // stitch runs on our own server's CPU, unlike the AI vendor calls elsewhere in this
        // codebase. Counting Pending too (not just terminal states) stops a burst of concurrent
        // requests from all slipping past the cap before any of them finishes.
        // MLACP-435: tru cac luot that bai do phia he thong (FailedBySystem) — CPU chua xu ly gi, va neu tinh thi dich vu
        // ngung vai lan la phong tra bi khoa tinh nang vinh vien vi loi khong phai cua ho.
        var maxAttempts = await _config.GetIntAsync(ConfigKeys.TourStitchMaxAttemptsPerLounge, 20, ct);
        var attemptsForLounge = await attemptRepo.CountAsync(
            a => a.LoungeId == request.LoungeId && !a.FailedBySystem, ct);
        if (attemptsForLounge >= maxAttempts)
            throw new DomainException(
                $"Venue này đã đạt giới hạn {maxAttempts} lần ghép ảnh. Vui lòng liên hệ hỗ trợ nếu cần thêm.");

        // MLACP-432: chua co dich vu ghep anh thi tu choi NGAY, truoc khi tao luot thu. Truoc day luot van duoc tao roi
        // that bai trong job, va moi luot tinh vao gioi han tron doi o tren — bam du so lan la phong tra bi khoa vinh vien
        // du chua ghep that lan nao. Dat sau cac kiem tra gioi han de nguoi bi chan vi goi/gioi han van nhan dung ly do.
        if (!_stitcher.IsConfiguredFor(request.SourceImageUrls))
            throw new DomainException(
                "Tính năng ghép ảnh 360° đang tạm ngưng nên chưa ghép được — lượt ghép của bạn không bị trừ. "
                + "Bạn vẫn có thể tải lên ảnh 360° chụp sẵn.");

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
