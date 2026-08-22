using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.UpdateMood;

internal sealed class UpdateMoodCommandHandler : IRequestHandler<UpdateMoodCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public UpdateMoodCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(UpdateMoodCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Mood, int>();
        var mood = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(Mood), request.Id);

        var nameTaken = await repo.AnyAsync(m => m.Id != request.Id && m.Name == request.Name, ct);
        if (nameTaken)
            throw new ConflictException($"Dòng nhạc/cảm xúc '{request.Name}' đã tồn tại.");

        mood.Name = request.Name;
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
