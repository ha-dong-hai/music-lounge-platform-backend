using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.CreateMood;

internal sealed class CreateMoodCommandHandler : IRequestHandler<CreateMoodCommand, int>
{
    private readonly IUnitOfWork _uow;

    public CreateMoodCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<int> Handle(CreateMoodCommand request, CancellationToken ct)
    {
        var nameExists = await _uow.Repository<Mood, int>().AnyAsync(m => m.Name == request.Name, ct);
        if (nameExists)
            throw new ConflictException($"Dòng nhạc/cảm xúc '{request.Name}' đã tồn tại.");

        var mood = new Mood { Name = request.Name };
        _uow.Repository<Mood, int>().Add(mood);
        await _uow.SaveChangesAsync(ct);

        return mood.Id;
    }
}
