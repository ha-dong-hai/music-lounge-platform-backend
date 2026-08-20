using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Atmospheres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Atmospheres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VnPayResponseCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Performers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Performers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AiConsent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDebit = table.Column<bool>(type: "bit", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_settlements_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PerformerGenres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformerId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformerGenres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformerGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PerformerGenres_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lounges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address_Street = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Address_Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address_District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address_Latitude = table.Column<double>(type: "float", nullable: true),
                    Address_Longitude = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lounges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lounges_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserFavouriteAtmospheres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AtmosphereId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavouriteAtmospheres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavouriteAtmospheres_Atmospheres_AtmosphereId",
                        column: x => x.AtmosphereId,
                        principalTable: "Atmospheres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFavouriteAtmospheres_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFavouriteGenres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavouriteGenres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavouriteGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFavouriteGenres_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFavouriteMoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MoodId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavouriteMoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavouriteMoods_Moods_MoodId",
                        column: x => x.MoodId,
                        principalTable: "Moods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFavouriteMoods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Follows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MusicLoungeId = table.Column<int>(type: "int", nullable: true),
                    PerformerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Follows_Lounges_MusicLoungeId",
                        column: x => x.MusicLoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Follows_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Follows_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoungeShows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ScheduledStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActualEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    Quota = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoungeShows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoungeShows_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    FinalScore = table.Column<float>(type: "real", nullable: false),
                    ContentScore = table.Column<float>(type: "real", nullable: false),
                    CollabScore = table.Column<float>(type: "real", nullable: false),
                    CustomScore = table.Column<float>(type: "real", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRecommendations_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiRecommendations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BehaviourLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviourLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BehaviourLogs_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BehaviourLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "livestreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    CloudflareInputId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RtmpUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StreamKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HlsUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ViewerCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_livestreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_livestreams_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoungeShowAtmospheres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    AtmosphereId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoungeShowAtmospheres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoungeShowAtmospheres_Atmospheres_AtmosphereId",
                        column: x => x.AtmosphereId,
                        principalTable: "Atmospheres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoungeShowAtmospheres_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoungeShowGenres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoungeShowGenres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoungeShowGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoungeShowGenres_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoungeShowMoods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    MoodId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoungeShowMoods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoungeShowMoods_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoungeShowMoods_Moods_MoodId",
                        column: x => x.MoodId,
                        principalTable: "Moods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Performances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    PerformerId = table.Column<int>(type: "int", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Performances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Performances_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Performances_Performers_PerformerId",
                        column: x => x.PerformerId,
                        principalTable: "Performers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ratings_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketTiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AccessType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTiers_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Wishlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoungeShowId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wishlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wishlists_LoungeShows_LoungeShowId",
                        column: x => x.LoungeShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Wishlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "livestream_chat_messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LivestreamId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_livestream_chat_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_livestream_chat_messages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_livestream_chat_messages_livestreams_LivestreamId",
                        column: x => x.LivestreamId,
                        principalTable: "livestreams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LivestreamId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SongTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArtistName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_song_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_song_requests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_song_requests_livestreams_LivestreamId",
                        column: x => x.LivestreamId,
                        principalTable: "livestreams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TierId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quota = table.Column<int>(type: "int", nullable: true),
                    SaleStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SaleEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurchaseChannel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketPrices_TicketTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "TicketTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_holds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PriceId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_holds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ticket_holds_TicketPrices_PriceId",
                        column: x => x.PriceId,
                        principalTable: "TicketPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ticket_holds_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    BuyerId = table.Column<int>(type: "int", nullable: true),
                    PriceId = table.Column<int>(type: "int", nullable: false),
                    TierId = table.Column<int>(type: "int", nullable: false),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QrCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PurchaseChannel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tickets_LoungeShows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_TicketPrices_PriceId",
                        column: x => x.PriceId,
                        principalTable: "TicketPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_TicketTiers_TierId",
                        column: x => x.TierId,
                        principalTable: "TicketTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_Users_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tickets_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "livestream_ticket_details",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StreamUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StreamKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_livestream_ticket_details", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_livestream_ticket_details_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "physical_ticket_details",
                columns: table => new
                {
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatInfo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckedInAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CheckedInByStaffId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_physical_ticket_details", x => x.TicketId);
                    table.ForeignKey(
                        name: "FK_physical_ticket_details_tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_LoungeShowId",
                table: "AiRecommendations",
                column: "LoungeShowId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_UserId_ExpiresAt",
                table: "AiRecommendations",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendations_UserId_LoungeShowId",
                table: "AiRecommendations",
                columns: new[] { "UserId", "LoungeShowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Atmospheres_Name",
                table: "Atmospheres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourLogs_CreatedAt",
                table: "BehaviourLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourLogs_LoungeShowId",
                table: "BehaviourLogs",
                column: "LoungeShowId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourLogs_UserId_LoungeShowId_Action",
                table: "BehaviourLogs",
                columns: new[] { "UserId", "LoungeShowId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_MusicLoungeId",
                table: "Follows",
                column: "MusicLoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_PerformerId",
                table: "Follows",
                column: "PerformerId");

            migrationBuilder.CreateIndex(
                name: "IX_Follows_TargetType_TargetId",
                table: "Follows",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_UserId_TargetType_TargetId",
                table: "Follows",
                columns: new[] { "UserId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_JournalId",
                table: "ledger_entries",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PaymentId",
                table: "ledger_entries",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_livestream_chat_messages_LivestreamId_SentAt",
                table: "livestream_chat_messages",
                columns: new[] { "LivestreamId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_livestream_chat_messages_UserId",
                table: "livestream_chat_messages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_livestreams_LoungeShowId",
                table: "livestreams",
                column: "LoungeShowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lounges_Address_City",
                table: "Lounges",
                column: "Address_City");

            migrationBuilder.CreateIndex(
                name: "IX_Lounges_Address_City_Address_District",
                table: "Lounges",
                columns: new[] { "Address_City", "Address_District" });

            migrationBuilder.CreateIndex(
                name: "IX_Lounges_OwnerId",
                table: "Lounges",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShowAtmospheres_AtmosphereId",
                table: "LoungeShowAtmospheres",
                column: "AtmosphereId");

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShowAtmospheres_LoungeShowId_AtmosphereId",
                table: "LoungeShowAtmospheres",
                columns: new[] { "LoungeShowId", "AtmosphereId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShowGenres_GenreId",
                table: "LoungeShowGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShowGenres_LoungeShowId_GenreId",
                table: "LoungeShowGenres",
                columns: new[] { "LoungeShowId", "GenreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShowMoods_LoungeShowId_MoodId",
                table: "LoungeShowMoods",
                columns: new[] { "LoungeShowId", "MoodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShowMoods_MoodId",
                table: "LoungeShowMoods",
                column: "MoodId");

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShows_LoungeId_Status",
                table: "LoungeShows",
                columns: new[] { "LoungeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShows_ScheduledStart",
                table: "LoungeShows",
                column: "ScheduledStart");

            migrationBuilder.CreateIndex(
                name: "IX_Moods_Name",
                table: "Moods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                table: "payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_Status",
                table: "payments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Performances_LoungeShowId_PerformerId",
                table: "Performances",
                columns: new[] { "LoungeShowId", "PerformerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Performances_PerformerId",
                table: "Performances",
                column: "PerformerId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformerGenres_GenreId",
                table: "PerformerGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformerGenres_PerformerId_GenreId",
                table: "PerformerGenres",
                columns: new[] { "PerformerId", "GenreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_LoungeShowId",
                table: "Ratings",
                column: "LoungeShowId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId_LoungeShowId",
                table: "Ratings",
                columns: new[] { "UserId", "LoungeShowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlements_OwnerId_Status",
                table: "settlements",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_PaymentId",
                table: "settlements",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_ScheduledAt",
                table: "settlements",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_song_requests_LivestreamId_Status",
                table: "song_requests",
                columns: new[] { "LivestreamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_song_requests_UserId",
                table: "song_requests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_holds_ExpiresAt",
                table: "ticket_holds",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_holds_PriceId_ExpiresAt",
                table: "ticket_holds",
                columns: new[] { "PriceId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_holds_UserId",
                table: "ticket_holds",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPrices_TierId",
                table: "TicketPrices",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_BuyerId",
                table: "tickets",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_PaymentId",
                table: "tickets",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_PriceId",
                table: "tickets",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_QrCode",
                table: "tickets",
                column: "QrCode",
                unique: true,
                filter: "[QrCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_ShowId_Status",
                table: "tickets",
                columns: new[] { "ShowId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tickets_TierId",
                table: "tickets",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTiers_LoungeShowId",
                table: "TicketTiers",
                column: "LoungeShowId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteAtmospheres_AtmosphereId",
                table: "UserFavouriteAtmospheres",
                column: "AtmosphereId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteAtmospheres_UserId_AtmosphereId",
                table: "UserFavouriteAtmospheres",
                columns: new[] { "UserId", "AtmosphereId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteGenres_GenreId",
                table: "UserFavouriteGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteGenres_UserId_GenreId",
                table: "UserFavouriteGenres",
                columns: new[] { "UserId", "GenreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteMoods_MoodId",
                table: "UserFavouriteMoods",
                column: "MoodId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavouriteMoods_UserId_MoodId",
                table: "UserFavouriteMoods",
                columns: new[] { "UserId", "MoodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wishlists_LoungeShowId",
                table: "Wishlists",
                column: "LoungeShowId");

            migrationBuilder.CreateIndex(
                name: "IX_Wishlists_UserId_LoungeShowId",
                table: "Wishlists",
                columns: new[] { "UserId", "LoungeShowId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiRecommendations");

            migrationBuilder.DropTable(
                name: "BehaviourLogs");

            migrationBuilder.DropTable(
                name: "Follows");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "livestream_chat_messages");

            migrationBuilder.DropTable(
                name: "livestream_ticket_details");

            migrationBuilder.DropTable(
                name: "LoungeShowAtmospheres");

            migrationBuilder.DropTable(
                name: "LoungeShowGenres");

            migrationBuilder.DropTable(
                name: "LoungeShowMoods");

            migrationBuilder.DropTable(
                name: "Performances");

            migrationBuilder.DropTable(
                name: "PerformerGenres");

            migrationBuilder.DropTable(
                name: "physical_ticket_details");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "settlements");

            migrationBuilder.DropTable(
                name: "song_requests");

            migrationBuilder.DropTable(
                name: "ticket_holds");

            migrationBuilder.DropTable(
                name: "UserFavouriteAtmospheres");

            migrationBuilder.DropTable(
                name: "UserFavouriteGenres");

            migrationBuilder.DropTable(
                name: "UserFavouriteMoods");

            migrationBuilder.DropTable(
                name: "Wishlists");

            migrationBuilder.DropTable(
                name: "Performers");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropTable(
                name: "livestreams");

            migrationBuilder.DropTable(
                name: "Atmospheres");

            migrationBuilder.DropTable(
                name: "Genres");

            migrationBuilder.DropTable(
                name: "Moods");

            migrationBuilder.DropTable(
                name: "TicketPrices");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "TicketTiers");

            migrationBuilder.DropTable(
                name: "LoungeShows");

            migrationBuilder.DropTable(
                name: "Lounges");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
