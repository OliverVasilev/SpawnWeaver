using System.Security.Claims;
using Platform.Application.Accounts;
using Platform.Domain.Accounts;

namespace Platform.Api.Auth;

/// <summary>
/// Request-scoped accessor for the signed-in dashboard user. Identity claims come
/// straight from the validated auth cookie; the owning organization is loaded on demand.
/// Injectable into both minimal-API endpoints and Blazor (static SSR) components.
/// </summary>
public sealed class CurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly OrganizationService _organizations;

    public CurrentUser(IHttpContextAccessor accessor, OrganizationService organizations)
    {
        _accessor = accessor;
        _organizations = organizations;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => Principal?.FindFirstValue(ClaimTypes.Name);

    public string? SessionId => Principal?.FindFirstValue(DashboardAuth.SessionClaim);

    /// <summary>Loads the user's primary organization, or null if not signed in.</summary>
    public async Task<Organization?> GetOrganizationAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        return string.IsNullOrEmpty(userId)
            ? null
            : await _organizations.GetPrimaryForUserAsync(userId, ct);
    }
}
