using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.CreateMusicGenre;

internal sealed class CreateMusicGenreCommandHandler : IRequestHandler<CreateMusicGenreCommand, int>
{
    private readonly IUnitOfWork _uow;

    public CreateMusicGenreCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<int> Handle(CreateMusicGenreCommand request, CancellationToken ct)
    {
        var nameExists = await _uow.Repository<MusicGenre, int>()
            .AnyAsync(g => g.Name == request.Name, ct);
        if (nameExists)
            throw new ConflictException($"Thể loại '{request.Name}' đã tồn tại.");

        var genre = new MusicGenre { Name = request.Name, NameEn = request.NameEn };
        _uow.Repository<MusicGenre, int>().Add(genre);
        await _uow.SaveChangesAsync(ct);

        return genre.Id;
    }
}
