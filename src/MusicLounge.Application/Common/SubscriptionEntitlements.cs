using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Works out which per-show ticket cap actually applies to a venue.
///
/// Every one of the four enforcement sites used to read <c>if (activeSub is not null) { check }</c>,
/// which meant a venue with no subscription had no cap at all — and PublishLoungeShow does not
/// require a subscription either, so never buying a plan was the way to sell without limit. The
/// same codebase fails CLOSED for the AI poster feature; a cost to the platform was gated while a
/// revenue gate was not.
///
/// The tempting fix — make the sale path refuse when there is no plan — is the wrong one. It does
/// not punish the venue; it punishes the audience trying to buy a ticket for a show that is already
/// published and advertised. Comparable SaaS practice says the same thing from the billing side:
/// grace periods and gradual restriction, never an abrupt cutoff.
///
/// So the cap is never absent, it just has a different source: the active plan's snapshot when
/// there is one, an explicit free-tier number when there is not. "No plan" stops meaning "no limit"
/// and starts meaning "the smallest limit", which is a policy someone chose rather than the absence
/// of one.
/// </summary>
public static class SubscriptionEntitlements
{
    /// <summary>Used when the venue has no active plan and config supplies no value.</summary>
    public const int DefaultFreeTierMaxTicketsPerEvent = 50;

    /// <summary>
    /// Picks the owner's current plan, if any. Mirrors the selection every call site was already
    /// doing by hand: Active status AND not yet expired — a row can sit at Active past its expiry
    /// until ExpireSubscriptionsJob next runs, so the date check is not redundant.
    /// </summary>
    public static OwnerSubscription? ActivePlan(IEnumerable<OwnerSubscription> subscriptions, DateTimeOffset now)
        => subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.ExpiresAt > now)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

    /// <summary>
    /// The per-show ticket cap in force, and where it came from. The source is returned so the
    /// error message can tell an Owner whether they hit their plan's limit or the free-tier one —
    /// those call for completely different next steps.
    /// </summary>
    public static TicketCap ResolveTicketCap(OwnerSubscription? activePlan, int freeTierCap)
        => activePlan is not null
            ? new TicketCap(activePlan.MaxTicketsPerEventSnapshot, FromActivePlan: true)
            : new TicketCap(freeTierCap, FromActivePlan: false);
}

public sealed record TicketCap(int MaxTicketsPerEvent, bool FromActivePlan)
{
    /// <summary>Message shown when a request would cross the cap, phrased for whoever hit it.</summary>
    public string ExceededMessage(string what) => FromActivePlan
        ? $"{what} vượt giới hạn {MaxTicketsPerEvent} vé/buổi hòa nhạc của gói subscription hiện tại. " +
          "Owner của venue có thể nâng cấp gói để tăng giới hạn này (xem GET /subscriptions/packages)."
        : $"{what} vượt giới hạn {MaxTicketsPerEvent} vé/buổi hòa nhạc dành cho venue chưa đăng ký gói " +
          "subscription. Đăng ký một gói để mở rộng giới hạn này (xem GET /subscriptions/packages).";
}
