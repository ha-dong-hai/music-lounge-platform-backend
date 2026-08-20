using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class KnownAdminSnapshotConfiguration : IEntityTypeConfiguration<KnownAdminSnapshot>
{
    public void Configure(EntityTypeBuilder<KnownAdminSnapshot> b)
    {
        b.ToTable("known_admin_snapshots");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.UserId).IsUnique();
    }
}
