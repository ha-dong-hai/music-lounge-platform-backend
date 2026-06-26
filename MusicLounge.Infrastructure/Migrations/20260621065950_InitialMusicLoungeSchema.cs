using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMusicLoungeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "moods",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_moods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "music_genres",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_music_genres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_packages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    duration_days = table.Column<int>(type: "int", nullable: false),
                    max_tickets_per_event = table.Column<int>(type: "int", nullable: false),
                    has_ai_poster = table.Column<bool>(type: "bit", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_subscription_packages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    avatar_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    auth_provider = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    google_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "venue_atmospheres",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_venue_atmospheres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    is_read = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_notifications", x => x.id);
                    table.ForeignKey(
                        name: "f_k_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "owner_subscriptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    package_id = table.Column<int>(type: "int", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    payment_ref = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_owner_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "f_k_owner_subscriptions_subscription_packages_package_id",
                        column: x => x.package_id,
                        principalTable: "subscription_packages",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_owner_subscriptions_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_favorite_genres",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    genre_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_user_favorite_genres", x => new { x.user_id, x.genre_id });
                    table.ForeignKey(
                        name: "f_k_user_favorite_genres_music_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "music_genres",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_user_favorite_genres_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_favorite_moods",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    mood_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_user_favorite_moods", x => new { x.user_id, x.mood_id });
                    table.ForeignKey(
                        name: "f_k_user_favorite_moods_moods_mood_id",
                        column: x => x.mood_id,
                        principalTable: "moods",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_user_favorite_moods_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "music_lounges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    atmosphere_id = table.Column<int>(type: "int", nullable: true),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    capacity_total = table.Column<int>(type: "int", nullable: false),
                    area_layout_image_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bank_account_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bank_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_escrow_required = table.Column<bool>(type: "bit", nullable: false),
                    reputation_score = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_music_lounges", x => x.id);
                    table.ForeignKey(
                        name: "f_k_music_lounges_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_music_lounges_venue_atmospheres_atmosphere_id",
                        column: x => x.atmosphere_id,
                        principalTable: "venue_atmospheres",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_favorite_atmospheres",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    atmosphere_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_user_favorite_atmospheres", x => new { x.user_id, x.atmosphere_id });
                    table.ForeignKey(
                        name: "f_k_user_favorite_atmospheres_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_user_favorite_atmospheres_venue_atmospheres_atmosphere_id",
                        column: x => x.atmosphere_id,
                        principalTable: "venue_atmospheres",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "artists",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lounge_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    bio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    photo_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bank_account_number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bank_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_artists", x => x.id);
                    table.ForeignKey(
                        name: "f_k_artists_music_lounges_lounge_id",
                        column: x => x.lounge_id,
                        principalTable: "music_lounges",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lounge_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    poster_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    online_quota = table.Column<int>(type: "int", nullable: false),
                    offline_quota = table.Column<int>(type: "int", nullable: false),
                    ticket_sale_closes_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cancellation_allowed = table.Column<bool>(type: "bit", nullable: false),
                    cancellation_deadline_hours = table.Column<int>(type: "int", nullable: true),
                    refund_percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_events", x => x.id);
                    table.ForeignKey(
                        name: "f_k_events_music_lounges_lounge_id",
                        column: x => x.lounge_id,
                        principalTable: "music_lounges",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fnb_menu_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lounge_id = table.Column<int>(type: "int", nullable: false),
                    category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    is_available = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_fnb_menu_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_fnb_menu_items_music_lounges_lounge_id",
                        column: x => x.lounge_id,
                        principalTable: "music_lounges",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "lounge_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lounge_id = table.Column<int>(type: "int", nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_lounge_images", x => x.id);
                    table.ForeignKey(
                        name: "f_k_lounge_images_music_lounges_lounge_id",
                        column: x => x.lounge_id,
                        principalTable: "music_lounges",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "lounge_staff",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lounge_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_lounge_staff", x => x.id);
                    table.ForeignKey(
                        name: "f_k_lounge_staff_music_lounges_lounge_id",
                        column: x => x.lounge_id,
                        principalTable: "music_lounges",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_lounge_staff_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "seating_areas",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    lounge_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    capacity = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_seating_areas", x => x.id);
                    table.ForeignKey(
                        name: "f_k_seating_areas_music_lounges_lounge_id",
                        column: x => x.lounge_id,
                        principalTable: "music_lounges",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "artist_genres",
                columns: table => new
                {
                    artist_id = table.Column<int>(type: "int", nullable: false),
                    genre_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_artist_genres", x => new { x.artist_id, x.genre_id });
                    table.ForeignKey(
                        name: "f_k_artist_genres_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_artist_genres_music_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "music_genres",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "follows",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    artist_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_follows", x => new { x.user_id, x.artist_id });
                    table.ForeignKey(
                        name: "f_k_follows_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_follows_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ai_generated_posters",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    prompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    image_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_selected = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ai_generated_posters", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ai_generated_posters_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_ai_generated_posters_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ai_recommendations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    recommendation_score = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ai_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ai_recommendations_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_ai_recommendations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_artists",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "int", nullable: false),
                    artist_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_artists", x => new { x.event_id, x.artist_id });
                    table.ForeignKey(
                        name: "f_k_event_artists_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_artists_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_atmospheres",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "int", nullable: false),
                    atmosphere_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_atmospheres", x => new { x.event_id, x.atmosphere_id });
                    table.ForeignKey(
                        name: "f_k_event_atmospheres_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_atmospheres_venue_atmospheres_atmosphere_id",
                        column: x => x.atmosphere_id,
                        principalTable: "venue_atmospheres",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_genres",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "int", nullable: false),
                    genre_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_genres", x => new { x.event_id, x.genre_id });
                    table.ForeignKey(
                        name: "f_k_event_genres_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_genres_music_genres_genre_id",
                        column: x => x.genre_id,
                        principalTable: "music_genres",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_moderations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    ai_score = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: true),
                    risk_level = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    flag_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ai_decision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    admin_id = table.Column<int>(type: "int", nullable: true),
                    admin_decision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    review_note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_moderations", x => x.id);
                    table.ForeignKey(
                        name: "f_k_event_moderations_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_moderations_users_admin_id",
                        column: x => x.admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_moods",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "int", nullable: false),
                    mood_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_moods", x => new { x.event_id, x.mood_id });
                    table.ForeignKey(
                        name: "f_k_event_moods_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_moods_moods_mood_id",
                        column: x => x.mood_id,
                        principalTable: "moods",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_ratings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    stars = table.Column<int>(type: "int", nullable: false),
                    review_text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_removed = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_ratings", x => x.id);
                    table.ForeignKey(
                        name: "f_k_event_ratings_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_ratings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_wishlists",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    saved_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_wishlists", x => x.id);
                    table.ForeignKey(
                        name: "f_k_event_wishlists_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_wishlists_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "livestreams",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    started_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ended_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    stream_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    recording_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rewatch_until = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_livestreams", x => x.id);
                    table.ForeignKey(
                        name: "f_k_livestreams_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_behaviour_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_user_behaviour_log", x => x.id);
                    table.ForeignKey(
                        name: "f_k_user_behaviour_log_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_user_behaviour_log_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_area_tickets",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    total_quota = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_event_area_tickets", x => x.id);
                    table.ForeignKey(
                        name: "f_k_event_area_tickets_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_event_area_tickets_seating_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "seating_areas",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fnb_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    audience_id = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    payment_method = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    payment_confirmed = table.Column<bool>(type: "bit", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_fnb_orders", x => x.id);
                    table.ForeignKey(
                        name: "f_k_fnb_orders_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_fnb_orders_seating_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "seating_areas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_fnb_orders_users_audience_id",
                        column: x => x.audience_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    event_id = table.Column<int>(type: "int", nullable: false),
                    buyer_id = table.Column<int>(type: "int", nullable: false),
                    area_id = table.Column<int>(type: "int", nullable: true),
                    ticket_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    qr_code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    purchased_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    checked_in_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    online_verified_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_tickets", x => x.id);
                    table.ForeignKey(
                        name: "f_k_tickets_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_tickets_seating_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "seating_areas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_tickets_users_buyer_id",
                        column: x => x.buyer_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ticket_holds",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    event_area_ticket_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    held_until = table.Column<DateTime>(type: "datetime2", nullable: false),
                    released = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_ticket_holds", x => x.id);
                    table.ForeignKey(
                        name: "f_k_ticket_holds_event_area_tickets_event_area_ticket_id",
                        column: x => x.event_area_ticket_id,
                        principalTable: "event_area_tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_ticket_holds_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "fnb_order_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<int>(type: "int", nullable: false),
                    menu_item_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    cancelled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_fnb_order_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_fnb_order_items_fnb_menu_items_menu_item_id",
                        column: x => x.menu_item_id,
                        principalTable: "fnb_menu_items",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_fnb_order_items_fnb_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "fnb_orders",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticket_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    subscription_id = table.Column<int>(type: "int", nullable: true),
                    payer_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    destination = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    gateway_ref = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_payments", x => x.id);
                    table.ForeignKey(
                        name: "f_k_payments_owner_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "owner_subscriptions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_payments_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_payments_users_payer_id",
                        column: x => x.payer_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "donations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    livestream_id = table.Column<int>(type: "int", nullable: false),
                    donor_id = table.Column<int>(type: "int", nullable: false),
                    artist_id = table.Column<int>(type: "int", nullable: false),
                    payment_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_donations", x => x.id);
                    table.ForeignKey(
                        name: "f_k_donations_artists_artist_id",
                        column: x => x.artist_id,
                        principalTable: "artists",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_donations_livestreams_livestream_id",
                        column: x => x.livestream_id,
                        principalTable: "livestreams",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_donations_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "f_k_donations_users_donor_id",
                        column: x => x.donor_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "i_x_ai_generated_posters_event_id",
                table: "ai_generated_posters",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_generated_posters_owner_id",
                table: "ai_generated_posters",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_recommendations_event_id",
                table: "ai_recommendations",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ai_recommendations_user_id",
                table: "ai_recommendations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_artist_genres_genre_id",
                table: "artist_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "i_x_artists_lounge_id",
                table: "artists",
                column: "lounge_id");

            migrationBuilder.CreateIndex(
                name: "i_x_donations_artist_id",
                table: "donations",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "i_x_donations_donor_id",
                table: "donations",
                column: "donor_id");

            migrationBuilder.CreateIndex(
                name: "i_x_donations_livestream_id",
                table: "donations",
                column: "livestream_id");

            migrationBuilder.CreateIndex(
                name: "i_x_donations_payment_id",
                table: "donations",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_area_tickets_area_id",
                table: "event_area_tickets",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_area_tickets_event_id",
                table: "event_area_tickets",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_artists_artist_id",
                table: "event_artists",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_atmospheres_atmosphere_id",
                table: "event_atmospheres",
                column: "atmosphere_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_genres_genre_id",
                table: "event_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_moderations_admin_id",
                table: "event_moderations",
                column: "admin_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_moderations_event_id",
                table: "event_moderations",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_moods_mood_id",
                table: "event_moods",
                column: "mood_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_ratings_event_id_user_id",
                table: "event_ratings",
                columns: new[] { "event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_event_ratings_user_id",
                table: "event_ratings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_wishlists_event_id",
                table: "event_wishlists",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_event_wishlists_user_id_event_id",
                table: "event_wishlists",
                columns: new[] { "user_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_events_lounge_id",
                table: "events",
                column: "lounge_id");

            migrationBuilder.CreateIndex(
                name: "i_x_fnb_menu_items_lounge_id",
                table: "fnb_menu_items",
                column: "lounge_id");

            migrationBuilder.CreateIndex(
                name: "i_x_fnb_order_items_menu_item_id",
                table: "fnb_order_items",
                column: "menu_item_id");

            migrationBuilder.CreateIndex(
                name: "i_x_fnb_order_items_order_id",
                table: "fnb_order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "i_x_fnb_orders_area_id",
                table: "fnb_orders",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "i_x_fnb_orders_audience_id",
                table: "fnb_orders",
                column: "audience_id");

            migrationBuilder.CreateIndex(
                name: "i_x_fnb_orders_event_id",
                table: "fnb_orders",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_follows_artist_id",
                table: "follows",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "i_x_livestreams_event_id",
                table: "livestreams",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_lounge_images_lounge_id",
                table: "lounge_images",
                column: "lounge_id");

            migrationBuilder.CreateIndex(
                name: "i_x_lounge_staff_lounge_id",
                table: "lounge_staff",
                column: "lounge_id");

            migrationBuilder.CreateIndex(
                name: "i_x_lounge_staff_user_id",
                table: "lounge_staff",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_moods_name",
                table: "moods",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_music_genres_name",
                table: "music_genres",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_music_lounges_atmosphere_id",
                table: "music_lounges",
                column: "atmosphere_id");

            migrationBuilder.CreateIndex(
                name: "i_x_music_lounges_owner_id",
                table: "music_lounges",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "i_x_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_owner_subscriptions_owner_id",
                table: "owner_subscriptions",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "i_x_owner_subscriptions_package_id",
                table: "owner_subscriptions",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "i_x_payments_payer_id",
                table: "payments",
                column: "payer_id");

            migrationBuilder.CreateIndex(
                name: "i_x_payments_subscription_id",
                table: "payments",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "i_x_payments_ticket_id",
                table: "payments",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "i_x_seating_areas_lounge_id",
                table: "seating_areas",
                column: "lounge_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ticket_holds_event_area_ticket_id",
                table: "ticket_holds",
                column: "event_area_ticket_id");

            migrationBuilder.CreateIndex(
                name: "i_x_ticket_holds_user_id",
                table: "ticket_holds",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_tickets_area_id",
                table: "tickets",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "i_x_tickets_buyer_id",
                table: "tickets",
                column: "buyer_id");

            migrationBuilder.CreateIndex(
                name: "i_x_tickets_event_id",
                table: "tickets",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_tickets_qr_code",
                table: "tickets",
                column: "qr_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_user_behaviour_log_event_id",
                table: "user_behaviour_log",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "i_x_user_behaviour_log_user_id",
                table: "user_behaviour_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_user_favorite_atmospheres_atmosphere_id",
                table: "user_favorite_atmospheres",
                column: "atmosphere_id");

            migrationBuilder.CreateIndex(
                name: "i_x_user_favorite_genres_genre_id",
                table: "user_favorite_genres",
                column: "genre_id");

            migrationBuilder.CreateIndex(
                name: "i_x_user_favorite_moods_mood_id",
                table: "user_favorite_moods",
                column: "mood_id");

            migrationBuilder.CreateIndex(
                name: "i_x_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_venue_atmospheres_name",
                table: "venue_atmospheres",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_generated_posters");

            migrationBuilder.DropTable(
                name: "ai_recommendations");

            migrationBuilder.DropTable(
                name: "artist_genres");

            migrationBuilder.DropTable(
                name: "donations");

            migrationBuilder.DropTable(
                name: "event_artists");

            migrationBuilder.DropTable(
                name: "event_atmospheres");

            migrationBuilder.DropTable(
                name: "event_genres");

            migrationBuilder.DropTable(
                name: "event_moderations");

            migrationBuilder.DropTable(
                name: "event_moods");

            migrationBuilder.DropTable(
                name: "event_ratings");

            migrationBuilder.DropTable(
                name: "event_wishlists");

            migrationBuilder.DropTable(
                name: "fnb_order_items");

            migrationBuilder.DropTable(
                name: "follows");

            migrationBuilder.DropTable(
                name: "lounge_images");

            migrationBuilder.DropTable(
                name: "lounge_staff");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "ticket_holds");

            migrationBuilder.DropTable(
                name: "user_behaviour_log");

            migrationBuilder.DropTable(
                name: "user_favorite_atmospheres");

            migrationBuilder.DropTable(
                name: "user_favorite_genres");

            migrationBuilder.DropTable(
                name: "user_favorite_moods");

            migrationBuilder.DropTable(
                name: "livestreams");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "fnb_menu_items");

            migrationBuilder.DropTable(
                name: "fnb_orders");

            migrationBuilder.DropTable(
                name: "artists");

            migrationBuilder.DropTable(
                name: "event_area_tickets");

            migrationBuilder.DropTable(
                name: "music_genres");

            migrationBuilder.DropTable(
                name: "moods");

            migrationBuilder.DropTable(
                name: "owner_subscriptions");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropTable(
                name: "subscription_packages");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "seating_areas");

            migrationBuilder.DropTable(
                name: "music_lounges");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "venue_atmospheres");
        }
    }
}
