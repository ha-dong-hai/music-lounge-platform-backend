using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.Queries.GetMyTickets;

// MLACP-97: Status khong bat buoc — null tra tat ca. Dung thang enum TicketStatus (Pending/
// Confirmed/Used/Cancelled/Refunded) thay vi tao rieng 1 nhom 3 gia tri "hieu luc/da dung/da huy" —
// giu dung 1 nguon su that voi trang thai ve that, giong cach GetMyLoungeShowsQuery da lam voi
// LoungeShowStatus.
public sealed record GetMyTicketsQuery(TicketStatus? Status = null, int Page = 1, int PageSize = 10)
    : IQuery<PaginatedResult<TicketListItemDto>>;
