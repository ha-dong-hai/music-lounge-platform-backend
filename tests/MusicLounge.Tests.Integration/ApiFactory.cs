using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Fakes;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Named shared-cache in-memory database (unique per factory instance so parallel test runs
    // never collide) instead of a single bare ":memory:" connection object. A bare ":memory:"
    // connection is destroyed the moment its own connection closes, so the old setup kept ONE
    // SqliteConnection object open and had every DbContext reuse it — which meant every request
    // shared a single physical connection, and SQLite only allows one active transaction per
    // connection. Concurrent requests (see SellWalkIn_ConcurrentRequestsAgainstLastSeat_OnlyOneSucceeds)
    // would then collide on "transaction already in progress" even though the app-level lock was
    // working correctly — a test-double artifact SQL Server production never has, since every
    // request there gets its own pooled connection. "cache=shared" lets each DbContext open its
    // own independent connection while all of them see the same in-memory data, matching how
    // production concurrency actually behaves.
    private readonly string _dbName = $"musiclounge_test_{Guid.NewGuid():N}";
    private SqliteConnection? _keepAliveConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // ── 1. Replace SQL Server DbContext with SQLite in-memory (shared cache) ──
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContext<ApplicationDbContext>(opts =>
                opts.UseSqlite($"DataSource=file:{_dbName}?mode=memory&cache=shared"));

            // ── 2. Replace Hangfire SQL Server storage with in-memory ─────────────
            // Remove only the Hangfire *library* descriptors registered by AddInfrastructure.
            // Namespace-based check (not a bare name Contains) so app types like
            // HangfireBackgroundJobService (implements IBackgroundJobService) survive —
            // they only call Hangfire's static API and work fine against in-memory storage.
            var hangfireDescriptors = services
                .Where(d =>
                    d.ServiceType.Namespace?.StartsWith("Hangfire") == true ||
                    d.ImplementationType?.Namespace?.StartsWith("Hangfire") == true)
                .ToList();
            foreach (var d in hangfireDescriptors) services.Remove(d);

            services.AddHangfire(cfg => cfg.UseInMemoryStorage(new InMemoryStorageOptions()));
            // Do NOT add AddHangfireServer() — no background processing in tests

            // ── 3. Replace external services with fakes ───────────────────────────
            services.RemoveAll<IVnPayService>();
            services.AddSingleton<IVnPayService, FakeVnPayService>();

            services.RemoveAll<ILivestreamServiceFactory>();
            services.AddSingleton<ILivestreamServiceFactory, FakeLivestreamServiceFactory>();

            // Remove keyed livestream services too (registered with AddKeyedTransient)
            var keyedLs = services
                .Where(d => d.ServiceType == typeof(ILivestreamService))
                .ToList();
            foreach (var d in keyedLs) services.Remove(d);

            services.RemoveAll<IAIRecommendationService>();
            services.AddSingleton<IAIRecommendationService, FakeAiService>();

            services.RemoveAll<IFcmService>();
            services.AddSingleton<IFcmService, FakeFcmService>();

            services.RemoveAll<IGoogleTokenVerifier>();
            services.AddSingleton<IGoogleTokenVerifier, FakeGoogleTokenVerifier>();

            // ── 4. Replace JWT auth with test header-based auth ───────────────────
            services.AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task InitializeAsync()
    {
        // Keep one connection to the shared-cache DB open for the factory's lifetime — the
        // in-memory database is destroyed once its last connection closes, so this is purely an
        // anchor; it is never used to run commands itself.
        _keepAliveConnection = new SqliteConnection($"DataSource=file:{_dbName}?mode=memory&cache=shared");
        await _keepAliveConnection.OpenAsync();
        await SeedHelper.SeedAsync(Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_keepAliveConnection is not null)
            await _keepAliveConnection.DisposeAsync();
    }

    // Helper: create an HTTP client pre-configured with test auth headers
    public HttpClient CreateAuthenticatedClient(
        int userId, string role, int? loungeId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, role);
        if (loungeId.HasValue)
            client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderLoungeId, loungeId.Value.ToString());
        return client;
    }
}
