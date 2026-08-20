using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PP1_StandardsAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiRecommendations_LoungeShows_LoungeShowId",
                table: "AiRecommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_AiRecommendations_Users_UserId",
                table: "AiRecommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_BehaviourLogs_LoungeShows_LoungeShowId",
                table: "BehaviourLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BehaviourLogs_Users_UserId",
                table: "BehaviourLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_complaints_Users_AdminId",
                table: "complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_complaints_Users_ComplainantUserId",
                table: "complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_custom_criteria_Lounges_LoungeId",
                table: "custom_criteria");

            migrationBuilder.DropForeignKey(
                name: "FK_donations_Performances_PerformanceId",
                table: "donations");

            migrationBuilder.DropForeignKey(
                name: "FK_donations_Users_DonorUserId",
                table: "donations");

            migrationBuilder.DropForeignKey(
                name: "FK_event_custom_values_LoungeShows_ShowId",
                table: "event_custom_values");

            migrationBuilder.DropForeignKey(
                name: "FK_event_moderations_Users_AdminId",
                table: "event_moderations");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_menu_items_Lounges_LoungeId",
                table: "fnb_menu_items");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_LoungeShows_ShowId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_Lounges_LoungeId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_Users_AudienceUserId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_Users_StaffId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Lounges_LoungeId",
                table: "Follows");

            migrationBuilder.DropForeignKey(
                name: "FK_Follows_Users_UserId",
                table: "Follows");

            migrationBuilder.DropForeignKey(
                name: "FK_livestream_chat_messages_Users_UserId",
                table: "livestream_chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_livestreams_LoungeShows_LoungeShowId",
                table: "livestreams");

            migrationBuilder.DropForeignKey(
                name: "FK_livestreams_Users_TerminatedById",
                table: "livestreams");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_images_Lounges_LoungeId",
                table: "lounge_images");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_Lounges_LoungeId",
                table: "lounge_staff");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_Users_AssignedBy",
                table: "lounge_staff");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_Users_UserId",
                table: "lounge_staff");

            migrationBuilder.DropForeignKey(
                name: "FK_Lounges_Atmospheres_AtmosphereId",
                table: "Lounges");

            migrationBuilder.DropForeignKey(
                name: "FK_Lounges_Users_OwnerId",
                table: "Lounges");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShowAtmospheres_Atmospheres_AtmosphereId",
                table: "LoungeShowAtmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShowAtmospheres_LoungeShows_LoungeShowId",
                table: "LoungeShowAtmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShowGenres_Genres_GenreId",
                table: "LoungeShowGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShowGenres_LoungeShows_LoungeShowId",
                table: "LoungeShowGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShowMoods_LoungeShows_LoungeShowId",
                table: "LoungeShowMoods");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShowMoods_Moods_MoodId",
                table: "LoungeShowMoods");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShows_Lounges_LoungeId",
                table: "LoungeShows");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShows_event_categories_CategoryId",
                table: "LoungeShows");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_Users_UserId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_owner_subscriptions_Users_OwnerId",
                table: "owner_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_Users_PayerId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Performances_LoungeShows_LoungeShowId",
                table: "Performances");

            migrationBuilder.DropForeignKey(
                name: "FK_Performances_Performers_PerformerId",
                table: "Performances");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformerGenres_Genres_GenreId",
                table: "PerformerGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_PerformerGenres_Performers_PerformerId",
                table: "PerformerGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_Performers_Users_CreatedByUserId",
                table: "Performers");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_LoungeShows_LoungeShowId",
                table: "Ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_Users_ProcessedBy",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_Users_RequestedBy",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_seating_zones_Lounges_LoungeId",
                table: "seating_zones");

            migrationBuilder.DropForeignKey(
                name: "FK_system_config_Users_UpdatedBy",
                table: "system_config");

            migrationBuilder.DropForeignKey(
                name: "FK_system_config_history_Users_ChangedBy",
                table: "system_config_history");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_holds_TicketPrices_PriceId",
                table: "ticket_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_holds_Users_UserId",
                table: "ticket_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketPrices_TicketTiers_TierId",
                table: "TicketPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_LoungeShows_ShowId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_TicketPrices_PriceId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_TicketTiers_TierId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_Users_BuyerId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketTiers_LoungeShows_LoungeShowId",
                table: "TicketTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketTiers_seating_zones_ZoneId",
                table: "TicketTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_user_custom_preferences_Users_UserId",
                table: "user_custom_preferences");

            migrationBuilder.DropForeignKey(
                name: "FK_user_event_scores_LoungeShows_ShowId",
                table: "user_event_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_user_event_scores_Users_UserId",
                table: "user_event_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavouriteAtmospheres_Atmospheres_AtmosphereId",
                table: "UserFavouriteAtmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavouriteAtmospheres_Users_UserId",
                table: "UserFavouriteAtmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavouriteGenres_Genres_GenreId",
                table: "UserFavouriteGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavouriteGenres_Users_UserId",
                table: "UserFavouriteGenres");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavouriteMoods_Moods_MoodId",
                table: "UserFavouriteMoods");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavouriteMoods_Users_UserId",
                table: "UserFavouriteMoods");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_penalties_Lounges_LoungeId",
                table: "venue_penalties");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_penalties_Users_IssuedBy",
                table: "venue_penalties");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_penalties_Users_ReviewedBy",
                table: "venue_penalties");

            migrationBuilder.DropForeignKey(
                name: "FK_Wishlists_LoungeShows_LoungeShowId",
                table: "Wishlists");

            migrationBuilder.DropForeignKey(
                name: "FK_Wishlists_Users_UserId",
                table: "Wishlists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Performers",
                table: "Performers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Performances",
                table: "Performances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Moods",
                table: "Moods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Follows",
                table: "Follows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Wishlists",
                table: "Wishlists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserFavouriteMoods",
                table: "UserFavouriteMoods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserFavouriteGenres",
                table: "UserFavouriteGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserFavouriteAtmospheres",
                table: "UserFavouriteAtmospheres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TicketTiers",
                table: "TicketTiers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TicketPrices",
                table: "TicketPrices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ratings",
                table: "Ratings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PerformerGenres",
                table: "PerformerGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoungeShows",
                table: "LoungeShows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoungeShowMoods",
                table: "LoungeShowMoods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoungeShowGenres",
                table: "LoungeShowGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoungeShowAtmospheres",
                table: "LoungeShowAtmospheres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lounges",
                table: "Lounges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Genres",
                table: "Genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BehaviourLogs",
                table: "BehaviourLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Atmospheres",
                table: "Atmospheres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AiRecommendations",
                table: "AiRecommendations");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Performers",
                newName: "performers");

            migrationBuilder.RenameTable(
                name: "Performances",
                newName: "performances");

            migrationBuilder.RenameTable(
                name: "Moods",
                newName: "moods");

            migrationBuilder.RenameTable(
                name: "Follows",
                newName: "follows");

            migrationBuilder.RenameTable(
                name: "Wishlists",
                newName: "show_wishlists");

            migrationBuilder.RenameTable(
                name: "UserFavouriteMoods",
                newName: "user_favourite_moods");

            migrationBuilder.RenameTable(
                name: "UserFavouriteGenres",
                newName: "user_favourite_genres");

            migrationBuilder.RenameTable(
                name: "UserFavouriteAtmospheres",
                newName: "user_favourite_atmospheres");

            migrationBuilder.RenameTable(
                name: "TicketTiers",
                newName: "ticket_tiers");

            migrationBuilder.RenameTable(
                name: "TicketPrices",
                newName: "ticket_prices");

            migrationBuilder.RenameTable(
                name: "Ratings",
                newName: "lounge_show_ratings");

            migrationBuilder.RenameTable(
                name: "PerformerGenres",
                newName: "performer_genres");

            migrationBuilder.RenameTable(
                name: "LoungeShows",
                newName: "lounge_shows");

            migrationBuilder.RenameTable(
                name: "LoungeShowMoods",
                newName: "lounge_show_moods");

            migrationBuilder.RenameTable(
                name: "LoungeShowGenres",
                newName: "lounge_show_genres");

            migrationBuilder.RenameTable(
                name: "LoungeShowAtmospheres",
                newName: "lounge_show_atmospheres");

            migrationBuilder.RenameTable(
                name: "Lounges",
                newName: "music_lounges");

            migrationBuilder.RenameTable(
                name: "Genres",
                newName: "music_genres");

            migrationBuilder.RenameTable(
                name: "BehaviourLogs",
                newName: "user_behaviour_logs");

            migrationBuilder.RenameTable(
                name: "Atmospheres",
                newName: "venue_atmospheres");

            migrationBuilder.RenameTable(
                name: "AiRecommendations",
                newName: "ai_recommendations");

            migrationBuilder.RenameIndex(
                name: "IX_Users_GoogleId",
                table: "users",
                newName: "IX_users_GoogleId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "users",
                newName: "IX_users_Email");

            migrationBuilder.RenameIndex(
                name: "IX_Performers_CreatedByUserId",
                table: "performers",
                newName: "IX_performers_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Performances_PerformerId",
                table: "performances",
                newName: "IX_performances_PerformerId");

            migrationBuilder.RenameIndex(
                name: "IX_Performances_LoungeShowId_PerformerId",
                table: "performances",
                newName: "IX_performances_LoungeShowId_PerformerId");

            migrationBuilder.RenameIndex(
                name: "IX_Moods_Name",
                table: "moods",
                newName: "IX_moods_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Follows_UserId_LoungeId",
                table: "follows",
                newName: "IX_follows_UserId_LoungeId");

            migrationBuilder.RenameIndex(
                name: "IX_Follows_LoungeId",
                table: "follows",
                newName: "IX_follows_LoungeId");

            migrationBuilder.RenameIndex(
                name: "IX_Wishlists_UserId_LoungeShowId",
                table: "show_wishlists",
                newName: "IX_show_wishlists_UserId_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_Wishlists_LoungeShowId",
                table: "show_wishlists",
                newName: "IX_show_wishlists_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFavouriteMoods_UserId_MoodId",
                table: "user_favourite_moods",
                newName: "IX_user_favourite_moods_UserId_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFavouriteMoods_MoodId",
                table: "user_favourite_moods",
                newName: "IX_user_favourite_moods_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFavouriteGenres_UserId_GenreId",
                table: "user_favourite_genres",
                newName: "IX_user_favourite_genres_UserId_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFavouriteGenres_GenreId",
                table: "user_favourite_genres",
                newName: "IX_user_favourite_genres_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFavouriteAtmospheres_UserId_AtmosphereId",
                table: "user_favourite_atmospheres",
                newName: "IX_user_favourite_atmospheres_UserId_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_UserFavouriteAtmospheres_AtmosphereId",
                table: "user_favourite_atmospheres",
                newName: "IX_user_favourite_atmospheres_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketTiers_ZoneId",
                table: "ticket_tiers",
                newName: "IX_ticket_tiers_ZoneId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketTiers_LoungeShowId",
                table: "ticket_tiers",
                newName: "IX_ticket_tiers_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketPrices_TierId",
                table: "ticket_prices",
                newName: "IX_ticket_prices_TierId");

            migrationBuilder.RenameIndex(
                name: "IX_Ratings_UserId_LoungeShowId",
                table: "lounge_show_ratings",
                newName: "IX_lounge_show_ratings_UserId_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_Ratings_LoungeShowId",
                table: "lounge_show_ratings",
                newName: "IX_lounge_show_ratings_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_PerformerGenres_PerformerId_GenreId",
                table: "performer_genres",
                newName: "IX_performer_genres_PerformerId_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_PerformerGenres_GenreId",
                table: "performer_genres",
                newName: "IX_performer_genres_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShows_ScheduledStart",
                table: "lounge_shows",
                newName: "IX_lounge_shows_ScheduledStart");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShows_LoungeId_Status",
                table: "lounge_shows",
                newName: "IX_lounge_shows_LoungeId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShows_CategoryId",
                table: "lounge_shows",
                newName: "IX_lounge_shows_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShowMoods_MoodId",
                table: "lounge_show_moods",
                newName: "IX_lounge_show_moods_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShowMoods_LoungeShowId_MoodId",
                table: "lounge_show_moods",
                newName: "IX_lounge_show_moods_LoungeShowId_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShowGenres_LoungeShowId_GenreId",
                table: "lounge_show_genres",
                newName: "IX_lounge_show_genres_LoungeShowId_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShowGenres_GenreId",
                table: "lounge_show_genres",
                newName: "IX_lounge_show_genres_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShowAtmospheres_LoungeShowId_AtmosphereId",
                table: "lounge_show_atmospheres",
                newName: "IX_lounge_show_atmospheres_LoungeShowId_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_LoungeShowAtmospheres_AtmosphereId",
                table: "lounge_show_atmospheres",
                newName: "IX_lounge_show_atmospheres_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_Lounges_Status",
                table: "music_lounges",
                newName: "IX_music_lounges_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Lounges_OwnerId",
                table: "music_lounges",
                newName: "IX_music_lounges_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Lounges_AtmosphereId",
                table: "music_lounges",
                newName: "IX_music_lounges_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_Lounges_Address_City_Address_District",
                table: "music_lounges",
                newName: "IX_music_lounges_Address_City_Address_District");

            migrationBuilder.RenameIndex(
                name: "IX_Lounges_Address_City",
                table: "music_lounges",
                newName: "IX_music_lounges_Address_City");

            migrationBuilder.RenameIndex(
                name: "IX_Genres_Name",
                table: "music_genres",
                newName: "IX_music_genres_Name");

            migrationBuilder.RenameIndex(
                name: "IX_BehaviourLogs_UserId_LoungeShowId_Action",
                table: "user_behaviour_logs",
                newName: "IX_user_behaviour_logs_UserId_LoungeShowId_Action");

            migrationBuilder.RenameIndex(
                name: "IX_BehaviourLogs_LoungeShowId",
                table: "user_behaviour_logs",
                newName: "IX_user_behaviour_logs_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_BehaviourLogs_CreatedAt",
                table: "user_behaviour_logs",
                newName: "IX_user_behaviour_logs_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Atmospheres_Name",
                table: "venue_atmospheres",
                newName: "IX_venue_atmospheres_Name");

            migrationBuilder.RenameIndex(
                name: "IX_AiRecommendations_UserId_LoungeShowId",
                table: "ai_recommendations",
                newName: "IX_ai_recommendations_UserId_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_AiRecommendations_UserId_ExpiresAt",
                table: "ai_recommendations",
                newName: "IX_ai_recommendations_UserId_ExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_AiRecommendations_LoungeShowId",
                table: "ai_recommendations",
                newName: "IX_ai_recommendations_LoungeShowId");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentId",
                table: "ledger_entries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "ledger_entries",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ledger_entries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceId",
                table: "ledger_entries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "ledger_entries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Backfill: create ledger accounts from the legacy inline (AccountType, OwnerId) pairs,
            // link every existing entry to its account, and stamp payment references —
            // append-only ledger data must survive the refactor (D8).
            migrationBuilder.Sql(@"
INSERT INTO ledger_accounts (OwnerType, OwnerId)
SELECT DISTINCT le.AccountType, le.OwnerId
FROM ledger_entries le
WHERE NOT EXISTS (
    SELECT 1 FROM ledger_accounts a
    WHERE a.OwnerType = le.AccountType
      AND ((a.OwnerId = le.OwnerId) OR (a.OwnerId IS NULL AND le.OwnerId IS NULL)));

UPDATE le SET le.AccountId = a.Id
FROM ledger_entries le
JOIN ledger_accounts a
  ON a.OwnerType = le.AccountType
 AND ((a.OwnerId = le.OwnerId) OR (a.OwnerId IS NULL AND le.OwnerId IS NULL));

UPDATE ledger_entries
SET ReferenceType = 'payment', ReferenceId = CAST(PaymentId AS NVARCHAR(255))
WHERE ReferenceType = '' AND PaymentId IS NOT NULL;
");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ledger_entries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_performers",
                table: "performers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_performances",
                table: "performances",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_moods",
                table: "moods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_follows",
                table: "follows",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_show_wishlists",
                table: "show_wishlists",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_favourite_moods",
                table: "user_favourite_moods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_favourite_genres",
                table: "user_favourite_genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_favourite_atmospheres",
                table: "user_favourite_atmospheres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ticket_tiers",
                table: "ticket_tiers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ticket_prices",
                table: "ticket_prices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_lounge_show_ratings",
                table: "lounge_show_ratings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_performer_genres",
                table: "performer_genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_lounge_shows",
                table: "lounge_shows",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_lounge_show_moods",
                table: "lounge_show_moods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_lounge_show_genres",
                table: "lounge_show_genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_lounge_show_atmospheres",
                table: "lounge_show_atmospheres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_music_lounges",
                table: "music_lounges",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_music_genres",
                table: "music_genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_behaviour_logs",
                table: "user_behaviour_logs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_venue_atmospheres",
                table: "venue_atmospheres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ai_recommendations",
                table: "ai_recommendations",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_ReferenceType_ReferenceId",
                table: "ledger_entries",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ai_recommendations_lounge_shows_LoungeShowId",
                table: "ai_recommendations",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ai_recommendations_users_UserId",
                table: "ai_recommendations",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_complaints_users_AdminId",
                table: "complaints",
                column: "AdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_complaints_users_ComplainantUserId",
                table: "complaints",
                column: "ComplainantUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_custom_criteria_music_lounges_LoungeId",
                table: "custom_criteria",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_donations_performances_PerformanceId",
                table: "donations",
                column: "PerformanceId",
                principalTable: "performances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_donations_users_DonorUserId",
                table: "donations",
                column: "DonorUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_event_custom_values_lounge_shows_ShowId",
                table: "event_custom_values",
                column: "ShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_event_moderations_users_AdminId",
                table: "event_moderations",
                column: "AdminId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_menu_items_music_lounges_LoungeId",
                table: "fnb_menu_items",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_lounge_shows_ShowId",
                table: "fnb_orders",
                column: "ShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_music_lounges_LoungeId",
                table: "fnb_orders",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_users_AudienceUserId",
                table: "fnb_orders",
                column: "AudienceUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_users_StaffId",
                table: "fnb_orders",
                column: "StaffId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_follows_music_lounges_LoungeId",
                table: "follows",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_follows_users_UserId",
                table: "follows",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_ledger_accounts_AccountId",
                table: "ledger_entries",
                column: "AccountId",
                principalTable: "ledger_accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_livestream_chat_messages_users_UserId",
                table: "livestream_chat_messages",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_livestreams_lounge_shows_LoungeShowId",
                table: "livestreams",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_livestreams_users_TerminatedById",
                table: "livestreams",
                column: "TerminatedById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_images_music_lounges_LoungeId",
                table: "lounge_images",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_atmospheres_lounge_shows_LoungeShowId",
                table: "lounge_show_atmospheres",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_atmospheres_venue_atmospheres_AtmosphereId",
                table: "lounge_show_atmospheres",
                column: "AtmosphereId",
                principalTable: "venue_atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_genres_lounge_shows_LoungeShowId",
                table: "lounge_show_genres",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_genres_music_genres_GenreId",
                table: "lounge_show_genres",
                column: "GenreId",
                principalTable: "music_genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_moods_lounge_shows_LoungeShowId",
                table: "lounge_show_moods",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_moods_moods_MoodId",
                table: "lounge_show_moods",
                column: "MoodId",
                principalTable: "moods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_ratings_lounge_shows_LoungeShowId",
                table: "lounge_show_ratings",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_show_ratings_users_UserId",
                table: "lounge_show_ratings",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_shows_event_categories_CategoryId",
                table: "lounge_shows",
                column: "CategoryId",
                principalTable: "event_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_shows_music_lounges_LoungeId",
                table: "lounge_shows",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_music_lounges_LoungeId",
                table: "lounge_staff",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_users_AssignedBy",
                table: "lounge_staff",
                column: "AssignedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_users_UserId",
                table: "lounge_staff",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_music_lounges_users_OwnerId",
                table: "music_lounges",
                column: "OwnerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_music_lounges_venue_atmospheres_AtmosphereId",
                table: "music_lounges",
                column: "AtmosphereId",
                principalTable: "venue_atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_owner_subscriptions_users_OwnerId",
                table: "owner_subscriptions",
                column: "OwnerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_PayerId",
                table: "payments",
                column: "PayerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_performances_lounge_shows_LoungeShowId",
                table: "performances",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_performances_performers_PerformerId",
                table: "performances",
                column: "PerformerId",
                principalTable: "performers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_performer_genres_music_genres_GenreId",
                table: "performer_genres",
                column: "GenreId",
                principalTable: "music_genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_performer_genres_performers_PerformerId",
                table: "performer_genres",
                column: "PerformerId",
                principalTable: "performers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_performers_users_CreatedByUserId",
                table: "performers",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_users_ProcessedBy",
                table: "refund_requests",
                column: "ProcessedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_users_RequestedBy",
                table: "refund_requests",
                column: "RequestedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_seating_zones_music_lounges_LoungeId",
                table: "seating_zones",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_show_wishlists_lounge_shows_LoungeShowId",
                table: "show_wishlists",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_show_wishlists_users_UserId",
                table: "show_wishlists",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_system_config_users_UpdatedBy",
                table: "system_config",
                column: "UpdatedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_system_config_history_users_ChangedBy",
                table: "system_config_history",
                column: "ChangedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_holds_ticket_prices_PriceId",
                table: "ticket_holds",
                column: "PriceId",
                principalTable: "ticket_prices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_holds_users_UserId",
                table: "ticket_holds",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_prices_ticket_tiers_TierId",
                table: "ticket_prices",
                column: "TierId",
                principalTable: "ticket_tiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_tiers_lounge_shows_LoungeShowId",
                table: "ticket_tiers",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_tiers_seating_zones_ZoneId",
                table: "ticket_tiers",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_lounge_shows_ShowId",
                table: "tickets",
                column: "ShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_ticket_prices_PriceId",
                table: "tickets",
                column: "PriceId",
                principalTable: "ticket_prices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_ticket_tiers_TierId",
                table: "tickets",
                column: "TierId",
                principalTable: "ticket_tiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_users_BuyerId",
                table: "tickets",
                column: "BuyerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_behaviour_logs_lounge_shows_LoungeShowId",
                table: "user_behaviour_logs",
                column: "LoungeShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_behaviour_logs_users_UserId",
                table: "user_behaviour_logs",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_custom_preferences_users_UserId",
                table: "user_custom_preferences",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_event_scores_lounge_shows_ShowId",
                table: "user_event_scores",
                column: "ShowId",
                principalTable: "lounge_shows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_event_scores_users_UserId",
                table: "user_event_scores",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_atmospheres_users_UserId",
                table: "user_favourite_atmospheres",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_atmospheres_venue_atmospheres_AtmosphereId",
                table: "user_favourite_atmospheres",
                column: "AtmosphereId",
                principalTable: "venue_atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_genres_music_genres_GenreId",
                table: "user_favourite_genres",
                column: "GenreId",
                principalTable: "music_genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_genres_users_UserId",
                table: "user_favourite_genres",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_moods_moods_MoodId",
                table: "user_favourite_moods",
                column: "MoodId",
                principalTable: "moods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_moods_users_UserId",
                table: "user_favourite_moods",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_penalties_music_lounges_LoungeId",
                table: "venue_penalties",
                column: "LoungeId",
                principalTable: "music_lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_penalties_users_IssuedBy",
                table: "venue_penalties",
                column: "IssuedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_penalties_users_ReviewedBy",
                table: "venue_penalties",
                column: "ReviewedBy",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_recommendations_lounge_shows_LoungeShowId",
                table: "ai_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_ai_recommendations_users_UserId",
                table: "ai_recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_complaints_users_AdminId",
                table: "complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_complaints_users_ComplainantUserId",
                table: "complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_custom_criteria_music_lounges_LoungeId",
                table: "custom_criteria");

            migrationBuilder.DropForeignKey(
                name: "FK_donations_performances_PerformanceId",
                table: "donations");

            migrationBuilder.DropForeignKey(
                name: "FK_donations_users_DonorUserId",
                table: "donations");

            migrationBuilder.DropForeignKey(
                name: "FK_event_custom_values_lounge_shows_ShowId",
                table: "event_custom_values");

            migrationBuilder.DropForeignKey(
                name: "FK_event_moderations_users_AdminId",
                table: "event_moderations");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_menu_items_music_lounges_LoungeId",
                table: "fnb_menu_items");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_lounge_shows_ShowId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_music_lounges_LoungeId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_users_AudienceUserId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_fnb_orders_users_StaffId",
                table: "fnb_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_follows_music_lounges_LoungeId",
                table: "follows");

            migrationBuilder.DropForeignKey(
                name: "FK_follows_users_UserId",
                table: "follows");

            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_ledger_accounts_AccountId",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_livestream_chat_messages_users_UserId",
                table: "livestream_chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_livestreams_lounge_shows_LoungeShowId",
                table: "livestreams");

            migrationBuilder.DropForeignKey(
                name: "FK_livestreams_users_TerminatedById",
                table: "livestreams");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_images_music_lounges_LoungeId",
                table: "lounge_images");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_atmospheres_lounge_shows_LoungeShowId",
                table: "lounge_show_atmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_atmospheres_venue_atmospheres_AtmosphereId",
                table: "lounge_show_atmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_genres_lounge_shows_LoungeShowId",
                table: "lounge_show_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_genres_music_genres_GenreId",
                table: "lounge_show_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_moods_lounge_shows_LoungeShowId",
                table: "lounge_show_moods");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_moods_moods_MoodId",
                table: "lounge_show_moods");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_ratings_lounge_shows_LoungeShowId",
                table: "lounge_show_ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_show_ratings_users_UserId",
                table: "lounge_show_ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_shows_event_categories_CategoryId",
                table: "lounge_shows");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_shows_music_lounges_LoungeId",
                table: "lounge_shows");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_music_lounges_LoungeId",
                table: "lounge_staff");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_users_AssignedBy",
                table: "lounge_staff");

            migrationBuilder.DropForeignKey(
                name: "FK_lounge_staff_users_UserId",
                table: "lounge_staff");

            migrationBuilder.DropForeignKey(
                name: "FK_music_lounges_users_OwnerId",
                table: "music_lounges");

            migrationBuilder.DropForeignKey(
                name: "FK_music_lounges_venue_atmospheres_AtmosphereId",
                table: "music_lounges");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_owner_subscriptions_users_OwnerId",
                table: "owner_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_PayerId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_performances_lounge_shows_LoungeShowId",
                table: "performances");

            migrationBuilder.DropForeignKey(
                name: "FK_performances_performers_PerformerId",
                table: "performances");

            migrationBuilder.DropForeignKey(
                name: "FK_performer_genres_music_genres_GenreId",
                table: "performer_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_performer_genres_performers_PerformerId",
                table: "performer_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_performers_users_CreatedByUserId",
                table: "performers");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_users_ProcessedBy",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_refund_requests_users_RequestedBy",
                table: "refund_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_seating_zones_music_lounges_LoungeId",
                table: "seating_zones");

            migrationBuilder.DropForeignKey(
                name: "FK_show_wishlists_lounge_shows_LoungeShowId",
                table: "show_wishlists");

            migrationBuilder.DropForeignKey(
                name: "FK_show_wishlists_users_UserId",
                table: "show_wishlists");

            migrationBuilder.DropForeignKey(
                name: "FK_system_config_users_UpdatedBy",
                table: "system_config");

            migrationBuilder.DropForeignKey(
                name: "FK_system_config_history_users_ChangedBy",
                table: "system_config_history");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_holds_ticket_prices_PriceId",
                table: "ticket_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_holds_users_UserId",
                table: "ticket_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_prices_ticket_tiers_TierId",
                table: "ticket_prices");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_tiers_lounge_shows_LoungeShowId",
                table: "ticket_tiers");

            migrationBuilder.DropForeignKey(
                name: "FK_ticket_tiers_seating_zones_ZoneId",
                table: "ticket_tiers");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_lounge_shows_ShowId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_ticket_prices_PriceId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_ticket_tiers_TierId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_tickets_users_BuyerId",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_user_behaviour_logs_lounge_shows_LoungeShowId",
                table: "user_behaviour_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_user_behaviour_logs_users_UserId",
                table: "user_behaviour_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_user_custom_preferences_users_UserId",
                table: "user_custom_preferences");

            migrationBuilder.DropForeignKey(
                name: "FK_user_event_scores_lounge_shows_ShowId",
                table: "user_event_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_user_event_scores_users_UserId",
                table: "user_event_scores");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_atmospheres_users_UserId",
                table: "user_favourite_atmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_atmospheres_venue_atmospheres_AtmosphereId",
                table: "user_favourite_atmospheres");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_genres_music_genres_GenreId",
                table: "user_favourite_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_genres_users_UserId",
                table: "user_favourite_genres");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_moods_moods_MoodId",
                table: "user_favourite_moods");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_moods_users_UserId",
                table: "user_favourite_moods");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_penalties_music_lounges_LoungeId",
                table: "venue_penalties");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_penalties_users_IssuedBy",
                table: "venue_penalties");

            migrationBuilder.DropForeignKey(
                name: "FK_venue_penalties_users_ReviewedBy",
                table: "venue_penalties");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_performers",
                table: "performers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_performances",
                table: "performances");

            migrationBuilder.DropPrimaryKey(
                name: "PK_moods",
                table: "moods");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_ReferenceType_ReferenceId",
                table: "ledger_entries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_follows",
                table: "follows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_venue_atmospheres",
                table: "venue_atmospheres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_favourite_moods",
                table: "user_favourite_moods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_favourite_genres",
                table: "user_favourite_genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_favourite_atmospheres",
                table: "user_favourite_atmospheres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_behaviour_logs",
                table: "user_behaviour_logs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ticket_tiers",
                table: "ticket_tiers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ticket_prices",
                table: "ticket_prices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_show_wishlists",
                table: "show_wishlists");

            migrationBuilder.DropPrimaryKey(
                name: "PK_performer_genres",
                table: "performer_genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_music_lounges",
                table: "music_lounges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_music_genres",
                table: "music_genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lounge_shows",
                table: "lounge_shows");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lounge_show_ratings",
                table: "lounge_show_ratings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lounge_show_moods",
                table: "lounge_show_moods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lounge_show_genres",
                table: "lounge_show_genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lounge_show_atmospheres",
                table: "lounge_show_atmospheres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ai_recommendations",
                table: "ai_recommendations");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "ledger_entries");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "performers",
                newName: "Performers");

            migrationBuilder.RenameTable(
                name: "performances",
                newName: "Performances");

            migrationBuilder.RenameTable(
                name: "moods",
                newName: "Moods");

            migrationBuilder.RenameTable(
                name: "follows",
                newName: "Follows");

            migrationBuilder.RenameTable(
                name: "venue_atmospheres",
                newName: "Atmospheres");

            migrationBuilder.RenameTable(
                name: "user_favourite_moods",
                newName: "UserFavouriteMoods");

            migrationBuilder.RenameTable(
                name: "user_favourite_genres",
                newName: "UserFavouriteGenres");

            migrationBuilder.RenameTable(
                name: "user_favourite_atmospheres",
                newName: "UserFavouriteAtmospheres");

            migrationBuilder.RenameTable(
                name: "user_behaviour_logs",
                newName: "BehaviourLogs");

            migrationBuilder.RenameTable(
                name: "ticket_tiers",
                newName: "TicketTiers");

            migrationBuilder.RenameTable(
                name: "ticket_prices",
                newName: "TicketPrices");

            migrationBuilder.RenameTable(
                name: "show_wishlists",
                newName: "Wishlists");

            migrationBuilder.RenameTable(
                name: "performer_genres",
                newName: "PerformerGenres");

            migrationBuilder.RenameTable(
                name: "music_lounges",
                newName: "Lounges");

            migrationBuilder.RenameTable(
                name: "music_genres",
                newName: "Genres");

            migrationBuilder.RenameTable(
                name: "lounge_shows",
                newName: "LoungeShows");

            migrationBuilder.RenameTable(
                name: "lounge_show_ratings",
                newName: "Ratings");

            migrationBuilder.RenameTable(
                name: "lounge_show_moods",
                newName: "LoungeShowMoods");

            migrationBuilder.RenameTable(
                name: "lounge_show_genres",
                newName: "LoungeShowGenres");

            migrationBuilder.RenameTable(
                name: "lounge_show_atmospheres",
                newName: "LoungeShowAtmospheres");

            migrationBuilder.RenameTable(
                name: "ai_recommendations",
                newName: "AiRecommendations");

            migrationBuilder.RenameIndex(
                name: "IX_users_GoogleId",
                table: "Users",
                newName: "IX_Users_GoogleId");

            migrationBuilder.RenameIndex(
                name: "IX_users_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameIndex(
                name: "IX_performers_CreatedByUserId",
                table: "Performers",
                newName: "IX_Performers_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_performances_PerformerId",
                table: "Performances",
                newName: "IX_Performances_PerformerId");

            migrationBuilder.RenameIndex(
                name: "IX_performances_LoungeShowId_PerformerId",
                table: "Performances",
                newName: "IX_Performances_LoungeShowId_PerformerId");

            migrationBuilder.RenameIndex(
                name: "IX_moods_Name",
                table: "Moods",
                newName: "IX_Moods_Name");

            migrationBuilder.RenameIndex(
                name: "IX_follows_UserId_LoungeId",
                table: "Follows",
                newName: "IX_Follows_UserId_LoungeId");

            migrationBuilder.RenameIndex(
                name: "IX_follows_LoungeId",
                table: "Follows",
                newName: "IX_Follows_LoungeId");

            migrationBuilder.RenameIndex(
                name: "IX_venue_atmospheres_Name",
                table: "Atmospheres",
                newName: "IX_Atmospheres_Name");

            migrationBuilder.RenameIndex(
                name: "IX_user_favourite_moods_UserId_MoodId",
                table: "UserFavouriteMoods",
                newName: "IX_UserFavouriteMoods_UserId_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_user_favourite_moods_MoodId",
                table: "UserFavouriteMoods",
                newName: "IX_UserFavouriteMoods_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_user_favourite_genres_UserId_GenreId",
                table: "UserFavouriteGenres",
                newName: "IX_UserFavouriteGenres_UserId_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_user_favourite_genres_GenreId",
                table: "UserFavouriteGenres",
                newName: "IX_UserFavouriteGenres_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_user_favourite_atmospheres_UserId_AtmosphereId",
                table: "UserFavouriteAtmospheres",
                newName: "IX_UserFavouriteAtmospheres_UserId_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_user_favourite_atmospheres_AtmosphereId",
                table: "UserFavouriteAtmospheres",
                newName: "IX_UserFavouriteAtmospheres_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_user_behaviour_logs_UserId_LoungeShowId_Action",
                table: "BehaviourLogs",
                newName: "IX_BehaviourLogs_UserId_LoungeShowId_Action");

            migrationBuilder.RenameIndex(
                name: "IX_user_behaviour_logs_LoungeShowId",
                table: "BehaviourLogs",
                newName: "IX_BehaviourLogs_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_user_behaviour_logs_CreatedAt",
                table: "BehaviourLogs",
                newName: "IX_BehaviourLogs_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_tiers_ZoneId",
                table: "TicketTiers",
                newName: "IX_TicketTiers_ZoneId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_tiers_LoungeShowId",
                table: "TicketTiers",
                newName: "IX_TicketTiers_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_ticket_prices_TierId",
                table: "TicketPrices",
                newName: "IX_TicketPrices_TierId");

            migrationBuilder.RenameIndex(
                name: "IX_show_wishlists_UserId_LoungeShowId",
                table: "Wishlists",
                newName: "IX_Wishlists_UserId_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_show_wishlists_LoungeShowId",
                table: "Wishlists",
                newName: "IX_Wishlists_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_performer_genres_PerformerId_GenreId",
                table: "PerformerGenres",
                newName: "IX_PerformerGenres_PerformerId_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_performer_genres_GenreId",
                table: "PerformerGenres",
                newName: "IX_PerformerGenres_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_music_lounges_Status",
                table: "Lounges",
                newName: "IX_Lounges_Status");

            migrationBuilder.RenameIndex(
                name: "IX_music_lounges_OwnerId",
                table: "Lounges",
                newName: "IX_Lounges_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_music_lounges_AtmosphereId",
                table: "Lounges",
                newName: "IX_Lounges_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_music_lounges_Address_City_Address_District",
                table: "Lounges",
                newName: "IX_Lounges_Address_City_Address_District");

            migrationBuilder.RenameIndex(
                name: "IX_music_lounges_Address_City",
                table: "Lounges",
                newName: "IX_Lounges_Address_City");

            migrationBuilder.RenameIndex(
                name: "IX_music_genres_Name",
                table: "Genres",
                newName: "IX_Genres_Name");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_shows_ScheduledStart",
                table: "LoungeShows",
                newName: "IX_LoungeShows_ScheduledStart");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_shows_LoungeId_Status",
                table: "LoungeShows",
                newName: "IX_LoungeShows_LoungeId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_shows_CategoryId",
                table: "LoungeShows",
                newName: "IX_LoungeShows_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_ratings_UserId_LoungeShowId",
                table: "Ratings",
                newName: "IX_Ratings_UserId_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_ratings_LoungeShowId",
                table: "Ratings",
                newName: "IX_Ratings_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_moods_MoodId",
                table: "LoungeShowMoods",
                newName: "IX_LoungeShowMoods_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_moods_LoungeShowId_MoodId",
                table: "LoungeShowMoods",
                newName: "IX_LoungeShowMoods_LoungeShowId_MoodId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_genres_LoungeShowId_GenreId",
                table: "LoungeShowGenres",
                newName: "IX_LoungeShowGenres_LoungeShowId_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_genres_GenreId",
                table: "LoungeShowGenres",
                newName: "IX_LoungeShowGenres_GenreId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_atmospheres_LoungeShowId_AtmosphereId",
                table: "LoungeShowAtmospheres",
                newName: "IX_LoungeShowAtmospheres_LoungeShowId_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_lounge_show_atmospheres_AtmosphereId",
                table: "LoungeShowAtmospheres",
                newName: "IX_LoungeShowAtmospheres_AtmosphereId");

            migrationBuilder.RenameIndex(
                name: "IX_ai_recommendations_UserId_LoungeShowId",
                table: "AiRecommendations",
                newName: "IX_AiRecommendations_UserId_LoungeShowId");

            migrationBuilder.RenameIndex(
                name: "IX_ai_recommendations_UserId_ExpiresAt",
                table: "AiRecommendations",
                newName: "IX_AiRecommendations_UserId_ExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_ai_recommendations_LoungeShowId",
                table: "AiRecommendations",
                newName: "IX_AiRecommendations_LoungeShowId");

            migrationBuilder.AlterColumn<int>(
                name: "PaymentId",
                table: "ledger_entries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "ledger_entries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "ledger_entries",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Performers",
                table: "Performers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Performances",
                table: "Performances",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Moods",
                table: "Moods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Follows",
                table: "Follows",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Atmospheres",
                table: "Atmospheres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserFavouriteMoods",
                table: "UserFavouriteMoods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserFavouriteGenres",
                table: "UserFavouriteGenres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserFavouriteAtmospheres",
                table: "UserFavouriteAtmospheres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BehaviourLogs",
                table: "BehaviourLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TicketTiers",
                table: "TicketTiers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TicketPrices",
                table: "TicketPrices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Wishlists",
                table: "Wishlists",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PerformerGenres",
                table: "PerformerGenres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lounges",
                table: "Lounges",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Genres",
                table: "Genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoungeShows",
                table: "LoungeShows",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ratings",
                table: "Ratings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoungeShowMoods",
                table: "LoungeShowMoods",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoungeShowGenres",
                table: "LoungeShowGenres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoungeShowAtmospheres",
                table: "LoungeShowAtmospheres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AiRecommendations",
                table: "AiRecommendations",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AiRecommendations_LoungeShows_LoungeShowId",
                table: "AiRecommendations",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AiRecommendations_Users_UserId",
                table: "AiRecommendations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BehaviourLogs_LoungeShows_LoungeShowId",
                table: "BehaviourLogs",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BehaviourLogs_Users_UserId",
                table: "BehaviourLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_complaints_Users_AdminId",
                table: "complaints",
                column: "AdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_complaints_Users_ComplainantUserId",
                table: "complaints",
                column: "ComplainantUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_custom_criteria_Lounges_LoungeId",
                table: "custom_criteria",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_donations_Performances_PerformanceId",
                table: "donations",
                column: "PerformanceId",
                principalTable: "Performances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_donations_Users_DonorUserId",
                table: "donations",
                column: "DonorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_event_custom_values_LoungeShows_ShowId",
                table: "event_custom_values",
                column: "ShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_event_moderations_Users_AdminId",
                table: "event_moderations",
                column: "AdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_menu_items_Lounges_LoungeId",
                table: "fnb_menu_items",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_LoungeShows_ShowId",
                table: "fnb_orders",
                column: "ShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_Lounges_LoungeId",
                table: "fnb_orders",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_Users_AudienceUserId",
                table: "fnb_orders",
                column: "AudienceUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_fnb_orders_Users_StaffId",
                table: "fnb_orders",
                column: "StaffId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Lounges_LoungeId",
                table: "Follows",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Users_UserId",
                table: "Follows",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_livestream_chat_messages_Users_UserId",
                table: "livestream_chat_messages",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_livestreams_LoungeShows_LoungeShowId",
                table: "livestreams",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_livestreams_Users_TerminatedById",
                table: "livestreams",
                column: "TerminatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_images_Lounges_LoungeId",
                table: "lounge_images",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_Lounges_LoungeId",
                table: "lounge_staff",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_Users_AssignedBy",
                table: "lounge_staff",
                column: "AssignedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_lounge_staff_Users_UserId",
                table: "lounge_staff",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lounges_Atmospheres_AtmosphereId",
                table: "Lounges",
                column: "AtmosphereId",
                principalTable: "Atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Lounges_Users_OwnerId",
                table: "Lounges",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShowAtmospheres_Atmospheres_AtmosphereId",
                table: "LoungeShowAtmospheres",
                column: "AtmosphereId",
                principalTable: "Atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShowAtmospheres_LoungeShows_LoungeShowId",
                table: "LoungeShowAtmospheres",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShowGenres_Genres_GenreId",
                table: "LoungeShowGenres",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShowGenres_LoungeShows_LoungeShowId",
                table: "LoungeShowGenres",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShowMoods_LoungeShows_LoungeShowId",
                table: "LoungeShowMoods",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShowMoods_Moods_MoodId",
                table: "LoungeShowMoods",
                column: "MoodId",
                principalTable: "Moods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShows_Lounges_LoungeId",
                table: "LoungeShows",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShows_event_categories_CategoryId",
                table: "LoungeShows",
                column: "CategoryId",
                principalTable: "event_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_Users_UserId",
                table: "notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_owner_subscriptions_Users_OwnerId",
                table: "owner_subscriptions",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_Users_PayerId",
                table: "payments",
                column: "PayerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Performances_LoungeShows_LoungeShowId",
                table: "Performances",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Performances_Performers_PerformerId",
                table: "Performances",
                column: "PerformerId",
                principalTable: "Performers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformerGenres_Genres_GenreId",
                table: "PerformerGenres",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformerGenres_Performers_PerformerId",
                table: "PerformerGenres",
                column: "PerformerId",
                principalTable: "Performers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Performers_Users_CreatedByUserId",
                table: "Performers",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_LoungeShows_LoungeShowId",
                table: "Ratings",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_Users_ProcessedBy",
                table: "refund_requests",
                column: "ProcessedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refund_requests_Users_RequestedBy",
                table: "refund_requests",
                column: "RequestedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_seating_zones_Lounges_LoungeId",
                table: "seating_zones",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_system_config_Users_UpdatedBy",
                table: "system_config",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_system_config_history_Users_ChangedBy",
                table: "system_config_history",
                column: "ChangedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_holds_TicketPrices_PriceId",
                table: "ticket_holds",
                column: "PriceId",
                principalTable: "TicketPrices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ticket_holds_Users_UserId",
                table: "ticket_holds",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketPrices_TicketTiers_TierId",
                table: "TicketPrices",
                column: "TierId",
                principalTable: "TicketTiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_LoungeShows_ShowId",
                table: "tickets",
                column: "ShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_TicketPrices_PriceId",
                table: "tickets",
                column: "PriceId",
                principalTable: "TicketPrices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_TicketTiers_TierId",
                table: "tickets",
                column: "TierId",
                principalTable: "TicketTiers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tickets_Users_BuyerId",
                table: "tickets",
                column: "BuyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTiers_LoungeShows_LoungeShowId",
                table: "TicketTiers",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTiers_seating_zones_ZoneId",
                table: "TicketTiers",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_custom_preferences_Users_UserId",
                table: "user_custom_preferences",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_event_scores_LoungeShows_ShowId",
                table: "user_event_scores",
                column: "ShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_event_scores_Users_UserId",
                table: "user_event_scores",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavouriteAtmospheres_Atmospheres_AtmosphereId",
                table: "UserFavouriteAtmospheres",
                column: "AtmosphereId",
                principalTable: "Atmospheres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavouriteAtmospheres_Users_UserId",
                table: "UserFavouriteAtmospheres",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavouriteGenres_Genres_GenreId",
                table: "UserFavouriteGenres",
                column: "GenreId",
                principalTable: "Genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavouriteGenres_Users_UserId",
                table: "UserFavouriteGenres",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavouriteMoods_Moods_MoodId",
                table: "UserFavouriteMoods",
                column: "MoodId",
                principalTable: "Moods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavouriteMoods_Users_UserId",
                table: "UserFavouriteMoods",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_penalties_Lounges_LoungeId",
                table: "venue_penalties",
                column: "LoungeId",
                principalTable: "Lounges",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_penalties_Users_IssuedBy",
                table: "venue_penalties",
                column: "IssuedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_venue_penalties_Users_ReviewedBy",
                table: "venue_penalties",
                column: "ReviewedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlists_LoungeShows_LoungeShowId",
                table: "Wishlists",
                column: "LoungeShowId",
                principalTable: "LoungeShows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlists_Users_UserId",
                table: "Wishlists",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
