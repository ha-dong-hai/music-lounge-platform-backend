using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Subscriptions.Queries.GetSubscriptionPackages;

internal sealed class GetSubscriptionPackagesQueryHandler
    : IRequestHandler<GetSubscriptionPackagesQuery, IReadOnlyList<SubscriptionPackageDto>>
{
    private readonly IUnitOfWork _uow;

    public GetSubscriptionPackagesQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SubscriptionPackageDto>> Handle(
        GetSubscriptionPackagesQuery request, CancellationToken ct)
    {
        var packages = request.ActiveOnly
            ? await _uow.Repository<SubscriptionPackage, int>().FindAsync(p => p.IsActive, ct)
            : await _uow.Repository<SubscriptionPackage, int>().GetAllAsync(ct);

        return packages
            .OrderBy(p => p.Price)
            .Select(p => new SubscriptionPackageDto(
                p.Id, p.Name, p.Description, p.Price, p.BillingCycle.ToString(),
                p.MaxTicketsPerEvent, p.HasAiPoster, p.IsActive))
            .ToList();
    }
}
