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
    private readonly IImageSizeReader _imageSize;
    private readonly IAsyncKeyedLock _lock;

    // Anh 360 chuan (phep chieu equirectangular) phu 360 do ngang x 180 do doc voi so do tren moi pixel bang nhau theo ca
    // hai chieu, nen ti le dung 2:1. Anh rong hon 2:1 van hop le (dai ghep bi cat bot tran/san). Anh hep hon thi khong
    // the phu du 360 do ngang. Dung sai 1% cho vai pixel bi cat khi chinh sua (198/100 = 1.98).
    private const int TiLeToiThieuPhanTram = 198;

    // MLACP-439: cung nguong voi dich vu ghep anh (_MIN_HORIZONTAL_COVERAGE_DEG, MLACP-438) — ho toi da 10 do, viewer
    // keo gian ngang ~2.8%.
    private const double GocPhuNgangToiThieu = 350.0;

    public AddVenueTourSceneCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorageService fileStorage,
        IImageModerationGate moderationGate, ISystemConfigService config, IImageSizeReader imageSize,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _moderationGate = moderationGate;
        _config = config;
        _imageSize = imageSize;
        _lock = @lock;
    }

    public async Task<int> Handle(AddVenueTourSceneCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa venue này.");

        // Kiem som (chua khoa) de tu choi nhanh truoc khi doc anh va goi kiem duyet AI; kiem LAI trong khoa ben duoi.
        await KiemGioiHanAsync(lounge.OwnerId, request.LoungeId, ct);

        // Throws (blocks the upload entirely) if the image scores high enough - see
        // IImageModerationGate. Checked BEFORE creating the scene so a blocked image never lands
        // in the DB at all, not even transiently.
        var imageBytes = await _fileStorage.ReadPublicImageAsync(request.ImageUrl, ct);

        // MLACP-433: truoc day nhan ca anh chup thuong (4:3, 16:9) — viewer trai anh do len mat cau nen hien thi meo.
        // Kiem TRUOC kiem duyet AI: anh bi tu choi thi khong ton luot goi Gemini.
        var size = _imageSize.ReadDisplaySize(imageBytes)
            ?? throw new DomainException(
                "Không đọc được kích thước ảnh. Hãy tải lên ảnh 360° dạng JPEG, PNG hoặc WebP.");
        if ((long)size.Width * 100 < (long)size.Height * TiLeToiThieuPhanTram)
            throw new DomainException(
                $"Ảnh {size.Width}×{size.Height} không phải ảnh 360°: ảnh 360° có chiều ngang ít nhất gấp đôi chiều cao "
                + "(tỉ lệ 2:1, ví dụ 4096×2048). Hãy chụp bằng chế độ 360°/Photo Sphere của điện thoại hoặc camera 360°, "
                + "hoặc dùng tính năng ghép nhiều ảnh chụp thường.");

        // MLACP-439: ti le >= 2:1 chua du — mot dai panorama chup mot phan vong ma cat bot tran/san van rong hon 2:1 (anh
        // that 8000×3314 chi phu ~192°). Anh co metadata GPano (chuan Photo Sphere) thi biet chinh xac goc phu; anh
        // khong co thi giu cach kiem ti le o tren.
        if (_imageSize.ReadGPanoHorizontalCoverageDegrees(imageBytes) is { } phuNgang && phuNgang < GocPhuNgangToiThieu)
            throw new DomainException(
                $"Ảnh này là panorama chỉ phủ khoảng {phuNgang:0}° theo chiều ngang (theo thông tin panorama lưu trong "
                + "ảnh) — tour 360° cần ảnh chụp đủ một vòng. Hãy chụp lại bằng chế độ 360°/Photo Sphere xoay đủ vòng, "
                + "hoặc dùng tính năng ghép nhiều ảnh chụp thường.");

        var moderation = await _moderationGate.CheckOrThrowAsync(
            imageBytes, ImageMimeTypeHelper.ForModeration(imageBytes), ct);

        // MLACP-436: khoa theo phong tra roi dem lai ngay truoc khi ghi — hai yeu cau dong thoi tung cung qua buoc kiem o
        // tren roi cung them, vuot gioi han goi. Khoa lay SAU buoc doc anh + kiem duyet AI (vai giay) de khong giu khoa
        // trong luc goi dich vu ngoai; trong transaction cua command, khoa duoc giu toi luc commit (MLACP-396).
        await using var _ = await _lock.AcquireAsync(VenueTourRules.LockKey(request.LoungeId), ct);
        var existingScenes = await KiemGioiHanAsync(lounge.OwnerId, request.LoungeId, ct);

        var scene = new VenueTourScene
        {
            LoungeId = request.LoungeId,
            ImageUrl = request.ImageUrl,
            Name = request.Name,
            OrderIndex = VenueTourRules.NextOrderIndex(existingScenes)
        };
        _uow.Repository<VenueTourScene, int>().Add(scene);
        await _uow.SaveChangesAsync(ct);

        if (moderation is not null)
            await FlagForReviewAsync(scene.Id, moderation, ct);

        return scene.Id;
    }

    private async Task<IReadOnlyList<VenueTourScene>> KiemGioiHanAsync(int ownerId, int loungeId, CancellationToken ct)
    {
        var subscriptions = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active, ct);
        var existingScenes = await _uow.Repository<VenueTourScene, int>().FindAsync(s => s.LoungeId == loungeId, ct);

        var loi = VenueTourRules.QuotaViolation(
            existingScenes.Count, VenueTourRules.MaxScenes(subscriptions, DateTimeOffset.UtcNow));
        return loi is null ? existingScenes : throw new DomainException(loi);
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
            SlaDeadline = now.AddHours(slaHours)
        });
        await _uow.SaveChangesAsync(ct);
    }
}
