using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Users.Queries.GetMyDataExport;

// DSAR (91/2025/QH15) data-portability endpoint — governance gap #1 from the 2026-08-09
// production-hardening audit. Assembles the PII this platform actually holds about the requesting
// user across every table with a direct FK to Users, synchronously (so the response itself is the
// acknowledgement — trivially inside the 2-day ACK SLA). Deliberately does NOT cover the erasure/
// deletion half of DSAR: hard-deleting a User row cascades through OwnerSubscription/LoungeStaff/
// MusicLounge (Restrict FKs would block it outright for an Owner with an active lounge) in ways
// that need a product decision on what "erasure" means for an Owner/Staff/Performer-adjacent
// account, not just an Audience one — see production-hardening-audit report for detail.
internal sealed class GetMyDataExportQueryHandler : IRequestHandler<GetMyDataExportQuery, MyDataExportDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMyDataExportQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<MyDataExportDto> Handle(GetMyDataExportQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        var user = await _uow.Repository<User, int>().GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(t => t.BuyerId == userId, ct);
        var donations = await _uow.Repository<Donation, int>().FindAsync(d => d.DonorUserId == userId, ct);
        var ratings = await _uow.Repository<LoungeShowRating, int>().FindAsync(r => r.UserId == userId, ct);
        var complaints = await _uow.Repository<Complaint, int>().FindAsync(c => c.ComplainantUserId == userId, ct);
        var follows = await _uow.Repository<Follow, int>().FindAsync(f => f.UserId == userId, ct);
        var wishlists = await _uow.Repository<ShowWishlist, int>().FindAsync(w => w.UserId == userId, ct);

        return new MyDataExportDto(
            new ExportedProfile(user.Id, user.Email, user.FullName, user.Phone, user.CreatedAt),
            tickets.Select(t => new ExportedTicket(t.Id, t.ShowId, t.Status.ToString(), t.CreatedAt)).ToList(),
            donations.Select(d => new ExportedDonation(d.Id, d.Gross, d.Status.ToString(), d.CreatedAt)).ToList(),
            ratings.Select(r => new ExportedRating(r.LoungeShowId, r.Score, r.Comment, r.CreatedAt)).ToList(),
            complaints.Select(c => new ExportedComplaint(c.Id, c.Category.ToString(), c.Status.ToString(), c.CreatedAt)).ToList(),
            follows.Select(f => f.LoungeId).ToList(),
            wishlists.Select(w => w.LoungeShowId).ToList());
    }
}
