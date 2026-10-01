using MusicLounge.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// Seeds baseline data. IDs are fixed so tests can reference them directly.
/// User IDs: 1=Admin, 2=Staff, 3=Owner, 4=Audience, 5=OtherOwner
/// Endpoint-level authorization in tests is driven by TestAuthHandler's role header, independent
/// of this seed — but User.Role itself is a real, persisted column (embedded in the JWT at login,
/// read directly by e.g. AdminRoleDriftDetectionJob), so it's set here to match each user's name
/// rather than left at the entity default.
/// </summary>
public static class SeedHelper
{
    public static readonly Guid AdminId = OrderedGuid.FromLegacy("users", 1);
    public static readonly Guid StaffId = OrderedGuid.FromLegacy("users", 2);
    public static readonly Guid OwnerId = OrderedGuid.FromLegacy("users", 3);
    public static readonly Guid AudienceId = OrderedGuid.FromLegacy("users", 4);
    public static readonly Guid OtherOwnerId = OrderedGuid.FromLegacy("users", 5);
    public static readonly Guid OtherVenueStaffId = OrderedGuid.FromLegacy("users", 6);   // real Staff assignment at OtherLoungeId — for "wrong venue" tests

    // MLACP-308 (CF1). Mot phong tra khong chay duoc hai buoi dien chong gio nhau, va tu gio he
    // thong cham dieu do that. Gan nhu moi test tao buoi dien qua API deu dung chung venue
    // SeedHelper.LoungeId VA dung chung mot moc gio (UtcNow.AddDays(14)), nen khi mot buoi dien
    // trong so do chuyen sang Pending/Published thi no giu cho, va moi buoi dien tao sau do trong
    // cung lan chay deu bi tu choi.
    //
    // Do khong phai loi cua quy tac moi — do la du lieu test chua bao gio can den lich thuc te.
    // Bo cap khung gio nay tra ve mot day gio tang dan, buoc 5 tieng (dai hon do dai mac dinh 4
    // tieng cua mot buoi dien), nen hai khung gio bat ky lay tu day khong the giam len nhau.
    private static int _showSlot = -1;

    /// <summary>
    /// Mot khung gio dien con trong, khac moi khung gio da cap truoc do trong cung lan chay.
    /// Dung cho cac test chi can "mot buoi dien nao do o tuong lai"; test nao can dung mot moc gio
    /// cu the (vi du dem so ngay lam viec truoc buoi dien) thi tu truyen moc gio cua no.
    /// </summary>
    public static DateTimeOffset NextShowStart()
        => DateTimeOffset.UtcNow.AddDays(14)
            .AddHours(System.Threading.Interlocked.Increment(ref _showSlot) * 5);

    public static readonly Guid LoungeId = OrderedGuid.FromLegacy("music_lounges", 1);
    public static readonly Guid OtherLoungeId = OrderedGuid.FromLegacy("music_lounges", 2);       // a different venue than LoungeId, staffed by OtherVenueStaffId
    public static readonly Guid ShowId = OrderedGuid.FromLegacy("lounge_shows", 1);           // Livestream format, Ongoing
    public static readonly Guid CancelledShowId = OrderedGuid.FromLegacy("lounge_shows", 2);
    public static readonly Guid OfflineShowId = OrderedGuid.FromLegacy("lounge_shows", 3);

    public static readonly Guid PerformerId = OrderedGuid.FromLegacy("performers", 1);
    public static readonly Guid PerformanceId = OrderedGuid.FromLegacy("performances", 1);

    public static readonly Guid GenreId1 = OrderedGuid.FromLegacy("music_genres", 1);
    public static readonly Guid GenreId2 = OrderedGuid.FromLegacy("music_genres", 2);
    public static readonly Guid MoodId1 = OrderedGuid.FromLegacy("moods", 1);
    public static readonly Guid AtmosphereId1 = OrderedGuid.FromLegacy("venue_atmospheres", 1);

    public static readonly Guid TicketTierId = OrderedGuid.FromLegacy("ticket_tiers", 1);
    public static readonly Guid TicketPriceId = OrderedGuid.FromLegacy("ticket_prices", 1);
    public static readonly Guid AudienceTicketId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var piiEncryption = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();

        await db.Database.EnsureCreatedAsync();

        if (await db.Users.AnyAsync()) return; // already seeded

        // Users — Role set to match each user's name; see class-level remark on why this matters.
        db.Users.AddRange(
            new User { Id = AdminId,          Email = "admin@test.com",      FullName = "Admin",          Role = UserRole.Admin },
            new User { Id = StaffId,          Email = "staff@test.com",      FullName = "Staff",          Role = UserRole.Staff },
            // MLACP-395: giai ngan chi chuyen cho chu phong tra da duyet CCCD — hai chu phong tra mau la nguoi nhan da xac minh.
            new User { Id = OwnerId,          Email = "owner@test.com",      FullName = "Owner",          Role = UserRole.Owner, CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-30), CitizenCardReviewStatus = KycReviewStatus.Approved },
            new User { Id = AudienceId,       Email = "audience@test.com",   FullName = "Audience",       Role = UserRole.Audience },
            new User { Id = OtherOwnerId,     Email = "owner2@test.com",     FullName = "OtherOwner",     Role = UserRole.Owner, CitizenCardSubmittedAt = DateTimeOffset.UtcNow.AddDays(-30), CitizenCardReviewStatus = KycReviewStatus.Approved },
            new User { Id = OtherVenueStaffId, Email = "staff2@test.com",    FullName = "OtherVenueStaff", Role = UserRole.Staff }
        );

        // Venues
        // Status dat tay la Approved: mac dinh cua entity la Pending, va tu MLACP-307 thi Pending
        // khong con la mot venue dang hoat dong — no khong hien cong khai va khong nop duyet buoi
        // dien duoc. Hai venue nen tang nay dong vai "phong tra dang chay binh thuong" cho gan het
        // bo test, nen chung phai la Approved. Truoc MLACP-307 ca bo test chay tren venue chua
        // duyet ma khong ai nhan ra, vi Pending luc do khong chan gi ca.
        db.Lounges.AddRange(
            new MusicLoungeVenue
            {
                Id = LoungeId, OwnerId = OwnerId, Name = "Test Lounge",
                Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "123 Main", District = "1", City = "HCM" }
            },
            new MusicLoungeVenue
            {
                Id = OtherLoungeId, OwnerId = OtherOwnerId, Name = "Other Test Lounge",
                Status = LoungeStatus.Approved,
                Address = new VenueAddress { Street = "456 Other", District = "2", City = "HCM" }
            });

        // Staff assignments (ActiveUserBehavior re-checks these on every request — D6/bug #31:
        // a Staff JWT claiming a lounge_id with no matching active row here is rejected at the
        // pipeline). One user can only be active Staff at one venue at a time (see
        // LoungeStaffConfiguration's filtered unique index), so "wrong venue" tests need a
        // genuinely different Staff identity rather than reusing StaffId with a fake lounge_id.
        db.Add(new LoungeStaff
        {
            LoungeId = LoungeId, UserId = StaffId, AssignedBy = OwnerId,
            IsActive = true, AssignedAt = DateTimeOffset.UtcNow
        });
        db.Add(new LoungeStaff
        {
            LoungeId = OtherLoungeId, UserId = OtherVenueStaffId, AssignedBy = OtherOwnerId,
            IsActive = true, AssignedAt = DateTimeOffset.UtcNow
        });

        // Shows
        db.LoungeShows.AddRange(
            new LoungeShow
            {
                Id = ShowId, LoungeId = LoungeId, Name = "Live Night",
                Description = "Test show", Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Ongoing,
                // ScheduledEnd is in the FUTURE so this show is genuinely still running. It used to
                // be left null, which made the effective end ScheduledStart + 4h — i.e. 20 hours in
                // the past — so an "Ongoing" show that any realistic reading would call long over.
                // AutoEndStaleShowsJob correctly closes such a show, which then broke every other
                // test that needs this one Ongoing. Fixing the seed rather than the job: a show
                // marked Ongoing should be one that is actually on right now.
                ScheduledStart = DateTimeOffset.UtcNow.AddHours(-1),
                ScheduledEnd = DateTimeOffset.UtcNow.AddHours(3)
            },
            new LoungeShow
            {
                Id = CancelledShowId, LoungeId = LoungeId, Name = "Cancelled Show",
                Description = "Cancelled", Format = LoungeShowFormat.Online,
                Status = LoungeShowStatus.Cancelled,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1)
            },
            new LoungeShow
            {
                Id = OfflineShowId, LoungeId = LoungeId, Name = "Offline Night",
                Description = "Offline", Format = LoungeShowFormat.Offline,
                Status = LoungeShowStatus.Published,
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(1)
            }
        );

        // Performer + Performance
        db.Performers.Add(new Performer { Id = PerformerId, Name = "Test Artist" });
        db.Performances.Add(new Performance
        {
            Id = PerformanceId, LoungeShowId = ShowId,
            PerformerId = PerformerId, OrderIndex = 1
        });

        // Ticket tier for livestream access
        db.Add(new TicketTier
        {
            Id = TicketTierId, LoungeShowId = ShowId,
            Name = "Online", AccessType = AccessType.Livestream
        });

        // Ticket price
        db.Add(new TicketPrice
        {
            Id = TicketPriceId, TierId = TicketTierId, Name = "Standard", Price = 50_000m,
            PurchaseChannel = PurchaseChannel.Online,
            SaleStart = DateTimeOffset.UtcNow.AddDays(-1),
            SaleEnd = DateTimeOffset.UtcNow.AddDays(2)
        });

        // AudienceId already has a confirmed livestream ticket → HasViewerAccess = true
        db.Add(new Ticket
        {
            Id = AudienceTicketId,
            BuyerId = AudienceId, PriceId = TicketPriceId, TierId = TicketTierId,
            ShowId = ShowId, Status = TicketStatus.Confirmed,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // D14: Owner/OtherOwner can luon co goi subscription Active de tao event (CreateLoungeShow gate)
        db.SubscriptionPackages.Add(new SubscriptionPackage
        {
            Id = TestId.Of(1), Name = "Test Package", Price = 500_000m,
            BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 1000, HasAiPoster = true, MaxAiPostersPerMonth = 10, MaxTourScenes = 5, IsActive = true
        });
        db.OwnerSubscriptions.AddRange(
            new OwnerSubscription
            {
                Id = TestId.Of(1), OwnerId = OwnerId, PackageId = TestId.Of(1),
                StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
                Status = SubscriptionStatus.Active,
                MaxTicketsPerEventSnapshot = 1000, HasAiPosterSnapshot = true, MaxAiPostersPerMonthSnapshot = 10,
                MaxTourScenesSnapshot = 5
            },
            new OwnerSubscription
            {
                Id = TestId.Of(2), OwnerId = OtherOwnerId, PackageId = TestId.Of(1),
                StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
                Status = SubscriptionStatus.Active,
                MaxTicketsPerEventSnapshot = 1000, HasAiPosterSnapshot = true, MaxAiPostersPerMonthSnapshot = 10,
                MaxTourScenesSnapshot = 5
            });

        // Default payout bank accounts — 2026-08-09: ScheduleSettlementHandler/ConfirmDonationPaidCommandHandler
        // now fail closed (DomainException) if the venue/performer has no default BankAccount registered,
        // since Settlement.BankAccountId/Donation.BankAccountId used to be defined but never actually
        // assigned. Every test exercising the settlement/donation payout path needs one of these to exist.
        // AccountNumber must be real ciphertext (IPiiEncryptionService), not plaintext — GetBankAccountsQueryHandler
        // decrypts unconditionally on read, and a plaintext value here throws there (found by running
        // the suite, not by inspection — the failure only shows up as a 500 on the list endpoint).
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = LoungeId,
            BankName = "Test Bank", AccountNumber = piiEncryption.Encrypt("0000000001"), AccountHolder = "Test Lounge Owner",
            IsDefault = true, IsVerified = true
        });
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = OtherLoungeId,
            BankName = "Test Bank", AccountNumber = piiEncryption.Encrypt("0000000002"), AccountHolder = "Other Test Lounge Owner",
            IsDefault = true, IsVerified = true
        });
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Performer, OwnerId = PerformerId,
            BankName = "Test Bank", AccountNumber = piiEncryption.Encrypt("0000000003"), AccountHolder = "Test Artist",
            IsDefault = true, IsVerified = true
        });

        // Catalog data (Genre/Mood/Atmosphere) for CF2 preference tests is NOT inserted here — origin's
        // MusicGenreConfiguration/MoodConfiguration/VenueAtmosphereConfiguration now seed a fixed
        // default catalog via EF HasData (MLACP-14), applied automatically by EnsureCreatedAsync
        // above. GenreId1/GenreId2/MoodId1/AtmosphereId1 below point at those already-seeded rows —
        // inserting our own here would collide on the same Id (or the unique Name index for a
        // different one).

        await db.SaveChangesAsync();
    }
}
