using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Subscriptions.Queries.GetSubscriptionPackages;

internal sealed class GetSubscriptionPackagesQueryHandler
    : IRequestHandler<GetSubscriptionPackagesQuery, IReadOnlyList<SubscriptionPackageDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetSubscriptionPackagesQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SubscriptionPackageDto>> Handle(
        GetSubscriptionPackagesQuery request, CancellationToken ct)
    {
        // This endpoint is [AllowAnonymous] with ActiveOnly as a client-supplied query param —
        // only an Admin gets to ask for inactive/retired packages too; anyone else always gets
        // active-only regardless of what they pass, so retired pricing/feature tiers aren't
        // enumerable by the public.
        var activeOnly = request.ActiveOnly || _currentUser.Role != Roles.Admin;

        var packages = activeOnly
            ? await _uow.Repository<SubscriptionPackage, Guid>().FindAsync(p => p.IsActive, ct)
            : await _uow.Repository<SubscriptionPackage, Guid>().GetAllAsync(ct);

        // MLACP-677: Admin thấy mỗi gói đang có bao nhiêu chủ phòng trà dùng (còn hạn). Người khác không cần — và không nên
        // đọc được — con số kinh doanh này.
        Dictionary<Guid, int>? dangDung = null;
        if (_currentUser.Role == Roles.Admin)
        {
            var now = DateTimeOffset.UtcNow;
            dangDung = (await _uow.Repository<OwnerSubscription, Guid>().FindAsync(
                    s => s.Status == Domain.Enums.SubscriptionStatus.Active, ct))
                .Where(s => s.ExpiresAt > now)
                .GroupBy(s => s.PackageId)
                .ToDictionary(g => g.Key, g => g.Select(s => s.OwnerId).Distinct().Count());
        }

        return packages
            .OrderBy(p => p.Price)
            .Select(p => new SubscriptionPackageDto(
                p.Id, p.Name, p.Description, p.Price, p.BillingCycle.ToString(),
                p.MaxTicketsPerEvent, p.HasAiPoster, p.MaxAiPostersPerMonth, p.MaxTourScenes, p.IsActive,
                dangDung is null ? null : dangDung.GetValueOrDefault(p.Id)))
            .ToList();
    }
}
