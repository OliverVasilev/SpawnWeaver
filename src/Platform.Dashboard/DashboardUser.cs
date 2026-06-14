using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Platform.Application.Accounts;
using Platform.Domain.Accounts;

namespace Platform.Dashboard;

/// <summary>
/// Request-scoped view of the signed-in developer for dashboard (static SSR) components.
/// Identity comes from the validated auth cookie's claims; the organization is loaded
/// on demand. Lives in the Dashboard project to avoid a dependency on the API host.
/// </summary>
public sealed class DashboardUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly OrganizationService _organizations;

    public DashboardUser(IHttpContextAccessor accessor, OrganizationService organizations)
    {
        _accessor = accessor;
        _organizations = organizations;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => Principal?.FindFirstValue(ClaimTypes.Name);

    /// <summary>The current server-side session id. Claim name mirrors DashboardAuth.SessionClaim.</summary>
    public string? SessionId => Principal?.FindFirstValue("sw_sid");

    public async Task<Organization?> GetOrganizationAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        return string.IsNullOrEmpty(userId)
            ? null
            : await _organizations.GetPrimaryForUserAsync(userId, ct);
    }
}
