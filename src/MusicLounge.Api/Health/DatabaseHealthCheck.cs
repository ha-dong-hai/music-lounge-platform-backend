using Microsoft.Extensions.Diagnostics.HealthChecks;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Api.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;

    public DatabaseHealthCheck(ApplicationDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var canConnect = await _db.Database.CanConnectAsync(ct);
        return canConnect
            ? HealthCheckResult.Healthy("Database reachable.")
            : HealthCheckResult.Unhealthy("Cannot reach database.");
    }
}
