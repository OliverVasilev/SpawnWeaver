using Platform.Domain.Accounts;

namespace Platform.Application.Accounts;

/// <summary>Read/maintenance use cases for organizations (workspaces).</summary>
public sealed class OrganizationService
{
    private readonly IOrganizationRepository _organizations;

    public OrganizationService(IOrganizationRepository organizations)
        => _organizations = organizations;

    public Task<Organization?> GetAsync(string id, CancellationToken ct = default)
        => _organizations.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Organization>> ListForUserAsync(string userId, CancellationToken ct = default)
        => _organizations.ListByOwnerAsync(userId, ct);

    /// <summary>Returns the user's primary (oldest) organization, or null if they have none.</summary>
    public async Task<Organization?> GetPrimaryForUserAsync(string userId, CancellationToken ct = default)
    {
        var orgs = await _organizations.ListByOwnerAsync(userId, ct);
        return orgs.OrderBy(o => o.CreatedAtUtc).FirstOrDefault();
    }
}
