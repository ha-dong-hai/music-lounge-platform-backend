using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.AddVenueTourScene;

// Gated by the active subscription's MaxTourScenesSnapshot — same D12 snapshot-at-subscribe-time
// pattern as MaxTicketsPerEventSnapshot (CreateTicketTierCommandHandler), so a later Admin edit to
// the package can't shrink a tour an Owner already built mid-subscription.
internal sealed class AddVenueTourSceneCommandHandler : IRequestHandler<AddVenueTourSceneCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IImageModerationGate _moderationGate;
    private readonly ISystemConfigService _config;

    public AddVenueTourSceneCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage,
        IImageModerationGate moderationGate, ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _moderationGate = moderationGate;
        _config = config;
    }

    public async Task<int> Handle(AddVenueTourSceneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
        var activeSub = activeStatusSubs
            .Where(s => s.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.StartedAt).FirstOrDefault();

        var existingScenes = await _uow.Repository<VenueTourScene, int>().FindAsync(
            s => s.LoungeId == request.LoungeId, ct);

        var maxScenes = activeSub?.MaxTourScenesSnapshot ?? 0;
        if (existingScenes.Count >= maxScenes)
            throw new DomainException(
                maxScenes == 0
                    ? "Gói subscription hiện tại không hỗ trợ tour ảo 360° — vui lòng nâng cấp gói."
                    : $"Tour đã đạt giới hạn {maxScenes} scene của gói subscription hiện tại.");

        // Throws (blocks the upload entirely) if the image scores high enough - see
        // IImageModerationGate. Checked BEFORE creating the scene so a blocked image never lands
        // in the DB at all, not even transiently.
        var imageBytes = await _fileStorage.ReadPublicImageAsync(request.ImageUrl, ct);
        var moderation = await _moderationGate.CheckOrThrowAsync(
            imageBytes, ImageMimeTypeHelper.FromUrl(request.ImageUrl), ct);

        var scene = new VenueTourScene
        {
            LoungeId = request.LoungeId,
            ImageUrl = request.ImageUrl,
            Name = request.Name,
            OrderIndex = existingScenes.Count
        };
        _uow.Repository<VenueTourScene, int>().Add(scene);
        await _uow.SaveChangesAsync(ct);

        if (moderation is not null)
            await FlagForReviewAsync(scene.Id, moderation, ct);

        return scene.Id;
    }

    private async Task FlagForReviewAsync(int sceneId, AiModerationResult moderation, CancellationToken ct)
    {
        var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
        var now = DateTimeOffset.UtcNow;
        _uow.Repository<EventModeration, int>().Add(new EventModeration
        {
            TargetType = ModerationTargetType.TourScene,
            TargetId = sceneId,
            AiScore = moderation.Score,
            RiskLevel = Enum.TryParse<ModerationRiskLevel>(moderation.RiskLevel, true, out var risk) ? risk : null,
            FlagReason = moderation.FlagReason,
            AiRecommendation = Enum.TryParse<AiModerationRecommendation>(moderation.Recommendation, true, out var rec) ? rec : null,
            CreatedAt = now,
            SlaDeadline = now.AddHours(slaHours)
        });
        await _uow.SaveChangesAsync(ct);
    }
}
