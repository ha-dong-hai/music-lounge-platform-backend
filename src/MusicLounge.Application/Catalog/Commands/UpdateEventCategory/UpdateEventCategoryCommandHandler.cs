using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.UpdateEventCategory;

internal sealed class UpdateEventCategoryCommandHandler : IRequestHandler<UpdateEventCategoryCommand, Unit>
{
    private readonly IUnitOfWork _uow;

    public UpdateEventCategoryCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Unit> Handle(UpdateEventCategoryCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<EventCategory, int>();
        var category = await repo.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(EventCategory), request.Id);

        var nameTaken = await repo.AnyAsync(c => c.Id != request.Id && c.Name == request.Name, ct);
        if (nameTaken)
            throw new ConflictException($"Loại buổi diễn '{request.Name}' đã tồn tại.");

        category.Name = request.Name;
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
