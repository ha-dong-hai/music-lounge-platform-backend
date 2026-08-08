using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Auth.Jobs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;
using MusicLounge.Infrastructure.Hubs;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Repositories;
using MusicLounge.Infrastructure.Security;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        // Typed configuration
        services.Configure<VnPaySettings>(configuration.GetSection("VnPay"));
        services.Configure<BusinessSettings>(configuration.GetSection("Business"));
        services.Configure<CloudflareSettings>(configuration.GetSection("Cloudflare"));
        services.Configure<MuxSettings>(configuration.GetSection("Mux"));
        services.Configure<LivestreamSettings>(configuration.GetSection("Livestream"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<FirebaseSettings>(configuration.GetSection("Firebase"));
        services.Configure<EmailSettings>(configuration.GetSection("Email"));

        // DbContext
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseSqlServer(connectionString));

        // Generic Repository + UnitOfWork
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Specific Repositories
        services.AddScoped<ILoungeShowRepository, LoungeShowRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ILivestreamRepository, LivestreamRepository>();
        services.AddScoped<IEventModerationRepository, EventModerationRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IFollowRepository, FollowRepository>();
        services.AddScoped<ILoungeRepository, LoungeRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();

        // Services
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ISystemConfigService, SystemConfigService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAIRecommendationService, MLNetRecommendationService>();
        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IFcmService, FcmService>();
        services.AddScoped<ILivestreamHubService, LivestreamHubService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        // Default key ring (%LOCALAPPDATA%\ASP.NET\DataProtection-Keys, protected via per-user
        // DPAPI) is fine for a single interactive dev session but is a real risk for this app's
        // actual self-hosted deployment: it's tied to whichever Windows user profile the process
        // happens to run under, so a service-account change, a different machine, or profile loss
        // silently makes every already-encrypted CitizenCardNumber (IPiiEncryptionService) and any
        // in-flight Hangfire job argument (ISecretProtector) permanently undecryptable. Persisting
        // to a fixed path on disk with machine-level (not user-profile-level) DPAPI protection
        // survives all of that as long as it's the same machine. Scoped to this project's current
        // single-machine deployment — horizontal scaling to multiple instances/containers would
        // need a shared key store (e.g. a network share or blob storage) instead.
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("MusicLounge")
            .PersistKeysToFileSystem(new DirectoryInfo(
                Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "dataprotection-keys")));
        // Machine-level DPAPI is Windows-only (this app's actual deployment) — guarded rather than
        // called unconditionally so this doesn't crash if ever run on Linux; falls back to Data
        // Protection's own OS-appropriate default key protection there instead.
        if (OperatingSystem.IsWindows())
            dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPiiEncryptionService, PiiEncryptionService>();
        services.AddScoped<IAuthAttemptTracker, AuthAttemptTracker>();
        // Singleton: the per-show semaphore dictionary must be shared process-wide, not per-request.
        services.AddSingleton<IShowBookingLock, ShowBookingLock>();
        services.AddSingleton<IAsyncKeyedLock, AsyncKeyedLock>();
        services.AddSingleton<IChatRateLimiter, ChatRateLimiter>();
        services.AddScoped<ReleaseExpiredHoldsJob>();
        services.AddScoped<RefreshRecommendationsJob>();
        services.AddScoped<RefreshUserRecommendationJob>();
        services.AddScoped<AutoConfirmDonationsJob>();
        services.AddScoped<ExpireStuckDonationsJob>();
        services.AddScoped<CancelAbandonedPaymentsJob>();
        services.AddScoped<SettlementReleaseJob>();
        services.AddScoped<TicketTransferExpiryJob>();
        services.AddScoped<SubscriptionExpiryWarningJob>();
        services.AddScoped<ExpireSubscriptionsJob>();
        services.AddScoped<ApplyDuePenaltiesJob>();
        services.AddScoped<AutoApproveOverdueAppealsJob>();
        // W23/D-donation: both scheduled below via RecurringJob.AddOrUpdate but were missing
        // from DI — Hangfire would throw InvalidOperationException ("No service for type...")
        // the first time either fired, silently breaking event reminders and overdue-donation
        // checks in production. Found by empirically exercising every job under test.
        services.AddScoped<EventReminderJob>();
        services.AddScoped<DonationOverdueCheckJob>();
        // Same class of bug as EventReminderJob/DonationOverdueCheckJob above — LogUserBehaviourJob
        // is enqueued via BackgroundJob.Enqueue<LogUserBehaviourJob> but was never registered, so
        // Hangfire's activator would throw "No service for type... has been registered" the first
        // time it tried to run, silently breaking AI-recommendation behaviour logging in production
        // (never noticed because it's fire-and-forget from GetLoungeShowDetail/GetRecommendedLoungeShows,
        // with nothing surfacing the failure to a caller). SendPasswordResetEmailJob/
        // SendEmailVerificationCodeJob registered alongside since they're new as of this fix.
        services.AddScoped<LogUserBehaviourJob>();
        services.AddScoped<SendPasswordResetEmailJob>();
        services.AddScoped<SendEmailVerificationCodeJob>();

        // Livestream provider abstraction
        // Explicit timeout — HttpClient's default is 100s, long enough that one slow/hanging
        // third-party call (Cloudflare/Mux/Firebase) can tie up a request thread and, under load,
        // contribute to pool exhaustion for unrelated requests.
        var externalCallTimeout = TimeSpan.FromSeconds(30);
        services.AddHttpClient("cloudflare").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient("mux").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient("firebase").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddKeyedTransient<ILivestreamService, CloudflareStreamService>("cloudflare");
        services.AddKeyedTransient<ILivestreamService, MuxStreamService>("mux");
        services.AddScoped<ILivestreamServiceFactory, LivestreamServiceFactory>();

        // Hangfire
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer();

        return services;
    }

    public static void ConfigureRecurringJobs()
    {
        RecurringJob.AddOrUpdate<ReleaseExpiredHoldsJob>(
            "release-expired-holds",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Minutely());

        RecurringJob.AddOrUpdate<RefreshRecommendationsJob>(
            "refresh-recommendations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        RecurringJob.AddOrUpdate<AutoConfirmDonationsJob>(
            "auto-confirm-donations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        RecurringJob.AddOrUpdate<ExpireStuckDonationsJob>(
            "expire-stuck-donations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        RecurringJob.AddOrUpdate<CancelAbandonedPaymentsJob>(
            "cancel-abandoned-payments",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Minutely());

        RecurringJob.AddOrUpdate<SettlementReleaseJob>(
            "release-due-settlements",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        RecurringJob.AddOrUpdate<EventReminderJob>(
            "send-event-reminders",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        RecurringJob.AddOrUpdate<DonationOverdueCheckJob>(
            "check-overdue-donations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        RecurringJob.AddOrUpdate<TicketTransferExpiryJob>(
            "expire-ticket-transfers",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        RecurringJob.AddOrUpdate<SubscriptionExpiryWarningJob>(
            "warn-expiring-subscriptions",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        RecurringJob.AddOrUpdate<ExpireSubscriptionsJob>(
            "expire-subscriptions",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        RecurringJob.AddOrUpdate<ApplyDuePenaltiesJob>(
            "apply-due-venue-penalties",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        RecurringJob.AddOrUpdate<AutoApproveOverdueAppealsJob>(
            "auto-approve-overdue-appeals",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());
    }
}
