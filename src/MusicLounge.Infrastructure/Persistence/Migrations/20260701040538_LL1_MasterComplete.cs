using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicLounge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LL1_MasterComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PhoneVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ZoneId",
                table: "TicketTiers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatingZoneId",
                table: "TicketPrices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "LoungeShows",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RatingOpenUntil",
                table: "LoungeShows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "ledger_entries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bank_accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountHolder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComplainantUserId = table.Column<int>(type: "int", nullable: true),
                    TargetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceUrls = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AdminId = table.Column<int>(type: "int", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_complaints_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_complaints_Users_ComplainantUserId",
                        column: x => x.ComplainantUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "custom_criteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Options = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_custom_criteria_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fnb_menu_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fnb_menu_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fnb_menu_items_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lounge_images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lounge_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lounge_images_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lounge_staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AssignedBy = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lounge_staff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lounge_staff_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lounge_staff_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lounge_staff_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refund_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AmountRequested = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountApproved = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundPercentage = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProcessedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refund_requests_Users_ProcessedBy",
                        column: x => x.ProcessedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refund_requests_Users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_refund_requests_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seating_zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seating_zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_seating_zones_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    BillingCycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxTicketsPerEvent = table.Column<int>(type: "int", nullable: false),
                    HasAiPoster = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_config",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedBy = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_config", x => x.Id);
                    table.UniqueConstraint("AK_system_config_ConfigKey", x => x.ConfigKey);
                    table.ForeignKey(
                        name: "FK_system_config_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_event_scores",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(8,6)", precision: 8, scale: 6, nullable: false),
                    Breakdown = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_event_scores", x => new { x.UserId, x.ShowId });
                    table.ForeignKey(
                        name: "FK_user_event_scores_LoungeShows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_event_scores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venue_penalties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    PenaltyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceRef = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuedBy = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SuspensionDays = table.Column<int>(type: "int", nullable: true),
                    SuspensionEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AppealDeadline = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppealedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AppealResult = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompensationNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_penalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_penalties_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_venue_penalties_Users_IssuedBy",
                        column: x => x.IssuedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_venue_penalties_Users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_custom_values",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShowId = table.Column<int>(type: "int", nullable: false),
                    CriteriaId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_custom_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_custom_values_LoungeShows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_custom_values_custom_criteria_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "custom_criteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_custom_preferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CriteriaId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(4,3)", precision: 4, scale: 3, nullable: false, defaultValue: 0.5m),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_custom_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_custom_preferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_custom_preferences_custom_criteria_CriteriaId",
                        column: x => x.CriteriaId,
                        principalTable: "custom_criteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fnb_orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoungeId = table.Column<int>(type: "int", nullable: false),
                    ShowId = table.Column<int>(type: "int", nullable: true),
                    AudienceUserId = table.Column<int>(type: "int", nullable: true),
                    StaffId = table.Column<int>(type: "int", nullable: true),
                    ZoneId = table.Column<int>(type: "int", nullable: true),
                    TableNote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false, defaultValue: 0m),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fnb_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fnb_orders_LoungeShows_ShowId",
                        column: x => x.ShowId,
                        principalTable: "LoungeShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fnb_orders_Lounges_LoungeId",
                        column: x => x.LoungeId,
                        principalTable: "Lounges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fnb_orders_Users_AudienceUserId",
                        column: x => x.AudienceUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fnb_orders_Users_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fnb_orders_seating_zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "seating_zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "owner_subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MaxTicketsPerEventSnapshot = table.Column<int>(type: "int", nullable: false),
                    HasAiPosterSnapshot = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owner_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_owner_subscriptions_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_owner_subscriptions_subscription_packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "subscription_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "system_config_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_config_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_system_config_history_Users_ChangedBy",
                        column: x => x.ChangedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_system_config_history_system_config_ConfigKey",
                        column: x => x.ConfigKey,
                        principalTable: "system_config",
                        principalColumn: "ConfigKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fnb_order_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FnbOrderId = table.Column<int>(type: "int", nullable: false),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    Cancelled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fnb_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fnb_order_items_fnb_menu_items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "fnb_menu_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fnb_order_items_fnb_orders_FnbOrderId",
                        column: x => x.FnbOrderId,
                        principalTable: "fnb_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTiers_ZoneId",
                table: "TicketTiers",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketPrices_SeatingZoneId",
                table: "TicketPrices",
                column: "SeatingZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_TransactionId",
                table: "payments",
                column: "TransactionId",
                unique: true,
                filter: "[TransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LoungeShows_CategoryId",
                table: "LoungeShows",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_accounts_OwnerType_OwnerId",
                table: "bank_accounts",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_complaints_AdminId",
                table: "complaints",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_ComplainantUserId",
                table: "complaints",
                column: "ComplainantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_complaints_Status_CreatedAt",
                table: "complaints",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_custom_criteria_LoungeId_Key",
                table: "custom_criteria",
                columns: new[] { "LoungeId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_categories_Name",
                table: "event_categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_custom_values_CriteriaId",
                table: "event_custom_values",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_event_custom_values_ShowId_CriteriaId",
                table: "event_custom_values",
                columns: new[] { "ShowId", "CriteriaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fnb_menu_items_LoungeId",
                table: "fnb_menu_items",
                column: "LoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_fnb_order_items_FnbOrderId",
                table: "fnb_order_items",
                column: "FnbOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_fnb_order_items_MenuItemId",
                table: "fnb_order_items",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_fnb_orders_AudienceUserId",
                table: "fnb_orders",
                column: "AudienceUserId");

            migrationBuilder.CreateIndex(
                name: "IX_fnb_orders_LoungeId_Status",
                table: "fnb_orders",
                columns: new[] { "LoungeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_fnb_orders_ShowId",
                table: "fnb_orders",
                column: "ShowId");

            migrationBuilder.CreateIndex(
                name: "IX_fnb_orders_StaffId",
                table: "fnb_orders",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_fnb_orders_ZoneId",
                table: "fnb_orders",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_accounts_OwnerType_OwnerId",
                table: "ledger_accounts",
                columns: new[] { "OwnerType", "OwnerId" },
                unique: true,
                filter: "[OwnerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_lounge_images_LoungeId_IsPrimary",
                table: "lounge_images",
                columns: new[] { "LoungeId", "IsPrimary" },
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_lounge_staff_AssignedBy",
                table: "lounge_staff",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_lounge_staff_LoungeId_UserId_IsActive",
                table: "lounge_staff",
                columns: new[] { "LoungeId", "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_lounge_staff_UserId",
                table: "lounge_staff",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_IsRead_CreatedAt",
                table: "notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_owner_subscriptions_OwnerId_Status",
                table: "owner_subscriptions",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_owner_subscriptions_PackageId",
                table: "owner_subscriptions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_PaymentId_Status",
                table: "refund_requests",
                columns: new[] { "PaymentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_ProcessedBy",
                table: "refund_requests",
                column: "ProcessedBy");

            migrationBuilder.CreateIndex(
                name: "IX_refund_requests_RequestedBy",
                table: "refund_requests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_seating_zones_LoungeId",
                table: "seating_zones",
                column: "LoungeId");

            migrationBuilder.CreateIndex(
                name: "IX_system_config_ConfigKey",
                table: "system_config",
                column: "ConfigKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_config_UpdatedBy",
                table: "system_config",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_system_config_history_ChangedBy",
                table: "system_config_history",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_system_config_history_ConfigKey_ChangedAt",
                table: "system_config_history",
                columns: new[] { "ConfigKey", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_custom_preferences_CriteriaId",
                table: "user_custom_preferences",
                column: "CriteriaId");

            migrationBuilder.CreateIndex(
                name: "IX_user_custom_preferences_UserId_CriteriaId",
                table: "user_custom_preferences",
                columns: new[] { "UserId", "CriteriaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_event_scores_ShowId",
                table: "user_event_scores",
                column: "ShowId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_penalties_IssuedBy",
                table: "venue_penalties",
                column: "IssuedBy");

            migrationBuilder.CreateIndex(
                name: "IX_venue_penalties_LoungeId_Status",
                table: "venue_penalties",
                columns: new[] { "LoungeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_venue_penalties_ReviewedBy",
                table: "venue_penalties",
                column: "ReviewedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_ledger_accounts_AccountId",
                table: "ledger_entries",
                column: "AccountId",
                principalTable: "ledger_accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LoungeShows_event_categories_CategoryId",
                table: "LoungeShows",
                column: "CategoryId",
                principalTable: "event_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketPrices_seating_zones_SeatingZoneId",
                table: "TicketPrices",
                column: "SeatingZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTiers_seating_zones_ZoneId",
                table: "TicketTiers",
                column: "ZoneId",
                principalTable: "seating_zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_ledger_accounts_AccountId",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_LoungeShows_event_categories_CategoryId",
                table: "LoungeShows");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketPrices_seating_zones_SeatingZoneId",
                table: "TicketPrices");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketTiers_seating_zones_ZoneId",
                table: "TicketTiers");

            migrationBuilder.DropTable(
                name: "bank_accounts");

            migrationBuilder.DropTable(
                name: "complaints");

            migrationBuilder.DropTable(
                name: "event_categories");

            migrationBuilder.DropTable(
                name: "event_custom_values");

            migrationBuilder.DropTable(
                name: "fnb_order_items");

            migrationBuilder.DropTable(
                name: "ledger_accounts");

            migrationBuilder.DropTable(
                name: "lounge_images");

            migrationBuilder.DropTable(
                name: "lounge_staff");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "owner_subscriptions");

            migrationBuilder.DropTable(
                name: "refund_requests");

            migrationBuilder.DropTable(
                name: "system_config_history");

            migrationBuilder.DropTable(
                name: "user_custom_preferences");

            migrationBuilder.DropTable(
                name: "user_event_scores");

            migrationBuilder.DropTable(
                name: "venue_penalties");

            migrationBuilder.DropTable(
                name: "fnb_menu_items");

            migrationBuilder.DropTable(
                name: "fnb_orders");

            migrationBuilder.DropTable(
                name: "subscription_packages");

            migrationBuilder.DropTable(
                name: "system_config");

            migrationBuilder.DropTable(
                name: "custom_criteria");

            migrationBuilder.DropTable(
                name: "seating_zones");

            migrationBuilder.DropIndex(
                name: "IX_TicketTiers_ZoneId",
                table: "TicketTiers");

            migrationBuilder.DropIndex(
                name: "IX_TicketPrices_SeatingZoneId",
                table: "TicketPrices");

            migrationBuilder.DropIndex(
                name: "IX_payments_TransactionId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_LoungeShows_CategoryId",
                table: "LoungeShows");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "PhoneVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "TicketTiers");

            migrationBuilder.DropColumn(
                name: "SeatingZoneId",
                table: "TicketPrices");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "RatingOpenUntil",
                table: "LoungeShows");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "ledger_entries");
        }
    }
}
