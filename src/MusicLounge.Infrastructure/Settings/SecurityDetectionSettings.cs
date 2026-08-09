namespace MusicLounge.Infrastructure.Settings;

// Deliberately NOT in system_config (unlike every other tunable duration/threshold in this
// codebase — see ConfigKeys' D9 convention). system_config today has no application-layer write
// path at all (ISystemConfigService only exposes Get*), so the only way to change a system_config
// row is a direct database UPDATE — the exact same privilege level as the credential-stuffing /
// silent-Admin-promotion attacks LoginSpikeDetectionJob and AdminRoleDriftDetectionJob exist to
// catch. Putting these thresholds there would let an attacker who already has DB write access
// blind both detectors with one UPDATE statement. Living in appsettings instead means changing
// them needs server/deployment access (edit + restart), a materially different and higher
// privilege boundary — the same one already trusted for GeminiSettings/OpenAiSettings' API keys.
public sealed class SecurityDetectionSettings
{
    // LoginSpikeDetectionJob — credential-stuffing: same source IP failing login across many
    // different accounts within this window.
    public int LoginSpikeWindowMinutes { get; init; } = 10;
    public int LoginSpikeMinDistinctAccounts { get; init; } = 5;
    public int LoginSpikeMinTotalAttempts { get; init; } = 10;

    // How long to wait before re-alerting Admins about the same ongoing attack from one IP.
    public int LoginSpikeAlertCooldownHours { get; init; } = 1;

    // LoginFailureLog only needs to cover LoginSpikeWindowMinutes of history to do its job —
    // anything older is pruned so the table doesn't grow unbounded on a busy site.
    public int LoginFailureLogRetentionHours { get; init; } = 24;
}
