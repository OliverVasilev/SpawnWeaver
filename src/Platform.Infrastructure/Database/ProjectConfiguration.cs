using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Projects;

namespace Platform.Infrastructure.Database;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasMaxLength(64);
        builder.Property(p => p.OrganizationId).HasMaxLength(64);
        builder.Property(p => p.Name).HasMaxLength(Project.MaxNameLength).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(120).IsRequired();
        builder.Property(p => p.PublicKey).HasMaxLength(128).IsRequired();
        builder.Property(p => p.SecretKeyHash).HasMaxLength(128).IsRequired();
        builder.Property(p => p.GameType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(p => p.MultiplayerMode).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(p => p.PersistenceFeaturesCsv).HasMaxLength(256).IsRequired();
        builder.Property(p => p.TargetPlatform).HasMaxLength(80);
        builder.Property(p => p.Environment).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        // PersistenceFeatures is a computed projection over PersistenceFeaturesCsv.
        builder.Ignore(p => p.PersistenceFeatures);

        builder.HasIndex(p => p.PublicKey).IsUnique();
        builder.HasIndex(p => p.OrganizationId);
    }
}
