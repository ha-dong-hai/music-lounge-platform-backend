using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        b.Property(u => u.Phone).HasMaxLength(20);
        b.Property(u => u.AvatarUrl).HasMaxLength(500);
        b.Property(u => u.PasswordHash).HasMaxLength(500);
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(u => u.AuthProvider).HasMaxLength(20).HasDefaultValue("local");
        b.Property(u => u.GoogleId).HasMaxLength(100);
        b.Property(u => u.IsActive).HasDefaultValue(true);
        b.Property(u => u.DateOfBirth);
        b.Property(u => u.AiConsent).HasDefaultValue(false);
        b.Property(u => u.PasswordResetTokenHash).HasMaxLength(64);
        b.Property(u => u.EmailVerificationCodeHash).HasMaxLength(64);
        b.Property(u => u.CitizenCardNumber).HasMaxLength(20);
        b.Property(u => u.CitizenCardFrontImageUrl).HasMaxLength(500);
        b.Property(u => u.CitizenCardBackImageUrl).HasMaxLength(500);
        b.HasIndex(u => u.Email).IsUnique();
        b.HasIndex(u => u.GoogleId).IsUnique().HasFilter("[GoogleId] IS NOT NULL");
        b.HasIndex(u => u.CitizenCardNumber).IsUnique().HasFilter("[CitizenCardNumber] IS NOT NULL");
    }
}
