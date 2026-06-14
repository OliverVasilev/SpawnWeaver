namespace Platform.Api.Auth;

/// <summary>
/// Gates the dashboard: unauthenticated requests to <c>/dashboard/*</c> are redirected to the
/// public Getting Started page, except for the pages a logged-out visitor is allowed to see
/// (Getting Started, sign in, sign up). The landing page (<c>/</c>) and static assets are not
/// under <c>/dashboard</c>, so they stay public.
/// </summary>
public sealed class DashboardGuardMiddleware
{
    private const string GettingStarted = "/dashboard/getting-started";

    private static readonly string[] PublicDashboardPaths =
    [
        GettingStarted,
        "/dashboard/signin",
        "/dashboard/signup",
        "/dashboard/verify-pending",
    ];

    private readonly RequestDelegate _next;

    public DashboardGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (path.StartsWithSegments("/dashboard") && !IsPublic(path) && !IsAuthenticated(context))
        {
            context.Response.Redirect(GettingStarted);
            return;
        }

        await _next(context);
    }

    private static bool IsPublic(PathString path)
    {
        // The whole docs section is public.
        if (path.StartsWithSegments("/dashboard/docs"))
        {
            return true;
        }

        foreach (var publicPath in PublicDashboardPaths)
        {
            if (path.Equals(publicPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAuthenticated(HttpContext context)
        => context.User.Identity?.IsAuthenticated ?? false;
}
