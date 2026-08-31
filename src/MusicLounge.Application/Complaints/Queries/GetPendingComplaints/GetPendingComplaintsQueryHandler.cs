using MediatR;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;

namespace MusicLounge.Application.Complaints.Queries.GetPendingComplaints;

internal sealed class GetPendingComplaintsQueryHandler
    : IRequestHandler<GetPendingComplaintsQuery, PaginatedResult<ComplaintDto>>
{
    private readonly IComplaintRepository _repo;

    public GetPendingComplaintsQueryHandler(IComplaintRepository repo) => _repo = repo;

    public async Task<PaginatedResult<ComplaintDto>> Handle(GetPendingComplaintsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        return await _repo.GetPendingAsync(page, size, ct);
    }
}
