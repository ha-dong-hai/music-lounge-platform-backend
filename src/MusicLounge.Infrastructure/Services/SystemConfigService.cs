using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;
using System.Globalization;

namespace MusicLounge.Infrastructure.Services;

internal sealed class SystemConfigService : ISystemConfigService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public SystemConfigService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw is not null
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw is not null
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw is not null && bool.TryParse(raw, out var value) ? value : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
    {
        var raw = await GetRawAsync(key, ct);
        return raw ?? fallback;
    }

    private async Task<string?> GetRawAsync(string key, CancellationToken ct)
        => await _cache.GetOrCreateAsync($"syscfg:{key}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            var config = await _db.SystemConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ConfigKey == key, ct);
            return config?.ConfigValue;
        });
}
