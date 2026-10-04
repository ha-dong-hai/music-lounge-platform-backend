using MediatR;
using MusicLounge.Application.Tickets;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.TicketTiers.Commands.CreateTicketTier;

internal sealed class CreateTicketTierCommandHandler : IRequestHandler<CreateTicketTierCommand, Guid>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;
    private readonly ISystemConfigService _config;
    private readonly ILivestreamRepository _livestreamRepo;

    public CreateTicketTierCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock,
        ISystemConfigService config, ILivestreamRepository livestreamRepo)
    {
        _livestreamRepo = livestreamRepo;
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
        _lock = @lock;
    }

    public async Task<Guid> Handle(CreateTicketTierCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, Guid>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền thiết lập giá vé cho event này.");

        var accessType = Enum.Parse<AccessType>(request.AccessType, ignoreCase: true);

        // MLACP-509. Loại hạng vé phải hợp với hình thức buổi diễn ở MỌI trạng thái — trước đây chỉ kiểm khi buổi đã đăng
        // (nhánh MLACP-388 bên dưới), nên lúc Draft tạo được hạng XEM TRỰC TUYẾN cho buổi Offline: đăng lên là khán giả
        // mua được vé cho một buổi không bao giờ phát (CreateLivestream chặn buổi Offline). Chiều ngược lại cùng lớp lỗi:
        // hạng VÀO CỬA cho buổi Online — PhysicalAccess đã chặn bán, nhưng hạng vé vẫn sinh ra và hiện trên trang.
        // Hình thức chỉ đổi được Offline → Online (ChangeLoungeShowFormat, có hoàn tiền vé vào cửa), và sửa hạng vé không
        // đổi được loại vé, nên chặn ở đây là đủ để không còn hạng vé lệch hình thức mới.
        if (accessType == AccessType.Livestream && show.Format == LoungeShowFormat.Offline)
            throw new DomainException("Buổi diễn tại chỗ (Offline) không bán vé xem trực tuyến — chỉ buổi online hoặc hybrid mới có hạng vé livestream.");
        if (!PhysicalAccess.IsOffered(show, accessType))
            throw new DomainException("Buổi diễn trực tuyến (Online) không có chỗ ngồi tại phòng trà — không tạo được hạng vé vào cửa.");

        // MLACP-388: buoi dien da dang ma phai chuyen sang online (MLACP-383 hoan 100% ve vao cua) truoc day khong the co
        // hang ve livestream — tao hang ve chi chay khi Draft va la duong duy nhat — nen khong ban duoc ve xem online. Nay
        // duoc them DUY NHAT hang ve livestream, cho buoi dien Online/Hybrid da co livestream; gia chua mo ban cho toi khi
        // Admin duyet (ReviewTicketTier). Hang ve vao cua va gia da cong bo van khoa nhu da hua voi nguoi mua.
        var addingAfterPublish = show.Status is LoungeShowStatus.Published or LoungeShowStatus.Ongoing;
        if (show.Status != LoungeShowStatus.Draft && !addingAfterPublish)
            throw new DomainException("Chỉ có thể thêm hạng vé khi event còn ở trạng thái Draft.");

        if (addingAfterPublish)
        {
            if (accessType != AccessType.Livestream || show.Format == LoungeShowFormat.Offline)
                throw new DomainException(
                    "Buổi diễn đã đăng chỉ được thêm hạng vé livestream, và chỉ khi hình thức là online hoặc hybrid — " +
                    "hạng vé vào cửa và giá đã công bố được giữ nguyên như đã hứa với người mua.");
            if (await _livestreamRepo.GetByShowIdAsync(show.Id, ct) is null)
                throw new DomainException("Cần thiết lập livestream cho buổi diễn trước khi thêm hạng vé livestream.");
        }

        // D14: tong TotalCapacity cac tier cua show khong duoc vuot MaxTicketsPerEvent cua goi
        // subscription dang Active (snapshot tai luc dang ky, khong bi anh huong neu gia goi doi sau).
        //
        // MLACP-271: locked by ShowId (same key UpdateTicketTierCommandHandler uses) so this
        // read-existing-tiers-then-compare-then-write can't race a concurrent Update (or another
        // concurrent Create) on the same show — see that handler's comment for the full race
        // scenario this closes.
        await using var _ = await _lock.AcquireAsync($"ticket-tier-capacity:{request.ShowId}", ct);

        // MLACP-589: hạng vé vào cửa PHẢI gắn một khu ghế, và khu đó chưa thuộc hạng vé nào khác của buổi này. Kiểm trong
        // khoá theo buổi diễn ở trên (đọc-rồi-ghi): hai lệnh tạo đồng thời không thể cùng lấy một khu. Trước đây ZoneId
        // tuỳ chọn và không được kiểm thuộc phòng trà nào — gửi mã khu của phòng trà khác vẫn lưu.
        if (accessType == AccessType.Physical)
        {
            if (request.ZoneId is not { } zoneId)
                throw new DomainException(
                    "Hạng vé vào cửa phải gắn với một khu ghế trên sơ đồ phòng trà. Hãy chọn khu cho hạng vé này " +
                    "(chưa có khu nào thì tạo ở mục Khu vực chỗ ngồi trước).");
            await TierZoneRules.EnsureZoneIsFreeForShowAsync(_uow, show, zoneId, exceptTierId: null, ct);
        }

        if (request.TotalCapacity.HasValue)
        {
            var activeStatusSubs = await _uow.Repository<OwnerSubscription, Guid>().FindAsync(
                s => s.OwnerId == lounge.OwnerId && s.Status == SubscriptionStatus.Active, ct);
            // Cap luon ton tai — goi dang hoat dong, hoac muc mien phi. Day la thoi diem venue TAO
            // MOT CAM KET MOI, dung cho gioi han nen can.
            var freeTierCap = await _config.GetIntAsync(
                ConfigKeys.FreeTierMaxTicketsPerEvent,
                SubscriptionEntitlements.DefaultFreeTierMaxTicketsPerEvent, ct);
            var cap = SubscriptionEntitlements.ResolveTicketCap(
                SubscriptionEntitlements.ActivePlan(activeStatusSubs, DateTimeOffset.UtcNow), freeTierCap);

            var existingTiers = await _uow.Repository<TicketTier, Guid>()
                .FindAsync(t => t.LoungeShowId == request.ShowId, ct);
            var totalCapacity = existingTiers.Sum(t => t.TotalCapacity ?? 0) + request.TotalCapacity.Value;

            if (totalCapacity > cap.MaxTicketsPerEvent)
                throw new DomainException(
                    cap.ExceededMessage($"Tổng sức chứa các hạng vé của buổi hòa nhạc này ({totalCapacity})"));
        }

        var tier = new TicketTier
        {
            LoungeShowId = request.ShowId,
            Name = request.Name,
            Description = request.Description,
            AccessType = accessType,
            ZoneId = accessType == AccessType.Physical ? request.ZoneId : null,
            TotalCapacity = request.TotalCapacity
        };

        _uow.Repository<TicketTier, Guid>().Add(tier);
        await _uow.SaveChangesAsync(ct);

        foreach (var priceInput in request.Prices)
        {
            var channel = Enum.Parse<PurchaseChannel>(priceInput.PurchaseChannel, ignoreCase: true);
            _uow.Repository<TicketPrice, Guid>().Add(new TicketPrice
            {
                TierId = tier.Id,
                Name = priceInput.Name,
                Price = priceInput.Price,
                Quota = priceInput.Quota,
                PurchaseChannel = channel,
                SaleStart = priceInput.SaleStart,
                SaleEnd = priceInput.SaleEnd,
                // MLACP-388: gia them sau khi dang chua ai duyet — chi mo ban khi Admin dong y.
                IsActive = !addingAfterPublish
            });
        }

        await _uow.SaveChangesAsync(ct);

        if (addingAfterPublish)
        {
            // Cung SLA voi duyet buoi dien va livestream (ND 147/2024 — doc tu system_config, khong co dinh).
            var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
            _uow.Repository<EventModeration, Guid>().Add(new EventModeration
            {
                TargetType = ModerationTargetType.TicketTier,
                TargetId = tier.Id,
                SlaDeadline = DateTimeOffset.UtcNow.AddHours(slaHours)
            });
            await _uow.SaveChangesAsync(ct);
        }

        return tier.Id;
    }
}
