using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Feedback;

namespace Platform.Infrastructure.Database;

internal sealed class FeedbackConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("feedback");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasMaxLength(64);
        builder.Property(f => f.Email).HasMaxLength(FeedbackEntry.MaxEmailLength);
        builder.Property(f => f.Message).HasMaxLength(FeedbackEntry.MaxMessageLength).IsRequired();
        builder.Property(f => f.CreatedAtUtc).IsRequired();
    }
}
