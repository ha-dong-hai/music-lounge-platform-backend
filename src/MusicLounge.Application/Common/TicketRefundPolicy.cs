using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Resolves a show's ticket-cancellation policy into the concrete terms that actually apply to a
/// buyer, from the D13 columns plus their fallbacks.
///
/// The point of putting this in one place is that the policy is now BOTH shown to the buyer before
/// they pay (GetLoungeShowDetail) and enforced when they cancel (CancelTicket). Those two had no
/// reason to agree while the fallback logic lived inline in the handler — and a platform that
/// advertises one refund policy and applies another is worse off than one that advertises nothing.
///
/// Note on today's data: <see cref="LoungeShow.RefundPercentage"/> and
/// <see cref="LoungeShow.CancellationDeadlineHours"/> are written by NOTHING in this solution, and
/// no endpoint accepts them — the D13 policy columns exist in the schema and are read here and by
/// CancelTicket, but there is no way to ever set them. Every show therefore resolves to the
/// defaults below: cancellable, 100% refund, no deadline beyond the show itself. That is a generous
/// policy the audience currently has no way of knowing about, which is exactly why disclosing it
/// matters more than tightening it.
/// </summary>
public static class TicketRefundPolicy
{
    /// <summary>Applied when a show leaves RefundPercentage unset — matches CancelTicket's own fallback.</summary>
    public const decimal DefaultRefundPercentage = 100m;

    public static TicketRefundTerms Resolve(LoungeShow show)
    {
        var scheduledEnd = show.ScheduledEnd ?? show.ScheduledStart.AddHours(4);

        // Mirrors CancelTicketCommandHandler's guards, in the same order, so the advertised terms
        // are the enforced ones:
        //   1. CancellationAllowed == false            -> no cancellation at all
        //   2. Status == Ended, or a Published show     -> too late; the show has happened
        //      already past its scheduled end
        //   3. CancellationDeadlineHours, when set      -> must cancel this long before the start
        var deadline = show.CancellationDeadlineHours is int hours
            ? show.ScheduledStart.AddHours(-hours)
            : scheduledEnd;

        return new TicketRefundTerms(
            CancellationAllowed: show.CancellationAllowed,
            RefundPercentage: show.RefundPercentage ?? DefaultRefundPercentage,
            CancelBefore: show.CancellationAllowed ? deadline : null,
            DeadlineHoursBeforeStart: show.CancellationDeadlineHours,
            // Independent of everything above: whenever the venue is at fault — the show is
            // cancelled, taken down after a complaint, or switched to a format the buyer did not
            // pay for — every platform-driven path hardcodes a full refund rather than reading
            // RefundPercentage. Buyers should be told that, because it is the protection that
            // actually matters and it does not depend on the venue's own generosity.
            AlwaysFullRefundIfVenueCancels: true);
    }

    /// <summary>Human-readable Vietnamese summary for display next to the ticket tiers.</summary>
    public static string Describe(TicketRefundTerms terms)
    {
        if (!terms.CancellationAllowed)
            return "Buổi hòa nhạc này không cho phép tự hủy vé. Nếu phòng trà hủy buổi diễn, " +
                   "bạn vẫn được hoàn 100% tiền vé.";

        var percentPart = terms.RefundPercentage >= 100m
            ? "hoàn 100% tiền vé"
            : $"hoàn {terms.RefundPercentage:0.##}% tiền vé";

        var deadlinePart = terms.DeadlineHoursBeforeStart is int hours
            ? $"nếu hủy trước giờ diễn ít nhất {hours} giờ"
            : "nếu hủy trước khi buổi diễn kết thúc";

        return $"Bạn có thể tự hủy vé và được {percentPart} {deadlinePart}. " +
               "Nếu phòng trà hủy buổi diễn, bạn luôn được hoàn 100% bất kể điều kiện trên.";
    }
}

/// <param name="CancelBefore">
/// The moment after which self-cancellation stops being possible; null when cancellation is not
/// allowed at all.
/// </param>
/// <param name="AlwaysFullRefundIfVenueCancels">
/// Always true today — every venue-at-fault path refunds 100% regardless of the show's own policy.
/// Exposed as a field rather than assumed by the client so that if that ever stops being
/// unconditional, the client is told instead of silently advertising a guarantee that has lapsed.
/// </param>
public sealed record TicketRefundTerms(
    bool CancellationAllowed,
    decimal RefundPercentage,
    DateTimeOffset? CancelBefore,
    int? DeadlineHoursBeforeStart,
    bool AlwaysFullRefundIfVenueCancels);
