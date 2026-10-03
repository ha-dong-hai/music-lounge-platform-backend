using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.SetMusicGenreImage;

internal sealed class SetMusicGenreImageCommandHandler : IRequestHandler<SetMusicGenreImageCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public SetMusicGenreImageCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(SetMusicGenreImageCommand request, CancellationToken ct)
    {
        var genre = await _uow.Repository<MusicGenre, Guid>().GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(MusicGenre), request.Id);

        genre.ImageUrl = request.ImageUrl;
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
