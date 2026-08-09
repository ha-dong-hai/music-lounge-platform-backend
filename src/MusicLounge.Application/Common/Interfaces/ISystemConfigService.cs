namespace MusicLounge.Application.Common.Interfaces;

// D9: business parameters live in system_config (DB), not appsettings.
// Values are cached briefly — a config change takes effect within the cache window.
public interface ISystemConfigService
{
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
}

// Keys seeded by migration MM1_DBCompleteness100pct
public static class ConfigKeys
{
    public const string PlatformCommissionRate = "platform_commission_rate";
    public const string TaxRate = "tax_rate";
    public const string SettlementPartialPct = "settlement_partial_pct";
    public const string SettlementDaysBefore = "settlement_days_before";
    public const string SettlementDaysAfter = "settlement_days_after";
    public const string TicketHoldMinutes = "ticket_hold_minutes";
    public const string DonationHoldDays = "donation_hold_days";
    public const string ModerationSlaHours = "moderation_sla_hours";

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
}
