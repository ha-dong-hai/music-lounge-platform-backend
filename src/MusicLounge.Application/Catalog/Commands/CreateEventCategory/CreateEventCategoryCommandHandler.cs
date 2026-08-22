using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Catalog.Commands.CreateEventCategory;

internal sealed class CreateEventCategoryCommandHandler : IRequestHandler<CreateEventCategoryCommand, int>
{
    private readonly IUnitOfWork _uow;

    public CreateEventCategoryCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<int> Handle(CreateEventCategoryCommand request, CancellationToken ct)
    {
        var nameExists = await _uow.Repository<EventCategory, int>()
            .AnyAsync(c => c.Name == request.Name, ct);
        if (nameExists)
            throw new ConflictException($"Loại sự kiện '{request.Name}' đã tồn tại.");

        var category = new EventCategory
        {
            Name = request.Name,
            Description = request.Description,
            IsActive = true
        };
        _uow.Repository<EventCategory, int>().Add(category);
        await _uow.SaveChangesAsync(ct);

        return category.Id;
    }
}
