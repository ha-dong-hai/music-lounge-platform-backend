using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using CustomCriteriaEntity = MusicLounge.Domain.Entities.CustomCriteria;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.CustomCriteria.Commands.SetEventCustomValues;

internal sealed class SetEventCustomValuesCommandHandler : IRequestHandler<SetEventCustomValuesCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetEventCustomValuesCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetEventCustomValuesCommand request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền gắn tiêu chí cho sự kiện này.");

        if (request.Values.Count == 0) return Unit.Value;

        var criteriaIds = request.Values.Select(v => v.CriteriaId).Distinct().ToList();
        var criteria = await _uow.Repository<CustomCriteriaEntity, int>()
            .FindAsync(c => criteriaIds.Contains(c.Id), ct);
        var criteriaById = criteria.ToDictionary(c => c.Id);

        // Chi cho gan gia tri cua dung tieu chi thuoc venue nay — chan truong hop 1 Owner khac gui
        // CriteriaId cua venue khac vao show cua minh.
        foreach (var criteriaId in criteriaIds)
        {
            if (!criteriaById.TryGetValue(criteriaId, out var c))
                throw new NotFoundException(nameof(CustomCriteriaEntity), criteriaId);
            if (c.LoungeId != lounge.Id)
                throw new DomainException(
                    $"Tiêu chí #{criteriaId} không thuộc venue này.");
        }

        var valueRepo = _uow.Repository<EventCustomValue, int>();
        var existing = await valueRepo.FindAsync(
            v => v.ShowId == request.ShowId && criteriaIds.Contains(v.CriteriaId), ct);
        var existingByCriteria = existing.ToDictionary(v => v.CriteriaId);

        foreach (var input in request.Values)
        {
            if (existingByCriteria.TryGetValue(input.CriteriaId, out var row))
            {
                row.Value = input.Value;
                valueRepo.Update(row);
            }
            else
            {
                valueRepo.Add(new EventCustomValue
                {
                    ShowId = request.ShowId,
                    CriteriaId = input.CriteriaId,
                    Value = input.Value
                });
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
