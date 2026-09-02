using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.CustomCriteria.Commands.CreateCustomCriteria;

internal sealed class CreateCustomCriteriaCommandHandler : IRequestHandler<CreateCustomCriteriaCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateCustomCriteriaCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateCustomCriteriaCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền tạo tiêu chí cho venue này.");

        var repo = _uow.Repository<CustomCriteriaEntity, int>();
        var keyExists = await repo.AnyAsync(
            c => c.LoungeId == request.LoungeId && c.Key == request.Key, ct);
        if (keyExists)
            throw new ConflictException($"Tiêu chí với Key \"{request.Key}\" đã tồn tại cho venue này.");

        var criteria = new CustomCriteriaEntity
        {
            LoungeId = request.LoungeId,
            Name = request.Name,
            Key = request.Key,
            DataType = Enum.Parse<CustomCriteriaDataType>(request.DataType, ignoreCase: true),
            Options = request.Options,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        repo.Add(criteria);
        await _uow.SaveChangesAsync(ct);

        return criteria.Id;
    }
}
