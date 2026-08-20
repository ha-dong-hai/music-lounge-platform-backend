using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Performers.Queries.GetPerformerById;

internal sealed class GetPerformerByIdQueryHandler : IRequestHandler<GetPerformerByIdQuery, PerformerDto>
{
    private readonly IUnitOfWork _uow;

    public GetPerformerByIdQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PerformerDto> Handle(GetPerformerByIdQuery request, CancellationToken ct)
    {
        var performer = await _uow.Repository<Performer, int>().GetByIdAsync(request.PerformerId, ct)
            ?? throw new NotFoundException(nameof(Performer), request.PerformerId);

        var dtos = await PerformerDtoMapper.MapAsync(_uow, [performer], ct);
        return dtos[0];
    }
}
