using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Application.Common;

// [Authorize(Policy = Policies.RequireStaff)] only proves role Staff/Admin — nothing about WHICH
// venue. Staff prove that via the "lounge_id" JWT claim (1 venue at a time by design, see
// LoungeStaffConfiguration). Owner accounts never carry that claim — an Owner can own more than
// one lounge, so a single static claim can't scope them — so every RequireStaff-gated handler that
// re-derived "_currentUser.LoungeId == resource.LoungeId" locked Owner out of operating their own,
// unstaffed venue (start/end their own show, check in guests, sell walk-in tickets, run F&B,
// control their own livestream). Centralize the rule so Owner is checked the same way the
// RequireOwner surface already checks resource ownership, instead of every handler re-deriving
// (and forgetting) it.
public static class VenueOperatorAccess
{
    public static bool CanOperate(ICurrentUserService currentUser, int loungeId, int loungeOwnerId) =>
        currentUser.Role == Roles.Admin
        || (currentUser.Role == Roles.Staff && currentUser.LoungeId == loungeId)
        || (currentUser.Role == Roles.Owner && currentUser.UserId == loungeOwnerId);
}
