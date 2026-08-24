using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Performers.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Performers.Queries.GetPerformers;

// §6.12: READ/ASSIGN — the shared-catalog autocomplete every Owner searches before deciding
// whether to reuse an existing Performer or create a new one.
internal sealed class GetPerformersQueryHandler
    : IRequestHandler<GetPerformersQuery, PaginatedResult<PerformerDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPerformersQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PaginatedResult<PerformerDto>> Handle(GetPerformersQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var search = request.Search?.Trim();

        var (performers, total) = await _uow.Repository<Performer, int>().GetPagedAsync(
            p => string.IsNullOrEmpty(search) || p.Name.Contains(search),
            p => p.Id,
            page, size, ct);

        var dtos = await PerformerDtoMapper.MapAsync(_uow, performers, ct);
        return new PaginatedResult<PerformerDto>(dtos, page, size, total);
    }
}
