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
internal sealed class StitchVenueTourSceneCommandHandler : IRequestHandler<StitchVenueTourSceneCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;
    private readonly IPanoramaStitchingService _stitcher;
    private readonly IFileStorageService _fileStorage;

    public StitchVenueTourSceneCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config,
        IPanoramaStitchingService stitcher, IFileStorageService fileStorage)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _stitcher = stitcher;
        _fileStorage = fileStorage;
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

        // Anti-abuse: every attempt (success or failure) on THIS lounge counts — a stitch runs on
        // our own server's CPU, unlike the AI vendor calls elsewhere in this codebase.
        var maxAttempts = await _config.GetIntAsync(ConfigKeys.TourStitchMaxAttemptsPerLounge, 20, ct);
        var attemptsForLounge = await attemptRepo.CountAsync(a => a.LoungeId == request.LoungeId, ct);
        if (attemptsForLounge >= maxAttempts)
            throw new DomainException(
                $"Venue này đã đạt giới hạn {maxAttempts} lần ghép ảnh. Vui lòng liên hệ hỗ trợ nếu cần thêm.");

        byte[] imageBytes;
        try
        {
            imageBytes = await _stitcher.StitchAsync(request.SourceImageUrls, ct);
        }
        catch (ExternalServiceException ex)
        {
            attemptRepo.Add(new VenueTourStitchAttempt
            {
                LoungeId = request.LoungeId,
                Status = VenueTourStitchStatus.Failed,
                ErrorMessage = ex.Message,
                CreatedAt = now
            });
            await _uow.SaveChangesAsync(ct);
            throw;
        }

        string imageUrl;
        await using (var stream = new MemoryStream(imageBytes))
        {
            imageUrl = await _fileStorage.SaveImageAsync(stream, "panorama.jpg", ct);
        }

        var scene = new VenueTourScene
        {
            LoungeId = request.LoungeId,
            ImageUrl = imageUrl,
            Name = request.Name,
            OrderIndex = existingScenes.Count
        };
        sceneRepo.Add(scene);

        attemptRepo.Add(new VenueTourStitchAttempt
        {
            LoungeId = request.LoungeId,
            Status = VenueTourStitchStatus.Succeeded,
            ResultScene = scene,
            CreatedAt = now
        });

        await _uow.SaveChangesAsync(ct);
        return scene.Id;
    }
}
