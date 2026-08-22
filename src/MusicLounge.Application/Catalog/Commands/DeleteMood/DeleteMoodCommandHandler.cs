using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.DeleteMood;

internal sealed class DeleteMoodCommandHandler : IRequestHandler<DeleteMoodCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public DeleteMoodCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(DeleteMoodCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Mood, int>();
        var mood = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(Mood), request.Id);

        var inUse = await _uow.Repository<LoungeShowMood, int>().AnyAsync(x => x.MoodId == request.Id, ct)
            || await _uow.Repository<UserFavouriteMood, int>().AnyAsync(x => x.MoodId == request.Id, ct);
        if (inUse)
            throw new ConflictException($"Dòng nhạc/cảm xúc '{mood.Name}' đang được sử dụng, không thể xóa.");

        repo.Remove(mood);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
