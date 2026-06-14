using Microsoft.EntityFrameworkCore;
using Platform.Domain.Accounts;
using Platform.Domain.Feedback;
using Platform.Domain.Players;
using Platform.Domain.Projects;

namespace Platform.Infrastructure.Database;

/// <summary>EF Core context for the control-plane database (SQLite for MVP).</summary>
public sealed class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<PlayerDataEntry> PlayerData => Set<PlayerDataEntry>();

    public DbSet<FeedbackEntry> Feedback => Set<FeedbackEntry>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<LoginToken> LoginTokens => Set<LoginToken>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
