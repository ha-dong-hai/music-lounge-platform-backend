using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

/// <summary>MLACP-676. Hàng đợi Admin xét lý do huỷ buổi hòa nhạc.</summary>
internal sealed class ShowCancellationReviewConfiguration : IEntityTypeConfiguration<ShowCancellationReview>
{
    public void Configure(EntityTypeBuilder<ShowCancellationReview> b)
    {
        b.ToTable("show_cancellation_reviews");
        b.HasKey(r => r.Id);

        b.Property(r => r.Reason).HasConversion<string>().HasMaxLength(30);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(r => r.Detail).HasMaxLength(1000).IsRequired();
        b.Property(r => r.EvidenceUrl).HasMaxLength(500);
        b.Property(r => r.AmountRefunded).HasPrecision(18, 2);
        b.Property(r => r.DecisionNote).HasMaxLength(1000);

        b.HasIndex(r => new { r.Status, r.CreatedAt });
        b.HasIndex(r => r.ShowId).IsUnique(); // một buổi diễn chỉ bị huỷ một lần

        // NoAction: lounge_shows đã là bảng con có cascade từ music_lounges; thêm một đường cascade thứ hai vào cùng chuỗi
        // là đúng lỗi SQL Server 1785 mà codebase này đã gặp ba lần. Buổi diễn không bao giờ bị xoá cứng.
        b.HasOne(r => r.Show)
            .WithMany()
            .HasForeignKey(r => r.ShowId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
