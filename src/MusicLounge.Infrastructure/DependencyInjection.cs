using Hangfire;
using Hangfire.Common;
using Hangfire.SqlServer;
using Hangfire.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Auth.Jobs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Livestreams.Jobs;
using MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;
using MusicLounge.Application.Tickets.Commands.CheckInLivestreamViewer;
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
        services.Configure<AuthLockoutSettings>(configuration.GetSection("AuthLockout"));
        services.Configure<FirebaseSettings>(configuration.GetSection("Firebase"));
        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<GeminiSettings>(configuration.GetSection("Gemini"));
        services.Configure<OpenAiSettings>(configuration.GetSection("OpenAi"));
        services.Configure<SecurityDetectionSettings>(configuration.GetSection("SecurityDetection"));
        services.Configure<PanoramaStitcherSettings>(configuration.GetSection("PanoramaStitcher"));
        services.Configure<StorageSettings>(configuration.GetSection("Storage"));
        services.Configure<SmsSettings>(configuration.GetSection("Sms"));

        // MLACP-420: bang kiem cau hinh — thieu cai dat nao, hau qua ra sao.
        services.AddSingleton<IConfigurationAudit, Configuration.ConfigurationAudit>();

        // DbContext
        // MLACP-415: Azure SQL reset ket noi vai lan moi ngay ("an error occurred during the login process ... Connection
        // reset by peer" trong log 14-15/09). Khong bat retry thi moi lan nhu vay la mot loi 500 that su cho nguoi dung.
        // Di kem: TransactionBehavior mo transaction ben trong execution strategy (xem IUnitOfWork.ExecuteWithRetryAsync).
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null)));

        // Generic Repository + UnitOfWork
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Specific Repositories
        services.AddScoped<ILoungeShowRepository, LoungeShowRepository>();
        services.AddScoped<IInferredAiProfileRepository, InferredAiProfileRepository>();
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
        services.AddScoped<IAiModerationService, GeminiModerationService>();
        services.AddScoped<IImageModerationService, GeminiImageModerationService>();
        services.AddScoped<IImageModerationGate, ImageModerationGate>();
        services.AddScoped<IAiTextGenerationService, GeminiTextGenerationService>();
        // MLACP-418: co cau hinh Cloudflare thi dung Workers AI (co bac mien phi ~230 anh/ngay); khong thi quay ve OpenAI
        // (khong co bac mien phi). Chon o day thay vi trong handler de tang Application khong phai biet ten nha cung cap.
        services.AddScoped<IAiImageGenerationService>(sp =>
            AiImageProvider.UseCloudflare(sp.GetRequiredService<IOptions<CloudflareSettings>>().Value)
                ? ActivatorUtilities.CreateInstance<CloudflareImageGenerationService>(sp)
                : ActivatorUtilities.CreateInstance<OpenAiImageGenerationService>(sp));
        services.AddScoped<IPanoramaStitchingService, HttpPanoramaStitchingService>();
        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IMuxWebhookVerifier, MuxWebhookVerifier>();
        services.AddScoped<IFcmService, FcmService>();
        services.AddScoped<ILivestreamHubService, LivestreamHubService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenVerifier, GoogleTokenVerifier>();
        // MLACP-293: Firebase Storage khi có credential và bucket, ngược lại về đĩa cục bộ.
        // Chọn ở đây chứ không nhét nhánh if vào trong service: một service tự quyết mình có hoạt
        // động hay không sẽ phải mang theo cả hai cách lưu, và nhánh không dùng tới thì không ai
        // chạy. Cùng nếp "thiếu cấu hình thì suy biến, không ném lỗi" mà FcmService và SmsService
        // đang theo — nếu ném thì môi trường dev và toàn bộ test sập vì thiếu bí mật.
        services.AddScoped<IFileStorageService>(sp =>
            FileStorageSelector.UseFirebase(
                sp.GetRequiredService<IOptions<FirebaseSettings>>().Value)
                ? ActivatorUtilities.CreateInstance<FirebaseFileStorageService>(sp)
                : ActivatorUtilities.CreateInstance<LocalFileStorageService>(sp));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddSingleton<IImageSizeReader, MetadataImageSizeReader>();
        // MLACP-400. Bộ khoá Data Protection giữ khả năng giải mã mọi cột PII và tham số job Hangfire — mất nó là mất các giá
        // trị đó vĩnh viễn. DataProtectionKeyRing chọn chỗ lưu nằm ngoài thư mục site trên Azure và mang khoá cũ sang.
        services.AddMusicLoungeDataProtection(Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable);
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPiiEncryptionService, PiiEncryptionService>();
        services.AddScoped<IAuthAttemptTracker, AuthAttemptTracker>();
        // Singleton: the per-show semaphore dictionary must be shared process-wide, not per-request.
        // MLACP-396: Scoped (khong con Singleton) de khoa thay duoc transaction cua request — tu dien semaphore van la
        // static, dung chung toan tien trinh. Xem TransactionLockScope.
        services.AddScoped<TransactionLockScope>();
        services.AddScoped<ITransactionLockScope>(sp => sp.GetRequiredService<TransactionLockScope>());
        services.AddScoped<IShowBookingLock, ShowBookingLock>();
        services.AddScoped<IAsyncKeyedLock, AsyncKeyedLock>();
        services.AddSingleton<IChatRateLimiter, ChatRateLimiter>();
        services.AddScoped<ReleaseExpiredHoldsJob>();
        services.AddScoped<RefreshRecommendationsJob>();
        services.AddScoped<RecomputeUserEventScoresJob>();
        services.AddScoped<RefreshUserRecommendationJob>();
        services.AddScoped<AutoConfirmDonationsJob>();
        services.AddScoped<ExpireStuckDonationsJob>();
        services.AddScoped<CancelAbandonedPaymentsJob>();
        services.AddScoped<RemindOwnerToStartShowJob>();
        services.AddScoped<RefundUndeliveredLivestreamTicketsJob>();
        services.AddScoped<NotifyUndeliveredOfflineShowJob>();
        services.AddScoped<SettlementReleaseJob>();
        services.AddScoped<AutoEndStaleShowsJob>();
        services.AddScoped<RefundSlaBreachAlertJob>();
        services.AddScoped<AutoApproveOverdueRefundsJob>();
        services.AddScoped<TicketTransferExpiryJob>();
        services.AddScoped<SubscriptionExpiryWarningJob>();
        services.AddScoped<ExpireSubscriptionsJob>();
        services.AddScoped<ApplyDuePenaltiesJob>();
        services.AddScoped<ExpireServedSuspensionsJob>();
        services.AddScoped<AutoApproveOverdueAppealsJob>();
        services.AddScoped<ModerationSlaBreachAlertJob>();
        services.AddScoped<ContentReportSlaBreachAlertJob>();
        services.AddScoped<ComplaintSlaBreachAlertJob>();
        services.AddScoped<ScoreModerationWithAiJob>();
        services.AddScoped<StitchVenueTourSceneJob>();
        services.AddScoped<ExpireStuckStitchAttemptsJob>();
        services.AddScoped<LoginSpikeDetectionJob>();
        services.AddScoped<AdminRoleDriftDetectionJob>();
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
        // Same registration discipline as the two jobs above — see comment there.
        services.AddScoped<SendPhoneVerificationCodeJob>();
        services.AddScoped<PhoneVerificationSmsJob>();
        // Fourth and fifth instances of that exact bug, found in the đợt-2 audit by cross-checking
        // every *Job class in the codebase against this list. Both are enqueued for real by
        // HangfireBackgroundJobService (Schedule<> / Enqueue<>) and neither was registered, so both
        // threw "No service for type..." the first time Hangfire tried to activate them:
        //   LivestreamReconnectTimeoutJob — MLACP-191's whole reconnect feature was dead. A stream
        //     that lost its encoder stayed Reconnecting forever: never marked Failed, the show never
        //     ended, viewers kept seeing "reconnecting", and the rating window never opened.
        //   CheckInLivestreamViewerJob — livestream attendance was never recorded, and RateShow
        //     requires a real check-in, so livestream ticket holders could never rate a show they
        //     actually watched.
        services.AddScoped<LivestreamReconnectTimeoutJob>();
        services.AddScoped<CheckInLivestreamViewerJob>();

        // Livestream provider abstraction
        // Explicit timeout — HttpClient's default is 100s, long enough that one slow/hanging
        // third-party call (Cloudflare/Mux/Firebase) can tie up a request thread and, under load,
        // contribute to pool exhaustion for unrelated requests.
        var externalCallTimeout = TimeSpan.FromSeconds(30);
        services.AddHttpClient("cloudflare").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient("mux").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient("firebase").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient("gemini").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient("vnpay").ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        services.AddHttpClient(SmsService.HttpClientName).ConfigureHttpClient(c => c.Timeout = externalCallTimeout);
        // Image generation can run noticeably longer than the other external calls this app makes —
        // a longer, dedicated timeout instead of reusing externalCallTimeout so a legitimately slow
        // (not hung) generation doesn't get cut off right as it would have succeeded.
        services.AddHttpClient("openai").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(90));
        // Stitching several phone photos can genuinely take a while (feature detection + matching
        // + blending scales with image count/resolution) — longer than the other external calls.
        services.AddHttpClient("panorama-stitcher").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120));
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

    private static readonly List<string> RegisteredRecurringJobIds = [];

    /// <summary>
    /// Every recurring job id that <see cref="ConfigureRecurringJobs"/> actually registered.
    ///
    /// Recorded at the point of registration rather than kept as a hand-written list beside it.
    /// MLACP-295 needed a whitelist of triggerable jobs, and the version of that list carried on the
    /// other branch had already fallen ten jobs behind — including every SLA alert job. A list that
    /// has to be remembered is a list that goes stale, and the failure mode here is silent: Hangfire
    /// no-ops on an unknown id, so a job simply never runs when triggered.
    /// </summary>
    public static IReadOnlyList<string> RecurringJobIds => RegisteredRecurringJobIds;

    private static void Recurring<TJob>(
        IRecurringJobManager manager,
        string recurringJobId,
        System.Linq.Expressions.Expression<Func<TJob, Task>> methodCall,
        string cronExpression)
    {
        // MLACP-440: dung manager tuong minh thay RecurringJob.AddOrUpdate tinh — theo ma nguon Hangfire 1.8.17 ban tinh la
        // Lazy<RecurringJobManager>() bam vao JobStorage.Current o LAN DUNG DAU, con cung goi y het
        // AddOrUpdate(id, Job.FromExpression(expr), cron, new RecurringJobOptions()). Tuong minh de dang ky va buoc go job
        // mo coi chac chan chay tren CUNG mot storage.
        manager.AddOrUpdate(recurringJobId, Job.FromExpression(methodCall), cronExpression, new RecurringJobOptions());
        if (!RegisteredRecurringJobIds.Contains(recurringJobId))
            RegisteredRecurringJobIds.Add(recurringJobId);
    }

    /// <returns>MLACP-440: cac job dinh ky mo coi vua bi go khoi storage (xem RemoveUnregisteredRecurringJobs).</returns>
    public static IReadOnlyList<string> ConfigureRecurringJobs()
    {
        var storage = JobStorage.Current;
        var manager = new RecurringJobManager(storage);

        Recurring<ReleaseExpiredHoldsJob>(
            manager,
            "release-expired-holds",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Minutely());

        // Must run before refresh-recommendations so the collaborative-filtering matrix it feeds
        // (user_event_scores) is fresh when MLNetRecommendationService reads it — daily cadence
        // matches UserEventScore's own "aggregated periodically from behaviour logs" design intent,
        // not hourly like the recommendation refresh itself (aggregating every table this job reads
        // hourly would be wasted work for a signal that doesn't meaningfully shift that often).
        Recurring<RecomputeUserEventScoresJob>(
            manager,
            "recompute-user-event-scores",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily(3)); // 03:00 UTC, ahead of every hourly refresh-recommendations run that day

        Recurring<RefreshRecommendationsJob>(
            manager,
            "refresh-recommendations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<AutoConfirmDonationsJob>(
            manager,
            "auto-confirm-donations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<ExpireStuckDonationsJob>(
            manager,
            "expire-stuck-donations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        // MLACP-435: luot ghep anh ket Pending (job bi gian doan giua chung) — 10 phut mot lan, dong luot qua 30 phut.
        Recurring<ExpireStuckStitchAttemptsJob>(
            manager,
            "expire-stuck-stitch-attempts",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            "*/10 * * * *");

        Recurring<CancelAbandonedPaymentsJob>(
            manager,
            "cancel-abandoned-payments",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Minutely());

        // Moi phut: nhac cang som cang cuu duoc dem dien. Truy van chi cham cac show Published va
        // co chot chong trung nen khong gay tai va khong gui lap.
        Recurring<RemindOwnerToStartShowJob>(
            manager,
            "remind-owner-to-start-show",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Minutely());

        // Moi gio la du: bien an toan da la 6 tieng, nen som hon cung khong hoan duoc som hon.
        Recurring<RefundUndeliveredLivestreamTicketsJob>(
            manager,
            "refund-undelivered-livestream-tickets",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        // Moi gio: hai chang cua job nay deu do bang moc thoi gian tinh tu gio ket thuc du kien,
        // nen chay day hon cung khong bao som hon.
        Recurring<NotifyUndeliveredOfflineShowJob>(
            manager,
            "notify-undelivered-offline-show",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<SettlementReleaseJob>(
            manager,
            "release-due-settlements",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        // Hourly, not daily: this is what closes the cancellation window and opens the rating
        // window, so a whole day of drift is a whole day of tickets still refundable for a show
        // that already happened.
        Recurring<AutoEndStaleShowsJob>(
            manager,
            "auto-end-stale-shows",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<EventReminderJob>(
            manager,
            "send-event-reminders",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<DonationOverdueCheckJob>(
            manager,
            "check-overdue-donations",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        Recurring<TicketTransferExpiryJob>(
            manager,
            "expire-ticket-transfers",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<SubscriptionExpiryWarningJob>(
            manager,
            "warn-expiring-subscriptions",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        Recurring<ExpireSubscriptionsJob>(
            manager,
            "expire-subscriptions",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Daily());

        Recurring<ApplyDuePenaltiesJob>(
            manager,
            "apply-due-venue-penalties",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        // Nua con lai cua job tren: no AP lenh tam khoa, cai nay GO ra khi da phuc vu du han.
        // Thieu cai nay thi "tam khoa N ngay" tren thuc te la khoa vinh vien.
        Recurring<ExpireServedSuspensionsJob>(
            manager,
            "expire-served-suspensions",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<AutoApproveOverdueAppealsJob>(
            manager,
            "auto-approve-overdue-appeals",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<ModerationSlaBreachAlertJob>(
            manager,
            "alert-moderation-sla-breaches",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<ContentReportSlaBreachAlertJob>(
            manager,
            "alert-content-report-sla-breaches",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<ComplaintSlaBreachAlertJob>(
            manager,
            "alert-complaint-sla-breaches",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        Recurring<RefundSlaBreachAlertJob>(
            manager,
            "alert-refund-sla-breaches",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        // Moi gio, cung nhip voi canh bao SLA: moc tu duyet tinh bang gio ke tu luc tao yeu cau.
        Recurring<AutoApproveOverdueRefundsJob>(
            manager,
            "auto-approve-overdue-refunds",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        // Every 5 minutes against a 10-minute detection window, so a spike is never more than one
        // extra run away from being caught, while still cheap enough to poll this often.
        Recurring<LoginSpikeDetectionJob>(
            manager,
            "detect-login-spikes",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            "*/5 * * * *");

        Recurring<AdminRoleDriftDetectionJob>(
            manager,
            "detect-admin-role-drift",
            j => j.ExecuteAsync(JobCancellationToken.Null),
            Cron.Hourly());

        return RemoveUnregisteredRecurringJobs(storage, RegisteredRecurringJobIds);
    }

    /// <summary>
    /// MLACP-440. Go khoi storage moi job dinh ky ma code KHONG con dang ky.
    ///
    /// Hangfire khong bao gio tu xoa job dinh ky: code ngung dang ky (doi ten, bo job, hay mot ban deploy khac tung ghi vao
    /// cung database) thi ban ghi van nam trong storage va toi lich lai chay — hoac loi. Tren Azure 17/09 co 4 job nhu vay,
    /// deu "Could not load type" vi lop khong ton tai trong code nay (alert-push-failures, check-ledger-integrity,
    /// prune-stale-device-tokens, reconcile-vnpay-payments): dashboard bao 31 job trong khi code chi dang ky 27, va ten
    /// "reconcile-vnpay-payments" de khien nguoi van hanh tuong co doi soat VNPay.
    ///
    /// Cung nguyen tac voi RecurringJobIds (MLACP-295): danh sach dang ky tu code la nguon su that. Gia dinh chi MOT ban
    /// app dung storage nay — hai ban khac nhau cung tro vao mot database se go job cua nhau moi lan khoi dong.
    /// Nhan storage lam tham so de test dung storage rieng, khong dung JobStorage.Current dung chung.
    /// </summary>
    internal static IReadOnlyList<string> RemoveUnregisteredRecurringJobs(JobStorage storage, IReadOnlyCollection<string> registeredIds)
    {
        List<string> stale;
        using (var connection = storage.GetConnection())
        {
            stale = connection.GetRecurringJobs()
                .Select(j => j.Id)
                .Where(id => !registeredIds.Contains(id))
                .ToList();
        }

        var manager = new RecurringJobManager(storage);
        foreach (var id in stale)
            manager.RemoveIfExists(id);
        return stale;
    }
}
