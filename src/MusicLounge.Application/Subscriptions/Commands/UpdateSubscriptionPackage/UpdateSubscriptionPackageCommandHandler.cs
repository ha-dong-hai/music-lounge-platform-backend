using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions.Commands.UpdateSubscriptionPackage;

// D12: Owner da subscribe (Active) vao goi nay thi khong duoc sua
// Price/MaxTicketsPerEvent/HasAiPoster/MaxAiPostersPerMonth nua - chi Description/IsActive.
// Muon doi gia that su -> tao goi moi + deactivate goi cu.
//
// MLACP-677 — AN GOI KHI CON NGUOI DUNG. Chu du an chot 06/10/2026: gói bị ẩn thì chủ đang dùng VẪN DÙNG ĐỦ tới hết kỳ đã
// trả (đã trả tiền cho kỳ đó — cắt ngang là thu tiền mà không giao dịch vụ), nhưng không gia hạn được gói này nữa
// (RenewSubscription đã chặn từ trước). Thiếu sót trước đây: không ai báo cho chủ phòng trà, nên họ chỉ biết khi bấm "Gia
// hạn" và nhận lỗi. Nay báo ngay lúc ẩn, kèm ngày hết hạn của chính họ.
internal sealed class UpdateSubscriptionPackageCommandHandler
    : IRequestHandler<UpdateSubscriptionPackageCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;

    public UpdateSubscriptionPackageCommandHandler(IUnitOfWork uow, INotificationService notifications)
    {
        _uow = uow;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(UpdateSubscriptionPackageCommand request, CancellationToken ct)
    {
        var packageRepo = _uow.Repository<SubscriptionPackage, Guid>();
        var package = await packageRepo.GetByIdAsync(request.PackageId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPackage), request.PackageId);

        var hasActiveSubscribers = await _uow.Repository<OwnerSubscription, Guid>().AnyAsync(
            s => s.PackageId == request.PackageId && s.Status == SubscriptionStatus.Active, ct);

        if (hasActiveSubscribers)
        {
            var changesLockedFields =
                package.Price != request.Price ||
                package.MaxTicketsPerEvent != request.MaxTicketsPerEvent ||
                package.HasAiPoster != request.HasAiPoster ||
                package.MaxAiPostersPerMonth != request.MaxAiPostersPerMonth ||
                package.MaxTourScenes != request.MaxTourScenes;

            if (changesLockedFields)
                throw new DomainException(
                    "Gói đã có Owner đang sử dụng — không thể sửa Giá/Số vé tối đa/AI Poster/Số scene tour. " +
                    "Hãy tạo gói mới và deactivate gói cũ nếu muốn thay đổi các điều khoản này.");
        }

        var vuaNgungBan = package.IsActive && !request.IsActive;

        package.Description = request.Description;
        package.Price = request.Price;
        package.MaxTicketsPerEvent = request.MaxTicketsPerEvent;
        package.HasAiPoster = request.HasAiPoster;
        package.MaxAiPostersPerMonth = request.MaxAiPostersPerMonth;
        package.MaxTourScenes = request.MaxTourScenes;
        package.IsActive = request.IsActive;

        packageRepo.Update(package);

        if (vuaNgungBan)
        {
            var now = DateTimeOffset.UtcNow;
            var dangDung = (await _uow.Repository<OwnerSubscription, Guid>().FindAsync(
                    s => s.PackageId == request.PackageId && s.Status == SubscriptionStatus.Active, ct))
                .Where(s => s.ExpiresAt > now);
            foreach (var sub in dangDung)
            {
                var han = VietnamTime.Format(sub.ExpiresAt, "dd/MM/yyyy");
                await _notifications.NotifyAsync(
                    sub.OwnerId, NotificationType.SubscriptionUpdated,
                    new SongNgu($"Gói \"{package.Name}\" đã ngừng mở bán", $"The \"{package.Name}\" plan is no longer sold"),
                    new SongNgu(
                        $"Bạn vẫn dùng đầy đủ quyền lợi của gói \"{package.Name}\" tới hết ngày {han}. Gói này không gia hạn được " +
                        "nữa — trước ngày đó, hãy chọn một gói khác trong mục Gói dịch vụ để không bị gián đoạn.",
                        $"You keep every benefit of the \"{package.Name}\" plan until {han}. This plan can no longer be renewed — " +
                        "before that date, pick another plan under Subscription to avoid any interruption."),
                    referenceType: "subscription", referenceId: sub.Id.ToString(), ct: ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
