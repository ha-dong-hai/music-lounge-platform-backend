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
/// MLACP-288 gave the D13 columns a write path, so a show can now carry a policy its owner chose
/// rather than only the defaults. <see cref="Validate"/> lives here rather than in the two command
/// validators for the same reason <see cref="Resolve"/> does: a rule enforced in a validator but not
/// known to the resolver is a rule the disclosure text can contradict.
///
/// What the owner may NOT touch is the venue-at-fault case. When the venue cancels the show, takes
/// it down after a complaint, or switches it to a format the buyer did not pay for, every
/// platform-driven path refunds 100% and none of them reads RefundPercentage. That is the
/// protection that actually matters, it is hardcoded, and nothing in this class can weaken it —
/// which is why the owner-settable part can safely be as strict as the owner likes.
/// </summary>
public static class TicketRefundPolicy
{
    /// <summary>Applied when a show leaves RefundPercentage unset — matches CancelTicket's own fallback.</summary>
    public const decimal DefaultRefundPercentage = 100m;

    public static TicketRefundTerms Resolve(LoungeShow show)
    {
        var scheduledEnd = ShowSchedule.EffectiveEnd(show);

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

    /// <summary>
    /// The largest cancellation deadline a show may advertise, in hours before the start.
    ///
    /// A show cannot go on sale until it is submitted for review, and PublishLoungeShow already
    /// requires that to happen at least <c>publish_min_business_days_lead_time</c> business days
    /// (7, per NĐ 144/2020 Điều 10) before the show. A deadline longer than the sale window is a
    /// cancellation right that expired before the first ticket existed — the page would say
    /// "refundable" and the endpoint would say no, to every buyer, always. 14 days is the round
    /// number safely above that 7-business-day floor; the exact figure matters far less than there
    /// being a ceiling at all.
    /// </summary>
    public const int MaxCancellationDeadlineHours = 24 * 14;

    /// <summary>
    /// Checks a proposed policy on its own terms. Returns an error message, or null when the policy
    /// is coherent.
    ///
    /// None of these rules force the owner to be generous — "no self-cancellation at all" is a valid
    /// answer here, and is what several ticketing platforms default to. What they rule out is a
    /// policy that cannot mean what it says: a refund of 0% offered as if it were a refund, a
    /// percentage that is not a percentage, or settings recorded against a show that ignores them.
    /// </summary>
    /// <param name="scheduledStart">Used only to check the deadline is still reachable; pass the
    /// value being written, not the value already stored.</param>
    public static string? Validate(
        bool cancellationAllowed,
        decimal? refundPercentage,
        int? cancellationDeadlineHours,
        DateTimeOffset scheduledStart,
        DateTimeOffset now)
    {
        if (!cancellationAllowed)
        {
            // Storing a percentage or a deadline alongside "no cancellation" would leave the owner
            // believing they had configured something. Resolve() ignores both in that case, and
            // Describe() does not mention them, so the settings would exist only in the database.
            if (refundPercentage.HasValue || cancellationDeadlineHours.HasValue)
                return "Buổi hòa nhạc không cho phép tự hủy vé thì không đặt được tỉ lệ hoàn tiền " +
                       "hay hạn hủy — hai giá trị này sẽ không có tác dụng. Hãy bỏ trống chúng, " +
                       "hoặc bật lại quyền tự hủy vé.";
            return null;
        }

        if (refundPercentage is decimal pct)
        {
            if (pct <= 0m)
                return "Tỉ lệ hoàn tiền phải lớn hơn 0. Nếu bạn không muốn hoàn tiền khi người mua " +
                       "tự hủy vé, hãy tắt quyền tự hủy vé thay vì đặt mức hoàn 0% — người mua cần " +
                       "biết rõ vé không hoàn được trước khi thanh toán.";

            if (pct > 100m)
                return $"Tỉ lệ hoàn tiền phải nằm trong khoảng từ 0 đến 100 (100 nghĩa là hoàn đủ " +
                       $"tiền vé). Giá trị {pct} không hợp lệ.";

            // The percentage is multiplied into a money amount in CancelTicket; more precision than
            // the money itself carries would only round away later.
            if (decimal.Round(pct, 2) != pct)
                return "Tỉ lệ hoàn tiền chỉ nhận tối đa 2 chữ số thập phân.";
        }

        if (cancellationDeadlineHours is int hours)
        {
            if (hours < 1)
                return "Hạn hủy vé phải từ 1 giờ trở lên. Nếu bạn không muốn đặt hạn, hãy bỏ trống " +
                       "trường này — khi đó người mua được hủy tới trước lúc buổi diễn kết thúc.";

            if (hours > MaxCancellationDeadlineHours)
                return $"Hạn hủy vé tối đa là {MaxCancellationDeadlineHours} giờ " +
                       $"({MaxCancellationDeadlineHours / 24} ngày) trước giờ diễn. Đặt xa hơn thì " +
                       $"hạn hủy đã trôi qua trước cả khi vé được mở bán, nghĩa là công bố một quyền " +
                       $"hủy mà không người mua nào dùng được.";

            if (scheduledStart.AddHours(-hours) <= now)
                return $"Hạn hủy {hours} giờ trước giờ diễn đã trôi qua rồi so với lịch diễn hiện " +
                       $"tại ({VietnamTime.Format(scheduledStart)}). Hãy rút ngắn hạn hủy hoặc dời " +
                       $"lịch diễn ra xa hơn.";
        }

        return null;
    }

    /// <summary>
    /// Whether a stored policy can still actually be exercised by someone buying a ticket right now.
    ///
    /// Separate from <see cref="Validate"/> because time passes between the two: a policy that was
    /// reachable when the draft was written can be unreachable by the time the owner submits the
    /// show for review, without anyone editing anything.
    /// </summary>
    public static bool IsDeadlineStillReachable(LoungeShow show, DateTimeOffset now)
        => !show.CancellationAllowed
           || show.CancellationDeadlineHours is not int hours
           || show.ScheduledStart.AddHours(-hours) > now;

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
