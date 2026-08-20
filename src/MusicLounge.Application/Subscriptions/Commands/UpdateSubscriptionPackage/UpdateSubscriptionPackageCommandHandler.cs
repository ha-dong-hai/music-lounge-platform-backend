using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions.Commands.UpdateSubscriptionPackage;

// D12: Owner da subscribe (Active) vao goi nay thi khong duoc sua
// Price/MaxTicketsPerEvent/HasAiPoster/MaxAiPostersPerMonth nua - chi Description/IsActive.
// Muon doi gia that su -> tao goi moi + deactivate goi cu.
internal sealed class UpdateSubscriptionPackageCommandHandler
    : IRequestHandler<UpdateSubscriptionPackageCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public UpdateSubscriptionPackageCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(UpdateSubscriptionPackageCommand request, CancellationToken ct)
    {
        var packageRepo = _uow.Repository<SubscriptionPackage, int>();
        var package = await packageRepo.GetByIdAsync(request.PackageId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPackage), request.PackageId);

        var hasActiveSubscribers = await _uow.Repository<OwnerSubscription, int>().AnyAsync(
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

        package.Description = request.Description;
        package.Price = request.Price;
        package.MaxTicketsPerEvent = request.MaxTicketsPerEvent;
        package.HasAiPoster = request.HasAiPoster;
        package.MaxAiPostersPerMonth = request.MaxAiPostersPerMonth;
        package.MaxTourScenes = request.MaxTourScenes;
        package.IsActive = request.IsActive;

        packageRepo.Update(package);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
