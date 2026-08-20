using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Infrastructure.Persistence.Configurations;

internal sealed class LoginSpikeAlertStateConfiguration : IEntityTypeConfiguration<LoginSpikeAlertState>
{
    public void Configure(EntityTypeBuilder<LoginSpikeAlertState> b)
    {
        b.ToTable("login_spike_alert_states");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.IpAddress).IsUnique();
    }
}
