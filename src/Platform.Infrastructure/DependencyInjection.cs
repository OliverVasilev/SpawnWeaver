using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Accounts;
using Platform.Application.Feedback;
using Platform.Application.Projects;
using Platform.Application.Security;
using Platform.Application.Storage;
using Platform.Infrastructure.Accounts;
using Platform.Infrastructure.Database;
using Platform.Infrastructure.Ids;
using Platform.Infrastructure.Repositories;
using Platform.Infrastructure.Security;
using Platform.Infrastructure.Time;

namespace Platform.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionString = "Data Source=spawnweaver.db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "sqlite";
        var connectionString = configuration["ConnectionStrings:Default"];

        services.AddDbContext<PlatformDbContext>(options =>
        {
            if (IsPostgres(provider))
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "Database:Provider is 'postgres' but ConnectionStrings:Default is not set.");
                }

                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString ?? DefaultConnectionString);
            }
        });

        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IPlayerDataRepository, PlayerDataRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<ILoginTokenRepository, LoginTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
        services.AddSingleton<IApiKeyHasher, ApiKeyHasher>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IIdGenerator, IdGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        // Email: real Resend sender when an API key is configured; otherwise the dev (logging)
        // sender, which also auto-verifies accounts so local flows work without a provider.
        var emailOptions = BuildEmailOptions(configuration);
        services.AddSingleton(Options.Create(emailOptions));
        if (UseResend(emailOptions))
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, DevEmailSender>();
        }

        services.AddSingleton(Options.Create(BuildPlayerTokenOptions(configuration)));
        services.AddSingleton<IPlayerTokenService, PlayerTokenService>();

        services.AddSingleton(Options.Create(BuildStorageOptions(configuration)));

        return services;
    }

    private static StorageOptions BuildStorageOptions(IConfiguration configuration)
    {
        var options = new StorageOptions();
        var section = configuration.GetSection("Storage");

        if (int.TryParse(section["MaxValueBytes"], out var maxValue) && maxValue > 0)
        {
            options.MaxValueBytes = maxValue;
        }

        if (int.TryParse(section["MaxKeyLength"], out var maxKey) && maxKey > 0)
        {
            options.MaxKeyLength = maxKey;
        }

        if (int.TryParse(section["MaxKeysPerPlayer"], out var maxKeys) && maxKeys > 0)
        {
            options.MaxKeysPerPlayer = maxKeys;
        }

        return options;
    }

    private static EmailOptions BuildEmailOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("Email");
        var options = new EmailOptions
        {
            Provider = section["Provider"],
            // Accept Email:Resend:ApiKey (nested) or Email:ApiKey (flat).
            ApiKey = section["Resend:ApiKey"] ?? section["ApiKey"],
            PublicBaseUrl = configuration["App:PublicBaseUrl"],
        };

        var from = section["FromAddress"];
        if (!string.IsNullOrWhiteSpace(from))
        {
            options.FromAddress = from;
        }

        var fromName = section["FromName"];
        if (!string.IsNullOrWhiteSpace(fromName))
        {
            options.FromName = fromName;
        }

        return options;
    }

    /// <summary>True when the real Resend sender should be used: provider == "resend", or an API key is present.</summary>
    private static bool UseResend(EmailOptions options)
        => options.Provider?.Equals("resend", StringComparison.OrdinalIgnoreCase) == true
            || options.HasRealProvider;

    private static PlayerTokenOptions BuildPlayerTokenOptions(IConfiguration configuration)
    {
        var options = new PlayerTokenOptions { TokenSecret = configuration["Auth:TokenSecret"] };

        if (TimeSpan.TryParse(configuration["Auth:TokenLifetime"], out var lifetime) && lifetime > TimeSpan.Zero)
        {
            options.TokenLifetime = lifetime;
        }

        return options;
    }

    /// <summary>
    /// Prepares the database at startup: applies migrations for SQLite, or creates the
    /// schema from the model for other providers (e.g. PostgreSQL test mode).
    /// </summary>
    public static void InitializePlatformDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        if (db.Database.IsSqlite())
        {
            db.Database.Migrate();
        }
        else
        {
            db.Database.EnsureCreated();
        }
    }

    private static bool IsPostgres(string provider)
        => provider.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("npgsql", StringComparison.OrdinalIgnoreCase);
}
