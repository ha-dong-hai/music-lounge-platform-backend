using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Commands.RemoveRating;

internal sealed class RemoveRatingCommandHandler : IRequestHandler<RemoveRatingCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public RemoveRatingCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(RemoveRatingCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<LoungeShowRating, int>();
        var rating = await repo.GetByIdAsync(request.RatingId, ct)
            ?? throw new NotFoundException(nameof(LoungeShowRating), request.RatingId);

        if (rating.IsRemoved)
            throw new ConflictException("Đánh giá này đã bị gỡ trước đó.");

        rating.IsRemoved = true;
        rating.RemovedReason = request.Reason;
        repo.Update(rating);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
