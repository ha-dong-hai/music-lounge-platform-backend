using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeMuteConfiguration : IEntityTypeConfiguration<LoungeMute>
{
    public void Configure(EntityTypeBuilder<LoungeMute> b)
    {
        b.ToTable("lounge_mutes");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.LoungeId }).IsUnique();

        // Khong khai dieu huong nguoc lai tren User/MusicLounge: quan he nay chi duoc doc tu phia
        // nguoi dung nen mot chieu la du, va tranh sua vao hai thuc the dang co.
        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
