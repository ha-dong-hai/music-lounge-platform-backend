using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class PaymentRepository : Repository<Payment, int>, IPaymentRepository
{
    private readonly ApplicationDbContext _ctx;

    public PaymentRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<int?> GetTicketShowOwnerIdAsync(int paymentId, CancellationToken ct = default)
        => await _ctx.Tickets
            .AsNoTracking()
            .Where(t => t.PaymentId == paymentId)
            .Select(t => (int?)t.Show.Lounge.OwnerId)
            .FirstOrDefaultAsync(ct);
}
