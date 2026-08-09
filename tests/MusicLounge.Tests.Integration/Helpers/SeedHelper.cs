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
    public const int AdminId = 1;
    public const int StaffId = 2;
    public const int OwnerId = 3;
    public const int AudienceId = 4;
    public const int OtherOwnerId = 5;
    public const int OtherVenueStaffId = 6;   // real Staff assignment at OtherLoungeId — for "wrong venue" tests

    public const int LoungeId = 1;
    public const int OtherLoungeId = 2;       // a different venue than LoungeId, staffed by OtherVenueStaffId
    public const int ShowId = 1;           // Livestream format, Ongoing
    public const int CancelledShowId = 2;
    public const int OfflineShowId = 3;

    public const int PerformerId = 1;
    public const int PerformanceId = 1;

    public const int GenreId1 = 1;
    public const int GenreId2 = 2;
    public const int MoodId1 = 1;
    public const int AtmosphereId1 = 1;

    public const int TicketTierId = 1;
    public const int TicketPriceId = 1;
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
            new User { Id = OwnerId,          Email = "owner@test.com",      FullName = "Owner",          Role = UserRole.Owner },
            new User { Id = AudienceId,       Email = "audience@test.com",   FullName = "Audience",       Role = UserRole.Audience },
            new User { Id = OtherOwnerId,     Email = "owner2@test.com",     FullName = "OtherOwner",     Role = UserRole.Owner },
            new User { Id = OtherVenueStaffId, Email = "staff2@test.com",    FullName = "OtherVenueStaff", Role = UserRole.Staff }
        );

        // Venues
        db.Lounges.AddRange(
            new MusicLoungeVenue
            {
                Id = LoungeId, OwnerId = OwnerId, Name = "Test Lounge",
                Address = new VenueAddress { Street = "123 Main", District = "1", City = "HCM" }
            },
            new MusicLoungeVenue
            {
                Id = OtherLoungeId, OwnerId = OtherOwnerId, Name = "Other Test Lounge",
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
                ScheduledStart = DateTimeOffset.UtcNow.AddDays(-1)
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
            Id = 1, Name = "Test Package", Price = 500_000m,
            BillingCycle = SubscriptionBillingCycle.Monthly,
            MaxTicketsPerEvent = 1000, HasAiPoster = true, MaxAiPostersPerMonth = 10, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.OwnerSubscriptions.AddRange(
            new OwnerSubscription
            {
                Id = 1, OwnerId = OwnerId, PackageId = 1,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
                Status = SubscriptionStatus.Active,
                MaxTicketsPerEventSnapshot = 1000, HasAiPosterSnapshot = true, MaxAiPostersPerMonthSnapshot = 10
            },
            new OwnerSubscription
            {
                Id = 2, OwnerId = OtherOwnerId, PackageId = 1,
                StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(29),
                Status = SubscriptionStatus.Active,
                MaxTicketsPerEventSnapshot = 1000, HasAiPosterSnapshot = true, MaxAiPostersPerMonthSnapshot = 10
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
            IsDefault = true, IsVerified = true, CreatedAt = DateTimeOffset.UtcNow
        });
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Lounge, OwnerId = OtherLoungeId,
            BankName = "Test Bank", AccountNumber = piiEncryption.Encrypt("0000000002"), AccountHolder = "Other Test Lounge Owner",
            IsDefault = true, IsVerified = true, CreatedAt = DateTimeOffset.UtcNow
        });
        db.Add(new BankAccount
        {
            OwnerType = BankAccountOwnerType.Performer, OwnerId = PerformerId,
            BankName = "Test Bank", AccountNumber = piiEncryption.Encrypt("0000000003"), AccountHolder = "Test Artist",
            IsDefault = true, IsVerified = true, CreatedAt = DateTimeOffset.UtcNow
        });

        // Catalog data for CF2 preference tests
        db.Genres.AddRange(
            new MusicGenre { Id = GenreId1, Name = "Jazz" },
            new MusicGenre { Id = GenreId2, Name = "Pop" }
        );
        db.Moods.Add(new Mood { Id = MoodId1, Name = "Relaxed" });
        db.Atmospheres.Add(new VenueAtmosphere { Id = AtmosphereId1, Name = "Intimate" });

        await db.SaveChangesAsync();
    }
}
