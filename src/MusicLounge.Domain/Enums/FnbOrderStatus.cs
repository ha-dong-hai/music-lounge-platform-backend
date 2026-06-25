// CoreFlow: CF3 (Ticket Booking — F&B during show)
// Lifecycle of a food and beverage order placed during a show.
namespace MusicLounge.Domain.Enums;

public enum FnbOrderStatus
{
    // Order placed — waiting for staff to accept
    Pending = 1,
    // Staff is preparing the order
    Preparing = 2,
    // Order delivered to the table
    Served = 3,
    // Payment collected — order closed
    Paid = 4
}
