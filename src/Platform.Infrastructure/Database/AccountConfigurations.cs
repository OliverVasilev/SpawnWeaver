using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Accounts;

namespace Platform.Infrastructure.Database;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasMaxLength(64);
        builder.Property(u => u.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(User.MaxDisplayNameLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.CreatedAtUtc).IsRequired();
        builder.Property(u => u.UpdatedAtUtc).IsRequired();
        builder.Property(u => u.LastLoginAtUtc);
        builder.Property(u => u.EmailVerifiedAtUtc);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasMaxLength(64);
        builder.Property(o => o.Name).HasMaxLength(Organization.MaxNameLength).IsRequired();
        builder.Property(o => o.OwnerUserId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.CreatedAtUtc).IsRequired();
        builder.Property(o => o.UpdatedAtUtc).IsRequired();

        builder.HasIndex(o => o.OwnerUserId);
    }
}

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasMaxLength(64);
        builder.Property(s => s.UserId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.ExpiresAtUtc).IsRequired();
        builder.Property(s => s.LastSeenAtUtc).IsRequired();

        builder.HasIndex(s => s.UserId);
    }
}

internal sealed class LoginTokenConfiguration : IEntityTypeConfiguration<LoginToken>
{
    public void Configure(EntityTypeBuilder<LoginToken> builder)
    {
        builder.ToTable("login_tokens");

        builder.HasKey(t => t.TokenHash);
        builder.Property(t => t.TokenHash).HasMaxLength(128);
        builder.Property(t => t.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.ConsumedAtUtc);

        builder.HasIndex(t => t.Email);
    }
}

internal sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("email_verification_tokens");

        builder.HasKey(t => t.TokenHash);
        builder.Property(t => t.TokenHash).HasMaxLength(128);
        builder.Property(t => t.UserId).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.ConsumedAtUtc);

        builder.HasIndex(t => t.UserId);
    }
}
