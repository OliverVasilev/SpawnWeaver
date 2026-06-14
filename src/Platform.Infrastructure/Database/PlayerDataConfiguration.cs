using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Players;

namespace Platform.Infrastructure.Database;

internal sealed class PlayerDataConfiguration : IEntityTypeConfiguration<PlayerDataEntry>
{
    public void Configure(EntityTypeBuilder<PlayerDataEntry> builder)
    {
        builder.ToTable("player_data");

        builder.HasKey(e => new { e.ProjectId, e.PlayerId, e.Key });
        builder.Property(e => e.ProjectId).HasMaxLength(64);
        builder.Property(e => e.PlayerId).HasMaxLength(64);
        builder.Property(e => e.Key).HasMaxLength(128);
        builder.Property(e => e.Value).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();
    }
}
