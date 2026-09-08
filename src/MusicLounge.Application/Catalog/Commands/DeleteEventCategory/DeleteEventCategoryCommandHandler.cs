using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.DeleteEventCategory;

internal sealed class DeleteEventCategoryCommandHandler : IRequestHandler<DeleteEventCategoryCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public DeleteEventCategoryCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(DeleteEventCategoryCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<EventCategory, int>();
        var category = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(EventCategory), request.Id);

        // LoungeShow.CategoryId la FK truc tiep (khong phai bang join) - danh muc nay khong co
        // bang join rieng nhu Genre/Mood/Atmosphere.
        var inUse = await _uow.Repository<LoungeShow, int>().AnyAsync(x => x.CategoryId == request.Id, ct);
        if (inUse)
            throw new ConflictException($"Loại buổi diễn '{category.Name}' đang được sử dụng, không thể xóa.");

        repo.Remove(category);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
