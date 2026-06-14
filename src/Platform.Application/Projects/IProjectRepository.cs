using Platform.Domain.Projects;

namespace Platform.Application.Projects;

/// <summary>Persistence boundary for <see cref="Project"/> aggregates.</summary>
public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken ct = default);

    Task<Project?> GetByIdAsync(string id, CancellationToken ct = default);

    Task<Project?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default);

    Task<IReadOnlyList<Project>> ListAsync(int limit, CancellationToken ct = default);

    /// <summary>Projects owned by an organization, most-recent first.</summary>
    Task<IReadOnlyList<Project>> ListByOrganizationAsync(string organizationId, CancellationToken ct = default);
}
