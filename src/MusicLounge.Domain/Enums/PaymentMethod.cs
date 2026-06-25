// CoreFlow: CF3 (Ticket Booking), CF6 (Payment & Revenue)
// How the customer paid — online via payment gateway or cash at venue.
namespace MusicLounge.Domain.Enums;

public enum PaymentMethod
{
    // Paid via VNPay or other online gateway
    Gateway = 1,
    // Paid in cash at the venue (walk-in tickets, F&B orders)
    Cash = 2
}
