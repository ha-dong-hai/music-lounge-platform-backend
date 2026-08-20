using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Complaints.DTOs;

namespace MusicLounge.Application.Complaints.Queries.GetPendingComplaints;

public sealed record GetPendingComplaintsQuery(int Page, int PageSize) : IQuery<PaginatedResult<ComplaintDto>>;
