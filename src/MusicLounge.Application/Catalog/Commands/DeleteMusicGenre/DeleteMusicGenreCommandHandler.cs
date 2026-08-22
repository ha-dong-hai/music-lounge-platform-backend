using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.DeleteMusicGenre;

// Chan xoa neu con dang duoc dung (409) thay vi xoa-va-de-lai-null: du lieu show/nghe si/so thich
// nguoi dung tro toi the loai nay se mat am tham neu cho xoa vo dieu kien - buoc admin phai go
// lien ket truoc, dung nhu WordPress/e-commerce category deletion protection.
internal sealed class DeleteMusicGenreCommandHandler : IRequestHandler<DeleteMusicGenreCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public DeleteMusicGenreCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(DeleteMusicGenreCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<MusicGenre, int>();
        var genre = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(MusicGenre), request.Id);

        var inUse = await _uow.Repository<LoungeShowGenre, int>().AnyAsync(x => x.GenreId == request.Id, ct)
            || await _uow.Repository<PerformerGenre, int>().AnyAsync(x => x.GenreId == request.Id, ct)
            || await _uow.Repository<UserFavouriteGenre, int>().AnyAsync(x => x.GenreId == request.Id, ct);
        if (inUse)
            throw new ConflictException($"Thể loại '{genre.Name}' đang được sử dụng, không thể xóa.");

        repo.Remove(genre);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
