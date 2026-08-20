namespace MusicLounge.Domain.Enums;

public enum SettlementReleaseType
{
    Partial70,  // D3: first tranche — pre_rate% of net (e.g. 70%)
    Final30     // D3: second tranche — remaining post_rate% (e.g. 30%)
}
