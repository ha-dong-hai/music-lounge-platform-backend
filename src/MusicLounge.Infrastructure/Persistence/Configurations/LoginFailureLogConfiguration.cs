using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoginFailureLogConfiguration : IEntityTypeConfiguration<LoginFailureLog>
{
    public void Configure(EntityTypeBuilder<LoginFailureLog> b)
    {
        b.ToTable("login_failure_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(255).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);

        b.HasIndex(x => x.IpAddress);
        b.HasIndex(x => x.CreatedAt);
    }
}
