using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Platform.Application.Accounts;
using Platform.Domain.Accounts;

namespace Platform.Api.Auth;

/// <summary>Shared constants for the dashboard cookie-auth scheme.</summary>
public static class DashboardAuth
{
    public const string Scheme = "SpawnWeaver.Dashboard";
    public const string CookieName = "sw_auth";

    /// <summary>Claim holding the server-side session id (for revocation/validation).</summary>
    public const string SessionClaim = "sw_sid";

    /// <summary>
    /// Registers cookie authentication. Each request re-validates the server-side session,
    /// so sign-out / sign-out-everywhere take effect immediately.
    /// </summary>
    public static IServiceCollection AddDashboardAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();

        services.AddAuthentication(Scheme)
            .AddCookie(Scheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = SessionService.DefaultLifetime;
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidateSessionAsync,
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Signs a user in: creates a server-side session and issues the auth cookie with the
    /// user's identity claims plus the session id.
    /// </summary>
    public static async Task SignInUserAsync(HttpContext context, User user, SessionService sessions)
    {
        var session = await sessions.CreateAsync(user.Id, context.RequestAborted);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(SessionClaim, session.Id),
        };

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(Scheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = session.ExpiresAtUtc,
        });
    }

    /// <summary>Signs the current user out and revokes their server-side session.</summary>
    public static async Task SignOutUserAsync(HttpContext context, SessionService sessions)
    {
        var sessionId = context.User.FindFirstValue(SessionClaim);
        if (!string.IsNullOrEmpty(sessionId))
        {
            await sessions.RevokeAsync(sessionId, context.RequestAborted);
        }

        await context.SignOutAsync(Scheme);
    }

    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        var sessionId = context.Principal?.FindFirstValue(SessionClaim);
        if (string.IsNullOrEmpty(sessionId))
        {
            context.RejectPrincipal();
            return;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
        var session = await sessions.ValidateAsync(sessionId, context.HttpContext.RequestAborted);
        if (session is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }
}
