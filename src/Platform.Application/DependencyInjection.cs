using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Accounts;
using Platform.Application.Feedback;
using Platform.Application.Projects;
using Platform.Application.Storage;

namespace Platform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProjectService>();
        services.AddScoped<PlayerStorageService>();
        services.AddScoped<FeedbackService>();
        services.AddScoped<AccountService>();
        services.AddScoped<SessionService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<MagicLinkService>();
        services.AddScoped<EmailVerificationService>();
        return services;
    }
}
