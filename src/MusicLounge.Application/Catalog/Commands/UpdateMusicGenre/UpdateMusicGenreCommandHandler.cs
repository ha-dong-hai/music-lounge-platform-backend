using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.UpdateMusicGenre;

internal sealed class UpdateMusicGenreCommandHandler : IRequestHandler<UpdateMusicGenreCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public UpdateMusicGenreCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(UpdateMusicGenreCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicGenre, int>();
        var genre = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(MusicGenre), request.Id);

        var nameTaken = await repo.AnyAsync(g => g.Id != request.Id && g.Name == request.Name, ct);
        if (nameTaken)
            throw new ConflictException($"Thể loại '{request.Name}' đã tồn tại.");

        genre.Name = request.Name;
        genre.NameEn = request.NameEn;
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
