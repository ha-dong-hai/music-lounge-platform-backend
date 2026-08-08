using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IPaymentRepository : IRepository<Payment, int>
{
    /// <summary>
    /// Owner of the venue whose show the payment's tickets belong to. All tickets under a
    /// single payment share the same show (one purchase = one show), so any ticket works.
    /// Returns null if the payment has no linked tickets (e.g. a non-ticket payment type).
    /// </summary>
    Task<int?> GetTicketShowOwnerIdAsync(int paymentId, CancellationToken ct = default);
}
