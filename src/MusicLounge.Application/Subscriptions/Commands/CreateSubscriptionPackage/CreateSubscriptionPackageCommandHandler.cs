using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Subscriptions.Commands.CreateSubscriptionPackage;

internal sealed class CreateSubscriptionPackageCommandHandler
    : IRequestHandler<CreateSubscriptionPackageCommand, int>
{
    private readonly IUnitOfWork _uow;

    public CreateSubscriptionPackageCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<int> Handle(CreateSubscriptionPackageCommand request, CancellationToken ct)
    {
        var package = new SubscriptionPackage
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            BillingCycle = Enum.Parse<SubscriptionBillingCycle>(request.BillingCycle, ignoreCase: true),
            MaxTicketsPerEvent = request.MaxTicketsPerEvent,
            HasAiPoster = request.HasAiPoster,
            MaxAiPostersPerMonth = request.MaxAiPostersPerMonth,
            MaxTourScenes = request.MaxTourScenes,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _uow.Repository<SubscriptionPackage, int>().Add(package);
        await _uow.SaveChangesAsync(ct);
        return package.Id;
    }
}
