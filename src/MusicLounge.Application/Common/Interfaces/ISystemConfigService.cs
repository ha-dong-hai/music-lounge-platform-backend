namespace MusicLounge.Application.Common.Interfaces;

// D9: business parameters live in system_config (DB), not appsettings.
// Values are cached briefly — a config change takes effect within the cache window.
public interface ISystemConfigService
{
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
    Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default);
}

// Keys seeded by migration MM1_DBCompleteness100pct
public static class ConfigKeys
{
    public const string PlatformCommissionRate = "platform_commission_rate";
    public const string TaxRate = "tax_rate";

    // D3 payout-speed tiers, keyed by venue ReputationScore + completed-show count — replaces the
    // old flat "settlement_partial_pct" key (removed from the seed table; a stale reference to it
    // silently fell back to a hardcoded 0.70m for every venue regardless of standing). See
    // ScheduleSettlementHandler for the tier-selection logic these four feed.
    public const string SettlementTierNewPreRate = "settlement_tier_new_pre_rate";
    public const string SettlementTierStandardPreRate = "settlement_tier_standard_pre_rate";
    public const string SettlementTierPremiumPreRate = "settlement_tier_premium_pre_rate";
    public const string SettlementTierStandardMinScore = "settlement_tier_standard_min_score";
    public const string SettlementTierPremiumMinScore = "settlement_tier_premium_min_score";
    public const string SettlementTierPremiumMinShows = "settlement_tier_premium_min_shows";

    // Research-grounded (Eventbrite: payout processing begins ~3 days post-event, up to 14 business
    // days for large-event final settlement/reconciliation) — matches this system's existing D3
    // two-stage timing (partial fast, final held longer for the refund/chargeback window to pass).
    // Previously hardcoded in ScheduleSettlementHandler; these replace the old "settlement_days_before"/
    // "settlement_days_after" keys, whose seeded description ("before show end") never matched the
    // code's actual (and, per research, more defensible) "after show end" behavior.
    public const string SettlementPartialHoursAfterShow = "settlement_partial_hours_after_show";
    public const string SettlementFinalDaysAfterShow = "settlement_final_days_after_show";

    public const string TicketHoldMinutes = "ticket_hold_minutes";

    // Research-grounded (Upwork: 14-day auto-release if unresponded; Fiverr: 3-day response window
    // + 14-day grace period) — an escrow-style "intermediary doesn't respond, so protect the payee"
    // window. 24h (this code's previous hardcoded value) is far more aggressive than any researched
    // comparable; the originally-seeded 7 days is the better-grounded number, now actually wired in.
    public const string DonationHoldDays = "donation_hold_days";

    public const string ModerationSlaHours = "moderation_sla_hours";

    // §6.13 — window after a show ends during which a buyer may submit a rating. Seeded since the
    // original migration but never actually read — TerminateLivestreamCommandHandler/
    // EndLivestreamCommandHandler/EndLoungeShowCommandHandler each hardcoded their own literal
    // `AddDays(7)` that happened to match this key's seeded value by coincidence, not by wiring.
    public const string RatingWindowDays = "rating_window_days";

    // §6.17 — hours an Admin has to resolve a venue's penalty appeal before AutoApproveOverdueAppealsJob
    // auto-overturns it. Same dead-seed situation as RatingWindowDays: seeded since the original
    // migration, but SubmitAppealCommandHandler hardcoded `AddHours(48)` independently.
    public const string AppealSlaHours = "appeal_sla_hours";

    // D18 (NĐ 144/2020/NĐ-CP Điều 10): minimum business-day lead time between submitting a ticketed
    // show for moderation (or rescheduling one) and its scheduled date. Was hardcoded as a bare `7`
    // literal in both PublishLoungeShowCommandHandler and RescheduleLoungeShowCommandHandler despite
    // PublishLoungeShowCommandHandler's own comment on the very next config read ("§6.7: never
    // hardcode") — the statutory minimum itself (7 business days) is unchanged, only how it's stored.
    public const string PublishMinBusinessDaysLeadTime = "publish_min_business_days_lead_time";

    // §6.8 — notice window before a Suspension/Ban penalty actually takes effect (venue status
    // change + subscription compensation), giving the Owner time to see the notification and
    // appeal before it bites. Warning has no delay (applied immediately, not config-gated).
    public const string PenaltySuspensionNoticeHours = "penalty_suspension_notice_hours";
    public const string PenaltyBanNoticeDays = "penalty_ban_notice_days";

    // Anti-abuse ceilings on a single hold/walk-in-sale/donation — not statutory figures, but
    // operational limits that should be Admin-tunable (D9) rather than baked into validator code.
    // Defaults preserve this system's existing behavior exactly; only the storage moved.
    public const string TicketHoldMaxQuantity = "ticket_hold_max_quantity";
    public const string WalkInTicketMaxQuantity = "walkin_ticket_max_quantity";
    public const string DonationMaxAmount = "donation_max_amount";

    // Window before an unanswered ticket-transfer request auto-cancels (TicketTransferExpiryJob).
    public const string TicketTransferExpiryHours = "ticket_transfer_expiry_hours";

    // D16: min actual/scheduled show-duration ratio for SettlementReleaseJob to auto-release a
    // Final30 tranche rather than parking it PendingReview. Was already seeded and already read —
    // just via a raw string literal instead of this constant, the one place in the job that didn't
    // follow the rest of the codebase's ConfigKeys convention.
    public const string SettlementCompletionThresholdPct = "settlement_completion_threshold_pct";

    // Global anti-abuse ceiling on AI poster generation ATTEMPTS (success + failure both count) per
    // show — distinct from SubscriptionPackage.MaxAiPostersPerMonth, which is the per-Owner monthly
    // billing quota (only successful generations count against it). This cap exists purely to stop
    // one show from looping the vendor call indefinitely regardless of tier; it is not a
    // tier-differentiator, so it lives in system_config rather than on the package.
    public const string AiPosterMaxAttemptsPerShow = "ai_poster_max_attempts_per_show";

    // NĐ 85/2021's complaint-channel requirement doesn't itself specify a numeric deadline — this is
    // a reasonable operational default (Admin-tunable), not a literal statutory figure.
    public const string ComplaintSlaHours = "complaint_sla_hours";

    // Version label of the currently-published Terms of Service / Privacy Policy — bump this (via
    // Admin config update, no deploy needed) whenever the actual document changes, so newly-consenting
    // users snapshot the version they agreed to (User.TermsVersion) distinctly from users who agreed
    // to an earlier one. The document CONTENT itself is not a backend concern — see the placeholder
    // note on CurrentTermsVersionDefault.
    public const string CurrentTermsVersion = "current_terms_version";

    // No real Terms of Service / Privacy Policy document exists yet as of this fix (2026-08-09) —
    // that's a legal-drafting task for the platform owner/counsel, not something to fabricate here.
    // This infrastructure (consent capture, versioning) is ready the moment a real document exists;
    // until then this placeholder label is what gets stamped onto new users' TermsVersion.
    public const string CurrentTermsVersionDefault = "v0-placeholder-pending-legal-review";

    // §6.5 chặng 2: fraction of the ORIGINAL gross donation the owner forwards to the performer
    // (owner keeps the rest of their chặng-1 net as compensation for holding/administering the
    // donation). Default 0.88 matches docs/04-design-decisions.md §6.5 — benchmarked 2026-08-09
    // against industry donation/tip intermediary practice (YouTube Super Chat keeps 30%, Twitch
    // Bits nets creators ~55-71%; venue-holds-tip-for-performer arrangements commonly run 0-20%+
    // house cut) and found generous to the performer, not an outlier. Admin-tunable via
    // system_config, not hardcoded (§6.7) — ConfirmDonationPaidCommandHandler re-reads this at
    // confirmation time, so a rate change applies to donations confirmed after the change without
    // a deploy.
    public const string DonationPerformerShareRate = "donation_performer_share_rate";

    // Not seeded by the original migration — GetIntAsync's fallback covers it until an Admin
    // adds a real row (D9: business parameters belong in system_config, not hardcoded).
    public const string EventReminderHours = "event_reminder_hours";

    // Off by default (product decision 2026-08-09): walk-in/box-office payments (PaymentMethod.Cash)
    // are collected directly by the venue and never touch the platform's own gateway account, so by
    // default they don't go through the ledger or get a settlement payout scheduled — the venue
    // already has the money, and platform revenue on walk-in sales comes from the subscription fee
    // instead. Flipping this on restores the pre-2026-08-09 behavior (cash payments treated exactly
    // like a Gateway payment: full ledger journal + a real scheduled bank payout of the owner's
    // share). Only turn this on for a setup where money from walk-in sales genuinely does reach the
    // platform (e.g. a platform-operated card terminal at the venue) — turning it on for sales the
    // venue still collects directly recreates the double-payment bug this config replaced (owner
    // keeps the cash AND receives a real bank transfer for the same sale).
    public const string WalkInCommissionEnabled = "walkin_commission_enabled";

    // Anti-abuse ceiling on panorama-STITCH attempts (success + failure both count) per venue —
    // same shape as AiPosterMaxAttemptsPerShow, but for a different reason: a stitch runs on our
    // OWN server's CPU (the standalone panorama-stitcher service), not a paid vendor call, so an
    // unbounded retry loop is a direct cost/DoS vector rather than just a wasted bill. Not seeded
    // by any migration — GetIntAsync's fallback covers it until an Admin adds a real row.
    public const string TourStitchMaxAttemptsPerLounge = "tour_stitch_max_attempts_per_lounge";
}
