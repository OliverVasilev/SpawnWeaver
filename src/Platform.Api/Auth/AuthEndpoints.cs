using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Platform.Application.Accounts;
using Platform.Contracts.Http;

namespace Platform.Api.Auth;

/// <summary>
/// Account endpoints backing the dashboard sign-up / sign-in / settings flow
/// (Milestone 19.1). Auth state is carried in an HttpOnly cookie; these are called
/// from the dashboard via same-origin JSON fetch.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/signup", async (
            SignUpRequest request, AccountService accounts, SessionService sessions,
            EmailVerificationService verification, IEmailSender email, IOptions<EmailOptions> emailOptions,
            HttpContext context) =>
        {
            var result = await accounts.SignUpAsync(
                request.Email, request.DisplayName, request.Password, context.RequestAborted);

            if (!result.Succeeded)
            {
                return result.Status switch
                {
                    SignUpStatus.EmailAlreadyInUse => Results.Conflict(
                        Problem("email", "An account with this email already exists.")),
                    SignUpStatus.WeakPassword => Results.BadRequest(
                        Problem("password", $"Password must be at least {AccountService.MinPasswordLength} characters.")),
                    _ => Results.BadRequest(Problem("email", "Enter a valid email address.")),
                };
            }

            var user = result.User!;

            // No real email provider (local/dev): auto-verify and sign in so the address-less
            // local flow (and the quickstart script) keeps working without an inbox.
            if (email.IsDevSender)
            {
                await accounts.MarkEmailVerifiedAsync(user.Id, context.RequestAborted);
                await DashboardAuth.SignInUserAsync(context, user, sessions);
                return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName, "/dashboard/onboarding"));
            }

            // Real provider: require email verification before sign-in. Send the link, don't sign in.
            await IssueAndSendVerificationAsync(user, verification, email, emailOptions.Value, context);
            return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName, "/dashboard/verify-pending"));
        });

        auth.MapPost("/signin", async (
            SignInRequest request, AccountService accounts, SessionService sessions,
            IEmailSender email, HttpContext context) =>
        {
            var user = await accounts.AuthenticateAsync(request.Email, request.Password, context.RequestAborted);
            if (user is null)
            {
                return Results.Json(Problem("email", "Incorrect email or password."), statusCode: StatusCodes.Status401Unauthorized);
            }

            // Hard gate: a real provider means the email must be confirmed before sign-in.
            if (!user.IsEmailVerified && !email.IsDevSender)
            {
                return Results.Json(
                    new { code = "email_not_verified", message = "Verify your email to sign in — check your inbox for the confirmation link.", email = user.Email },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            await DashboardAuth.SignInUserAsync(context, user, sessions);
            return Results.Ok(new AuthResponse(user.Id, user.Email, user.DisplayName, "/dashboard"));
        });

        // Confirm an emailed verification link: sign in on success, otherwise bounce to a resend page.
        auth.MapGet("/verify", async (
            string token, EmailVerificationService verification, SessionService sessions, HttpContext context) =>
        {
            var user = await verification.ConsumeAsync(token, context.RequestAborted);
            if (user is null)
            {
                return Results.Redirect("/dashboard/verify-pending?expired=1");
            }

            await DashboardAuth.SignInUserAsync(context, user, sessions);
            return Results.Redirect("/dashboard");
        });

        // Re-send a verification link. Always returns 200 (no account enumeration); throttled per user.
        auth.MapPost("/verify/resend", async (
            ResendVerificationRequest request, AccountService accounts, EmailVerificationService verification,
            IEmailSender email, IOptions<EmailOptions> emailOptions, HttpContext context) =>
        {
            if (!email.IsDevSender)
            {
                var user = await accounts.GetByEmailAsync(request.Email, context.RequestAborted);
                if (user is not null && !user.IsEmailVerified)
                {
                    await IssueAndSendVerificationAsync(user, verification, email, emailOptions.Value, context);
                }
            }

            return Results.Ok(new { message = "If that account still needs verification, we've sent a new link." });
        });

        auth.MapPost("/signout", async (SessionService sessions, HttpContext context) =>
        {
            await DashboardAuth.SignOutUserAsync(context, sessions);
            return Results.Ok(new { redirect = "/dashboard/signin" });
        });

        // Passwordless "magic link" sign-in (one-click for the free beta).
        auth.MapPost("/magic/request", async (
            MagicLinkRequest request, MagicLinkService magic, IOptions<EmailOptions> emailOptions,
            HttpContext context, IHostEnvironment env) =>
        {
            var result = await magic.RequestAsync(request.Email, context.RequestAborted);
            if (!result.Sent)
            {
                return Results.Json(
                    new MagicLinkResponse(false, "Please wait a moment before requesting another link.", null),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            string? devLink = null;
            if (result.RawToken is not null)
            {
                var link = BuildLink(context, emailOptions.Value, $"/api/auth/magic?token={Uri.EscapeDataString(result.RawToken)}");
                await magic.SendAsync(request.Email, link, context.RequestAborted);
                if (!env.IsProduction() && magic.IsDevSender)
                {
                    devLink = link; // dev only: no real email provider, so surface the link
                }
            }

            return Results.Ok(new MagicLinkResponse(true, "Check your email for a sign-in link.", devLink));
        });

        auth.MapGet("/magic", async (string token, MagicLinkService magic, SessionService sessions, HttpContext context) =>
        {
            var user = await magic.ConsumeAsync(token, context.RequestAborted);
            if (user is null)
            {
                return Results.Redirect("/dashboard/signin?error=link");
            }

            await DashboardAuth.SignInUserAsync(context, user, sessions);
            return Results.Redirect("/dashboard");
        });

        MapAccountEndpoints(app);
        return app;
    }

    private static void MapAccountEndpoints(IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account").WithTags("Account");

        account.MapGet("", async (CurrentUser current, AccountService accounts, CancellationToken ct) =>
        {
            if (!current.IsAuthenticated || current.UserId is null)
            {
                return Results.Unauthorized();
            }

            var user = await accounts.GetByIdAsync(current.UserId, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var org = await current.GetOrganizationAsync(ct);
            return Results.Ok(new AccountResponse(
                user.Id, user.Email, user.DisplayName, user.CreatedAtUtc, user.LastLoginAtUtc,
                org?.Id, org?.Name));
        });

        account.MapPut("", async (
            UpdateAccountRequest request, CurrentUser current, AccountService accounts, CancellationToken ct) =>
        {
            if (!current.IsAuthenticated || current.UserId is null)
            {
                return Results.Unauthorized();
            }

            var ok = await accounts.UpdateDisplayNameAsync(current.UserId, request.DisplayName ?? string.Empty, ct);
            return ok
                ? Results.Ok()
                : Results.BadRequest(Problem("displayName", "Display name is required."));
        });

        account.MapPost("/password", async (
            ChangePasswordRequest request, CurrentUser current, AccountService accounts, CancellationToken ct) =>
        {
            if (!current.IsAuthenticated || current.UserId is null)
            {
                return Results.Unauthorized();
            }

            var ok = await accounts.ChangePasswordAsync(
                current.UserId, request.CurrentPassword ?? string.Empty, request.NewPassword ?? string.Empty, ct);
            return ok
                ? Results.Ok()
                : Results.BadRequest(Problem("password",
                    $"Check your current password; the new one must be at least {AccountService.MinPasswordLength} characters."));
        });

        account.MapGet("/sessions", async (CurrentUser current, SessionService sessions, CancellationToken ct) =>
        {
            if (!current.IsAuthenticated || current.UserId is null)
            {
                return Results.Unauthorized();
            }

            var list = await sessions.ListForUserAsync(current.UserId, ct);
            var current_sid = current.SessionId;
            var summaries = list
                .Select(s => new SessionSummary(
                    s.Id, s.CreatedAtUtc, s.LastSeenAtUtc, s.ExpiresAtUtc,
                    Current: string.Equals(s.Id, current_sid, StringComparison.Ordinal)))
                .ToArray();
            return Results.Ok(new SessionsResponse(summaries));
        });

        account.MapPost("/sessions/revoke-all", async (
            CurrentUser current, AccountService accounts, SessionService sessions, HttpContext context, CancellationToken ct) =>
        {
            if (!current.IsAuthenticated || current.UserId is null)
            {
                return Results.Unauthorized();
            }

            // Revoke every session (including this one), then mint a fresh session for the
            // current browser so the developer stays signed in here.
            await sessions.RevokeAllAsync(current.UserId, ct);
            var user = await accounts.GetByIdAsync(current.UserId, ct);
            if (user is not null)
            {
                await DashboardAuth.SignInUserAsync(context, user, sessions);
            }

            return Results.Ok();
        });
    }

    /// <summary>Issues a verification token for a user and emails the absolute confirmation link.</summary>
    private static async Task IssueAndSendVerificationAsync(
        Platform.Domain.Accounts.User user,
        EmailVerificationService verification,
        IEmailSender email,
        EmailOptions options,
        HttpContext context)
    {
        var issue = await verification.IssueAsync(user, context.RequestAborted);
        if (issue.RawToken is null)
        {
            return; // already verified, or throttled
        }

        var link = BuildLink(context, options, $"/api/auth/verify?token={Uri.EscapeDataString(issue.RawToken)}");
        await email.SendEmailVerificationAsync(user.Email, link, context.RequestAborted);
    }

    /// <summary>Builds an absolute URL using the configured public base URL, or the request origin.</summary>
    private static string BuildLink(HttpContext context, EmailOptions options, string pathAndQuery)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? $"{context.Request.Scheme}://{context.Request.Host}"
            : options.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}{pathAndQuery}";
    }

    private static Dictionary<string, string[]> Problem(string field, string message)
        => new() { [field] = [message] };
}
