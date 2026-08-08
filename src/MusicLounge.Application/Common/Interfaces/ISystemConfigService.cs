namespace MusicLounge.Application.Common.Interfaces;

// D9: business parameters live in system_config (DB), not appsettings.
// Values are cached briefly — a config change takes effect within the cache window.
public interface ISystemConfigService
{
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
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

    // Not seeded by the original migration — GetIntAsync's fallback covers it until an Admin
    // adds a real row (D9: business parameters belong in system_config, not hardcoded).
    public const string EventReminderHours = "event_reminder_hours";
}
