using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;

namespace MusicLounge.Application.Complaints.Queries.GetMyComplaints;

internal sealed class GetMyComplaintsQueryHandler
    : IRequestHandler<GetMyComplaintsQuery, PaginatedResult<ComplaintDto>>
{
    private readonly IComplaintRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetMyComplaintsQueryHandler(IComplaintRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ComplaintDto>> Handle(GetMyComplaintsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        return await _repo.GetMyComplaintsAsync(_currentUser.UserId, page, size, ct);
    }
}
