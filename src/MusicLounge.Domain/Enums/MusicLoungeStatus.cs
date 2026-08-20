// CoreFlow: CF1 (Event Management), CF6 (Payment & Revenue)
// Controls whether a venue is allowed to operate on the platform.
// Only Admin can change this status.
namespace MusicLounge.Domain.Enums;

public enum MusicLoungeStatus
{
    // Newly registered — waiting for Admin to review business license
    Pending = 1,
    // Verified and active — can create events and sell tickets
    Approved = 2,
    // Received a formal warning — still operational but under watch
    Warned = 3,
    // Temporarily blocked — subscription extended by suspension_days when lifted
    Suspended = 4,
    // Permanently banned — cannot be reactivated
    Locked = 5
}
