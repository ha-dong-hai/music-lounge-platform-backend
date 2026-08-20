namespace MusicLounge.Domain.Enums;

// D3: 2-stage settlement
public enum ReleaseType
{
    Partial70,  // pre_rate (50/70/80%) released before show
    Final30     // remaining released after show completes
}
