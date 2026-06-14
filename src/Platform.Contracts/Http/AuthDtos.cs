namespace Platform.Contracts.Http;

/// <summary>Request body for <c>POST /api/auth/signup</c>.</summary>
public sealed record SignUpRequest(string Email, string? DisplayName, string Password);

/// <summary>Request body for <c>POST /api/auth/signin</c>.</summary>
public sealed record SignInRequest(string Email, string Password);

/// <summary>Request body for <c>POST /api/auth/magic/request</c>.</summary>
public sealed record MagicLinkRequest(string Email);

/// <summary>Request body for <c>POST /api/auth/verify/resend</c>.</summary>
public sealed record ResendVerificationRequest(string Email);

/// <summary>
/// Response for <c>POST /api/auth/magic/request</c>. <see cref="DevLink"/> is only populated in
/// non-production environments (no real email provider) so you can click the link during dev.
/// </summary>
public sealed record MagicLinkResponse(bool Sent, string Message, string? DevLink);

/// <summary>
/// Result of a successful auth call. The session cookie is set via <c>Set-Cookie</c>;
/// <see cref="Redirect"/> tells the dashboard JS where to navigate next.
/// </summary>
public sealed record AuthResponse(string UserId, string Email, string DisplayName, string Redirect);

/// <summary>Response for <c>GET /api/account</c> — the signed-in user's profile.</summary>
public sealed record AccountResponse(
    string Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    string? OrganizationId,
    string? OrganizationName);

/// <summary>Request body for <c>PUT /api/account</c>.</summary>
public sealed record UpdateAccountRequest(string DisplayName);

/// <summary>Request body for <c>POST /api/account/password</c>.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>One active dashboard session, shown in account settings.</summary>
public sealed record SessionSummary(
    string Id,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool Current);

/// <summary>Response for <c>GET /api/account/sessions</c>.</summary>
public sealed record SessionsResponse(IReadOnlyList<SessionSummary> Sessions);
