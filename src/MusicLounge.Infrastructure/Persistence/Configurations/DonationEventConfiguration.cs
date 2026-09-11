using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class DonationEventConfiguration : IEntityTypeConfiguration<DonationEvent>
{
    public void Configure(EntityTypeBuilder<DonationEvent> b)
    {
        b.ToTable("donation_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Amount).HasPrecision(15, 2);
        b.Property(x => x.Reference).HasMaxLength(255);
        b.Property(x => x.EvidenceUrl).HasMaxLength(500);
        b.Property(x => x.EvidenceSha256).HasMaxLength(64);
        b.Property(x => x.Detail).HasMaxLength(500);
        b.Property(x => x.PreviousHash).HasMaxLength(64);
        b.Property(x => x.Hash).HasMaxLength(64).IsRequired();

        // Một khoản donate chỉ có một chuỗi: hai sự kiện không thể trùng số thứ tự. Hai tiến trình cùng
        // ghi thì một bên thất bại, thay vì chuỗi bị rẽ nhánh mà không ai biết.
        b.HasIndex(x => new { x.DonationId, x.Sequence }).IsUnique();

        b.HasOne<Donation>()
            .WithMany()
            .HasForeignKey(x => x.DonationId)
            .OnDelete(DeleteBehavior.Restrict);

        // NoAction: người dùng không bao giờ bị xoá cứng (xoá dữ liệu cá nhân là ẩn danh hoá tại chỗ),
        // và bảng này phải giữ nguyên ai đã làm gì.
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
