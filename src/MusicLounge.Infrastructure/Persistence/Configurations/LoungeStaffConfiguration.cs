using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoungeStaffConfiguration : IEntityTypeConfiguration<LoungeStaff>
{
    public void Configure(EntityTypeBuilder<LoungeStaff> b)
    {
        b.ToTable("lounge_staff");
        b.HasKey(x => x.Id);
        b.Property(x => x.IsActive).HasDefaultValue(true);

        b.HasIndex(x => new { x.LoungeId, x.UserId, x.IsActive });

        // Moi tai khoan chi duoc active staff o DUY NHAT 1 venue tai 1 thoi diem — filtered unique
        // index la hang rao cuoi cung chan dung race condition (2 request AssignStaff dong thoi
        // cho 2 venue khac nhau cung 1 user deu vuot qua check AnyAsync o tang Application vi chua
        // request nao commit truoc), DbUpdateException se duoc GlobalExceptionHandler map sang 409.
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsActive] = 1");

        b.HasOne(x => x.Lounge)
            .WithMany()
            .HasForeignKey(x => x.LoungeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.AssignedByUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
