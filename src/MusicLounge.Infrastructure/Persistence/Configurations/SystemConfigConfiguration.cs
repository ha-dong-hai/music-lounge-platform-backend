using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> b)
    {
        b.ToTable("system_config");
        b.HasKey(x => x.Id);
        b.Property(x => x.ConfigKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.ConfigKey).IsUnique();
        b.Property(x => x.ConfigValue).HasMaxLength(500).IsRequired();
        b.Property(x => x.DataType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.History)
            .WithOne(h => h.Config)
            .HasForeignKey(h => h.ConfigKey)
            .HasPrincipalKey(x => x.ConfigKey)
            .OnDelete(DeleteBehavior.Cascade);

        var seed = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            // Payment & Tax (NĐ 117/2025, NĐ 52/2024)
            new { Id = 1,  ConfigKey = "gateway_fee_rate",                   ConfigValue = "0.02", DataType = ConfigDataType.Decimal,  Description = "VNPay gateway processing fee (2%) — NĐ 52/2024",                  UpdatedAt = seed },
            new { Id = 2,  ConfigKey = "platform_commission_rate",           ConfigValue = "0.05", DataType = ConfigDataType.Decimal,  Description = "Platform fee rate (5%) — NĐ 117/2025",                            UpdatedAt = seed },
            new { Id = 3,  ConfigKey = "tax_rate",                           ConfigValue = "0.05", DataType = ConfigDataType.Decimal,  Description = "VAT withheld at source (5%) — NĐ 117/2025",                       UpdatedAt = seed },
            // Settlement schedule
            new { Id = 4,  ConfigKey = "settlement_days_before",             ConfigValue = "3",    DataType = ConfigDataType.Integer,  Description = "Days before scheduled_start to release partial settlement",         UpdatedAt = seed },
            new { Id = 5,  ConfigKey = "settlement_days_after",              ConfigValue = "3",    DataType = ConfigDataType.Integer,  Description = "Days after actual_end to release final settlement",                 UpdatedAt = seed },
            new { Id = 6,  ConfigKey = "settlement_completion_threshold_pct",ConfigValue = "0.70", DataType = ConfigDataType.Decimal,  Description = "D16: min actual/scheduled ratio for auto-release final settlement", UpdatedAt = seed },
            // Settlement tier pre_rates (D3)
            new { Id = 7,  ConfigKey = "settlement_tier_new_pre_rate",       ConfigValue = "0.50", DataType = ConfigDataType.Decimal,  Description = "D3 Tier Mới: pre_rate for venues score<3.5 or <3 shows",          UpdatedAt = seed },
            new { Id = 8,  ConfigKey = "settlement_tier_standard_pre_rate",  ConfigValue = "0.70", DataType = ConfigDataType.Decimal,  Description = "D3 Tier Chuẩn: pre_rate for venues score 3.5–4.2",                UpdatedAt = seed },
            new { Id = 9,  ConfigKey = "settlement_tier_premium_pre_rate",   ConfigValue = "0.80", DataType = ConfigDataType.Decimal,  Description = "D3 Tier Premium: pre_rate for venues score≥4.2 AND ≥10 shows",    UpdatedAt = seed },
            // Settlement tier thresholds
            new { Id = 10, ConfigKey = "settlement_tier_standard_min_score", ConfigValue = "3.5",  DataType = ConfigDataType.Decimal,  Description = "D3: reputation_score threshold to qualify for Tier Chuẩn",        UpdatedAt = seed },
            new { Id = 11, ConfigKey = "settlement_tier_premium_min_score",  ConfigValue = "4.2",  DataType = ConfigDataType.Decimal,  Description = "D3: reputation_score threshold to qualify for Tier Premium",       UpdatedAt = seed },
            new { Id = 12, ConfigKey = "settlement_tier_premium_min_shows",  ConfigValue = "10",   DataType = ConfigDataType.Integer,  Description = "D3: minimum completed shows to qualify for Tier Premium",          UpdatedAt = seed },
            // Moderation (NĐ 147/2024, D11)
            new { Id = 13, ConfigKey = "ai_priority_high_threshold",         ConfigValue = "0.60", DataType = ConfigDataType.Decimal,  Description = "AI score ≥ this → urgent queue for Admin review — D11",           UpdatedAt = seed },
            new { Id = 14, ConfigKey = "ai_priority_low_threshold",          ConfigValue = "0.20", DataType = ConfigDataType.Decimal,  Description = "AI score ≤ this → normal queue for Admin review — D11",           UpdatedAt = seed },
            new { Id = 15, ConfigKey = "moderation_sla_hours",               ConfigValue = "24",   DataType = ConfigDataType.Integer,  Description = "Admin SLA to review flagged content — NĐ 147/2024",               UpdatedAt = seed },
            // Tickets & Donations
            new { Id = 16, ConfigKey = "ticket_hold_minutes",                ConfigValue = "15",   DataType = ConfigDataType.Integer,  Description = "Checkout hold duration before slot released — §6.3",              UpdatedAt = seed },
            new { Id = 17, ConfigKey = "donation_hold_days",                 ConfigValue = "7",    DataType = ConfigDataType.Integer,  Description = "Days before auto-confirm donation if Owner inactive — D4",         UpdatedAt = seed },
            new { Id = 21, ConfigKey = "donation_performer_share_rate",      ConfigValue = "0.88", DataType = ConfigDataType.Decimal,  Description = "§6.5 chặng 2: % of gross donation forwarded to performer",         UpdatedAt = seed },
            // Ratings & Appeals
            new { Id = 18, ConfigKey = "rating_window_days",                 ConfigValue = "7",    DataType = ConfigDataType.Integer,  Description = "Days after show end to submit rating — §6.13",                    UpdatedAt = seed },
            new { Id = 19, ConfigKey = "appeal_sla_hours",                   ConfigValue = "48",   DataType = ConfigDataType.Integer,  Description = "Hours for Admin to review penalty appeal — §6.17",                UpdatedAt = seed },
            new { Id = 20, ConfigKey = "appeal_auto_approve",                ConfigValue = "true", DataType = ConfigDataType.Boolean,  Description = "Auto-approve appeal when Admin misses SLA — §6.17",               UpdatedAt = seed }
        );
    }
}
