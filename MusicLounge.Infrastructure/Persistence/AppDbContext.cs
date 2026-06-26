using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MusicLounge.Domain.Entities;
using MusicLoungeEntity = global::MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Infrastructure.Persistence
{

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<MusicGenre> MusicGenres { get; set; } = null!;
        public DbSet<MusicLoungeEntity> MusicLounges { get; set; } = null!;
        public DbSet<LoungeImage> LoungeImages { get; set; } = null!;
        public DbSet<LoungeStaff> LoungeStaffs { get; set; } = null!;
        public DbSet<SeatingArea> SeatingAreas { get; set; } = null!;
        public DbSet<SubscriptionPackage> SubscriptionPackages { get; set; } = null!;
        public DbSet<OwnerSubscription> OwnerSubscriptions { get; set; } = null!;
        public DbSet<Artist> Artists { get; set; } = null!;
        public DbSet<ArtistGenre> ArtistGenres { get; set; } = null!;
        public DbSet<Event> Events { get; set; } = null!;
        public DbSet<EventGenre> EventGenres { get; set; } = null!;
        public DbSet<EventArtist> EventArtists { get; set; } = null!;
        public DbSet<EventAreaTicket> EventAreaTickets { get; set; } = null!;
        public DbSet<Ticket> Tickets { get; set; } = null!;
        public DbSet<TicketHold> TicketHolds { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Livestream> Livestreams { get; set; } = null!;
        public DbSet<Donation> Donations { get; set; } = null!;
        public DbSet<FnbMenuItem> FnbMenuItems { get; set; } = null!;
        public DbSet<FnbOrder> FnbOrders { get; set; } = null!;
        public DbSet<FnbOrderItem> FnbOrderItems { get; set; } = null!;
        public DbSet<Follow> Follows { get; set; } = null!;
        public DbSet<EventWishlist> EventWishlists { get; set; } = null!;
        public DbSet<EventRating> EventRatings { get; set; } = null!;
        public DbSet<UserBehaviourLog> UserBehaviourLogs { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<Mood> Moods { get; set; } = null!;
        public DbSet<VenueAtmosphere> VenueAtmospheres { get; set; } = null!;
        public DbSet<UserFavoriteGenre> UserFavoriteGenres { get; set; } = null!;
        public DbSet<UserFavoriteMood> UserFavoriteMoods { get; set; } = null!;
        public DbSet<UserFavoriteAtmosphere> UserFavoriteAtmospheres { get; set; } = null!;
        public DbSet<EventMood> EventMoods { get; set; } = null!;
        public DbSet<EventAtmosphere> EventAtmospheres { get; set; } = null!;
        public DbSet<EventModeration> EventModerations { get; set; } = null!;
        public DbSet<AiRecommendation> AiRecommendations { get; set; } = null!;
        public DbSet<AiGeneratedPoster> AiGeneratedPosters { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureTables(modelBuilder);
            ConfigureKeys(modelBuilder);
            ConfigureIndexes(modelBuilder);
            ConfigurePrecisions(modelBuilder);
            ConfigureRelationships(modelBuilder);
            ApplySnakeCaseNames(modelBuilder);

            base.OnModelCreating(modelBuilder);
        }

        private static void ConfigureTables(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<MusicGenre>().ToTable("music_genres");
            modelBuilder.Entity<MusicLoungeEntity>().ToTable("music_lounges");
            modelBuilder.Entity<LoungeImage>().ToTable("lounge_images");
            modelBuilder.Entity<LoungeStaff>().ToTable("lounge_staff");
            modelBuilder.Entity<SeatingArea>().ToTable("seating_areas");
            modelBuilder.Entity<SubscriptionPackage>().ToTable("subscription_packages");
            modelBuilder.Entity<OwnerSubscription>().ToTable("owner_subscriptions");
            modelBuilder.Entity<Artist>().ToTable("artists");
            modelBuilder.Entity<ArtistGenre>().ToTable("artist_genres");
            modelBuilder.Entity<Event>().ToTable("events");
            modelBuilder.Entity<EventGenre>().ToTable("event_genres");
            modelBuilder.Entity<EventArtist>().ToTable("event_artists");
            modelBuilder.Entity<EventAreaTicket>().ToTable("event_area_tickets");
            modelBuilder.Entity<Ticket>().ToTable("tickets");
            modelBuilder.Entity<TicketHold>().ToTable("ticket_holds");
            modelBuilder.Entity<Payment>().ToTable("payments");
            modelBuilder.Entity<Livestream>().ToTable("livestreams");
            modelBuilder.Entity<Donation>().ToTable("donations");
            modelBuilder.Entity<FnbMenuItem>().ToTable("fnb_menu_items");
            modelBuilder.Entity<FnbOrder>().ToTable("fnb_orders");
            modelBuilder.Entity<FnbOrderItem>().ToTable("fnb_order_items");
            modelBuilder.Entity<Follow>().ToTable("follows");
            modelBuilder.Entity<EventWishlist>().ToTable("event_wishlists");
            modelBuilder.Entity<EventRating>().ToTable("event_ratings");
            modelBuilder.Entity<UserBehaviourLog>().ToTable("user_behaviour_log");
            modelBuilder.Entity<Notification>().ToTable("notifications");
            modelBuilder.Entity<Mood>().ToTable("moods");
            modelBuilder.Entity<VenueAtmosphere>().ToTable("venue_atmospheres");
            modelBuilder.Entity<UserFavoriteGenre>().ToTable("user_favorite_genres");
            modelBuilder.Entity<UserFavoriteMood>().ToTable("user_favorite_moods");
            modelBuilder.Entity<UserFavoriteAtmosphere>().ToTable("user_favorite_atmospheres");
            modelBuilder.Entity<EventMood>().ToTable("event_moods");
            modelBuilder.Entity<EventAtmosphere>().ToTable("event_atmospheres");
            modelBuilder.Entity<EventModeration>().ToTable("event_moderations");
            modelBuilder.Entity<AiRecommendation>().ToTable("ai_recommendations");
            modelBuilder.Entity<AiGeneratedPoster>().ToTable("ai_generated_posters");
        }

        private static void ConfigureKeys(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ArtistGenre>().HasKey(x => new { x.ArtistId, x.GenreId });
            modelBuilder.Entity<EventGenre>().HasKey(x => new { x.EventId, x.GenreId });
            modelBuilder.Entity<EventArtist>().HasKey(x => new { x.EventId, x.ArtistId });
            modelBuilder.Entity<Follow>().HasKey(x => new { x.UserId, x.ArtistId });
            modelBuilder.Entity<UserFavoriteGenre>().HasKey(x => new { x.UserId, x.GenreId });
            modelBuilder.Entity<UserFavoriteMood>().HasKey(x => new { x.UserId, x.MoodId });
            modelBuilder.Entity<UserFavoriteAtmosphere>().HasKey(x => new { x.UserId, x.AtmosphereId });
            modelBuilder.Entity<EventMood>().HasKey(x => new { x.EventId, x.MoodId });
            modelBuilder.Entity<EventAtmosphere>().HasKey(x => new { x.EventId, x.AtmosphereId });
        }

        private static void ConfigureIndexes(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
            modelBuilder.Entity<User>().HasIndex(x => x.CitizenCardNumber).IsUnique(false);
            modelBuilder.Entity<MusicGenre>().HasIndex(x => x.Name).IsUnique();
            modelBuilder.Entity<Ticket>().HasIndex(x => x.QrCode).IsUnique();
            modelBuilder.Entity<Livestream>().HasIndex(x => x.EventId).IsUnique();
            modelBuilder.Entity<EventWishlist>().HasIndex(x => new { x.UserId, x.EventId }).IsUnique();
            modelBuilder.Entity<EventRating>().HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
            modelBuilder.Entity<Mood>().HasIndex(x => x.Name).IsUnique();
            modelBuilder.Entity<VenueAtmosphere>().HasIndex(x => x.Name).IsUnique();
        }

        private static void ConfigurePrecisions(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SubscriptionPackage>().Property(x => x.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Event>().Property(x => x.RefundPercentage).HasPrecision(5, 2);
            modelBuilder.Entity<EventAreaTicket>().Property(x => x.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Donation>().Property(x => x.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<FnbMenuItem>().Property(x => x.Price).HasPrecision(18, 2);
            modelBuilder.Entity<FnbOrder>().Property(x => x.TotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<FnbOrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<EventModeration>().Property(x => x.AiScore).HasPrecision(8, 4);
            modelBuilder.Entity<AiRecommendation>().Property(x => x.RecommendationScore).HasPrecision(8, 4);
        }

        private static void ConfigureRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MusicLoungeEntity>().HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<MusicLoungeEntity>().HasOne<VenueAtmosphere>().WithMany().HasForeignKey(x => x.AtmosphereId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LoungeImage>().HasOne<MusicLoungeEntity>().WithMany().HasForeignKey(x => x.LoungeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LoungeStaff>().HasOne<MusicLoungeEntity>().WithMany().HasForeignKey(x => x.LoungeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<LoungeStaff>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<SeatingArea>().HasOne<MusicLoungeEntity>().WithMany().HasForeignKey(x => x.LoungeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<OwnerSubscription>().HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<OwnerSubscription>().HasOne<SubscriptionPackage>().WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Artist>().HasOne<MusicLoungeEntity>().WithMany().HasForeignKey(x => x.LoungeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ArtistGenre>().HasOne<Artist>().WithMany().HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ArtistGenre>().HasOne<MusicGenre>().WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Event>().HasOne<MusicLoungeEntity>().WithMany().HasForeignKey(x => x.LoungeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventGenre>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventGenre>().HasOne<MusicGenre>().WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventArtist>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventArtist>().HasOne<Artist>().WithMany().HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventAreaTicket>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventAreaTicket>().HasOne<SeatingArea>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Ticket>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Ticket>().HasOne<User>().WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Ticket>().HasOne<SeatingArea>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TicketHold>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TicketHold>().HasOne<EventAreaTicket>().WithMany().HasForeignKey(x => x.EventAreaTicketId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Payment>().HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Payment>().HasOne<OwnerSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Payment>().HasOne<User>().WithMany().HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Livestream>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Donation>().HasOne<Livestream>().WithMany().HasForeignKey(x => x.LivestreamId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Donation>().HasOne<User>().WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Donation>().HasOne<Artist>().WithMany().HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Donation>().HasOne<Payment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FnbMenuItem>().HasOne<MusicLoungeEntity>().WithMany().HasForeignKey(x => x.LoungeId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FnbOrder>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FnbOrder>().HasOne<User>().WithMany().HasForeignKey(x => x.AudienceId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FnbOrder>().HasOne<SeatingArea>().WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FnbOrderItem>().HasOne<FnbOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<FnbOrderItem>().HasOne<FnbMenuItem>().WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Follow>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Follow>().HasOne<Artist>().WithMany().HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventWishlist>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventWishlist>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventRating>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventRating>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserBehaviourLog>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserBehaviourLog>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<Notification>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserFavoriteGenre>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserFavoriteGenre>().HasOne<MusicGenre>().WithMany().HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserFavoriteMood>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserFavoriteMood>().HasOne<Mood>().WithMany().HasForeignKey(x => x.MoodId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserFavoriteAtmosphere>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserFavoriteAtmosphere>().HasOne<VenueAtmosphere>().WithMany().HasForeignKey(x => x.AtmosphereId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventMood>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventMood>().HasOne<Mood>().WithMany().HasForeignKey(x => x.MoodId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventAtmosphere>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventAtmosphere>().HasOne<VenueAtmosphere>().WithMany().HasForeignKey(x => x.AtmosphereId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventModeration>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<EventModeration>().HasOne<User>().WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AiRecommendation>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AiRecommendation>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AiGeneratedPoster>().HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<AiGeneratedPoster>().HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        }

        private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }

                foreach (var key in entity.GetKeys())
                {
                    key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
                }

                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
                }

                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
                }
            }
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < value.Length; i++)
            {
                var current = value[i];
                if (char.IsUpper(current))
                {
                    if (i > 0)
                    {
                        builder.Append('_');
                    }
                    builder.Append(char.ToLowerInvariant(current));
                }
                else
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }
    }
}



