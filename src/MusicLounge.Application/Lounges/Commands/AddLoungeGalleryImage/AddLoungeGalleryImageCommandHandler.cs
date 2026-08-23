using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Lounges.Commands.AddLoungeGalleryImage;

// Free for every Owner, no subscription gate — same as PrimaryImageUrl, unlike VenueTourScene
// (the 360° tour, which IS gated) since these are just showcase photos, not an interactive feature.
internal sealed class AddLoungeGalleryImageCommandHandler : IRequestHandler<AddLoungeGalleryImageCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly IImageModerationGate _moderationGate;
    private readonly ISystemConfigService _config;

    public AddLoungeGalleryImageCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage,
        IImageModerationGate moderationGate, ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _moderationGate = moderationGate;
        _config = config;
    }

    public async Task<int> Handle(AddLoungeGalleryImageCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        // Throws (blocks the upload entirely) if the image scores high enough - see
        // IImageModerationGate. Checked BEFORE creating the gallery row so a blocked image never
        // lands in the DB at all, not even transiently.
        var imageBytes = await _fileStorage.ReadPublicImageAsync(request.ImageUrl, ct);
        var moderation = await _moderationGate.CheckOrThrowAsync(
            imageBytes, ImageMimeTypeHelper.FromUrl(request.ImageUrl), ct);

        var imageRepo = _uow.Repository<LoungeGalleryImage, int>();
        var existingCount = (await imageRepo.FindAsync(g => g.LoungeId == request.LoungeId, ct)).Count;

        var image = new LoungeGalleryImage
        {
            LoungeId = request.LoungeId,
            ImageUrl = request.ImageUrl,
            Caption = request.Caption,
            OrderIndex = existingCount
        };
        imageRepo.Add(image);

        // MLACP-33 DONE WHEN: anh dau tien tu dong la anh dai dien.
        if (existingCount == 0)
        {
            lounge.PrimaryImageUrl = request.ImageUrl;
            _uow.Repository<MusicLoungeEntity, int>().Update(lounge);
        }

        await _uow.SaveChangesAsync(ct);

        if (moderation is not null)
            await FlagForReviewAsync(image.Id, moderation, ct);

        return image.Id;
    }

    private async Task FlagForReviewAsync(int imageId, AiModerationResult moderation, CancellationToken ct)
    {
        var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
        var now = DateTimeOffset.UtcNow;
        _uow.Repository<EventModeration, int>().Add(new EventModeration
        {
            TargetType = ModerationTargetType.GalleryImage,
            TargetId = imageId,
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
