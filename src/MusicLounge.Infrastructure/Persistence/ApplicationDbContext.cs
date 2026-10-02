using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Entities;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MusicLoungeVenue> Lounges => Set<MusicLoungeVenue>();
    public DbSet<LoungeShow> LoungeShows => Set<LoungeShow>();
    public DbSet<Performer> Performers => Set<Performer>();
    public DbSet<PerformerSocialLink> PerformerSocialLinks => Set<PerformerSocialLink>();
    public DbSet<VenueTourScene> VenueTourScenes => Set<VenueTourScene>();
    public DbSet<VenueTourHotspot> VenueTourHotspots => Set<VenueTourHotspot>();
    public DbSet<LoungeGalleryImage> LoungeGalleryImages => Set<LoungeGalleryImage>();
    public DbSet<VenueTourStitchAttempt> VenueTourStitchAttempts => Set<VenueTourStitchAttempt>();
    public DbSet<Performance> Performances => Set<Performance>();
    public DbSet<TicketTier> TicketTiers => Set<TicketTier>();
    public DbSet<TicketPrice> TicketPrices => Set<TicketPrice>();
    public DbSet<MusicGenre> Genres => Set<MusicGenre>();
    public DbSet<Mood> Moods => Set<Mood>();
    public DbSet<VenueAtmosphere> Atmospheres => Set<VenueAtmosphere>();
    public DbSet<LoungeShowGenre> LoungeShowGenres => Set<LoungeShowGenre>();
    public DbSet<LoungeShowMood> LoungeShowMoods => Set<LoungeShowMood>();
    public DbSet<LoungeShowAtmosphere> LoungeShowAtmospheres => Set<LoungeShowAtmosphere>();
    public DbSet<PerformerGenre> PerformerGenres => Set<PerformerGenre>();
    public DbSet<UserFavouriteGenre> UserFavouriteGenres => Set<UserFavouriteGenre>();
    public DbSet<UserFavouriteMood> UserFavouriteMoods => Set<UserFavouriteMood>();
    public DbSet<UserFavouriteAtmosphere> UserFavouriteAtmospheres => Set<UserFavouriteAtmosphere>();
    public DbSet<ShowWishlist> Wishlists => Set<ShowWishlist>();
    public DbSet<Follow> Follows => Set<Follow>();
    // MLACP-330: hai tin hieu tieu cuc, doi xung voi Follows va UserFavouriteGenres.
    public DbSet<LoungeMute> LoungeMutes => Set<LoungeMute>();
    public DbSet<UserDislikedGenre> UserDislikedGenres => Set<UserDislikedGenre>();
    public DbSet<UserBehaviourLog> BehaviourLogs => Set<UserBehaviourLog>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<LoungeShowRating> Ratings => Set<LoungeShowRating>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<PhysicalTicketDetail> PhysicalTicketDetails => Set<PhysicalTicketDetail>();
    public DbSet<LivestreamTicketDetail> LivestreamTicketDetails => Set<LivestreamTicketDetail>();
    public DbSet<TicketHold> TicketHolds => Set<TicketHold>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Livestream> Livestreams => Set<Livestream>();
    public DbSet<LivestreamChatMessage> LivestreamChatMessages => Set<LivestreamChatMessage>();
    public DbSet<LivestreamViewingSession> LivestreamViewingSessions => Set<LivestreamViewingSession>();
    public DbSet<EventModeration> EventModerations => Set<EventModeration>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<AiPosterGeneration> AiPosterGenerations => Set<AiPosterGeneration>();
    public DbSet<Donation> Donations => Set<Donation>();

    // --- N1: Identity extensions ---
    public DbSet<LoungeStaff> LoungeStaff => Set<LoungeStaff>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Account> LedgerAccounts => Set<Account>();

    // --- N3: Venue extensions ---
    public DbSet<SeatingZone> SeatingZones => Set<SeatingZone>();
    public DbSet<LoungeImage> LoungeImages => Set<LoungeImage>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    // --- N4: Subscription ---
    public DbSet<SubscriptionPackage> SubscriptionPackages => Set<SubscriptionPackage>();
    public DbSet<OwnerSubscription> OwnerSubscriptions => Set<OwnerSubscription>();

    // --- N10: Refunds ---
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();

    // --- N11: F&B ---
    public DbSet<FnbMenu> FnbMenus => Set<FnbMenu>();
    public DbSet<FnbMenuItem> FnbMenuItems => Set<FnbMenuItem>();
    public DbSet<FnbOrder> FnbOrders => Set<FnbOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // --- N12: AI input extensions ---
    public DbSet<UserEventScore> UserEventScores => Set<UserEventScore>();

    // --- N14: Notifications ---
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<LoginFailureLog> LoginFailureLogs => Set<LoginFailureLog>();
    public DbSet<LoginSpikeAlertState> LoginSpikeAlertStates => Set<LoginSpikeAlertState>();
    public DbSet<KnownAdminSnapshot> KnownAdminSnapshots => Set<KnownAdminSnapshot>();

    // --- N15: Moderation extensions ---
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<VenuePenalty> VenuePenalties => Set<VenuePenalty>();

    // --- N16: System config ---
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<SystemConfigHistory> SystemConfigHistory => Set<SystemConfigHistory>();

    // --- N17: AI Custom criteria ---
    public DbSet<CustomCriteria> CustomCriteria => Set<CustomCriteria>();
    public DbSet<EventCustomValue> EventCustomValues => Set<EventCustomValue>();
    public DbSet<UserCustomPreference> UserCustomPreferences => Set<UserCustomPreference>();

    // MLACP-526: cột DateTime (datetime2) không lưu múi giờ, nên EF đọc lên Kind=Unspecified, và phép đổi ngầm sang
    // DateTimeOffset ở DTO gắn múi giờ CỦA MÁY CHỦ. Azure chạy UTC nên không lộ; máy +07 thì lệch 7 tiếng ("đơn vừa đặt
    // 7 giờ trước", E2E 02/10). Mọi chỗ ghi đều là DateTime.UtcNow (đã kiểm: không có DateTime.Now), nên đọc lên đánh
    // dấu Utc là đúng. Đây là "đường nâng cấp" MLACP-475 ghi ở TimestampsCarryOffsetTests — thực tế chỉ 2 cột
    // (AuditableEntity.CreatedAt/UpdatedAt); quy ước áp cho mọi DateTime nên cột thêm sau cũng được bao phủ.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcKindDateTimeConverter>();
    }

    private sealed class UtcKindDateTimeConverter()
        : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // MLACP-515 (D-19): MỌI khoá chính GUID một cột lấy giá trị từ OrderedGuid — một chỗ duy nhất, để thứ tự "mới nhất
        // trước theo Id" đúng như nhau trên SQL Server (production) và SQLite (test). Không đặt ở từng Configuration: quên
        // một bảng là bảng đó sắp lộn xộn mà không test nào báo.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var pk = entityType.FindPrimaryKey();
            // Bỏ qua khoá chính ĐỒNG THỜI là khoá ngoại (quan hệ 1-1 dùng chung khoá, vd PhysicalTicketDetail.TicketId):
            // giá trị phải lấy đúng từ bản ghi cha. Sinh GUID mới ở đây làm chi tiết vé lạc sang vé khác — test check-in
            // bắt được (vé mới quét lần đầu đã báo "đã check-in").
            if (pk is { Properties.Count: 1 } && pk.Properties[0].ClrType == typeof(Guid) && !entityType.IsOwned()
                && !pk.Properties[0].IsForeignKey())
                modelBuilder.Entity(entityType.ClrType).Property(pk.Properties[0].Name)
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<OrderedGuidValueGenerator>();
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var userId = _currentUser?.IsAuthenticated == true ? _currentUser.UserId : (Guid?)null;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userId;
            }
        }

        return base.SaveChangesAsync(ct);
    }
}
