using Platform.Application.Abstractions;
using Platform.Application.Security;
using Platform.Domain.Accounts;
using Platform.Domain.Projects;

namespace Platform.Application.Projects;

/// <summary>The plaintext secret key is part of the result only at creation time.</summary>
public sealed record ProjectCreationResult(Project Project, string PlaintextSecretKey);

/// <summary>
/// Onboarding selections captured during project creation v2 (Milestone 19.3–19.6).
/// All fields are optional so name-only creation keeps working.
/// </summary>
public sealed record CreateProjectCommand(
    string Name,
    string? OrganizationId = null,
    GameType GameType = GameType.Unspecified,
    MultiplayerMode MultiplayerMode = MultiplayerMode.Unspecified,
    IReadOnlyList<PersistenceFeature>? PersistenceFeatures = null,
    string? TargetPlatform = null,
    ProjectEnvironment Environment = ProjectEnvironment.Development);

/// <summary>Application use cases for registering and looking up projects.</summary>
public sealed class ProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApiKeyGenerator _keys;
    private readonly IApiKeyHasher _hasher;
    private readonly IIdGenerator _ids;
    private readonly IClock _clock;

    public ProjectService(
        IProjectRepository projects,
        IUnitOfWork unitOfWork,
        IApiKeyGenerator keys,
        IApiKeyHasher hasher,
        IIdGenerator ids,
        IClock clock)
    {
        _projects = projects;
        _unitOfWork = unitOfWork;
        _keys = keys;
        _hasher = hasher;
        _ids = ids;
        _clock = clock;
    }

    /// <summary>
    /// Registers a new project. Generates the public/secret key pair, stores only the
    /// secret's hash, and returns the plaintext secret once for the caller to surface.
    /// </summary>
    public async Task<ProjectCreationResult> CreateAsync(string name, CancellationToken ct = default)
    {
        var id = _ids.NewId("proj");
        var publicKey = _keys.GeneratePublicKey();
        var secretKey = _keys.GenerateSecretKey();
        var secretKeyHash = _hasher.Hash(secretKey);

        var project = Project.Create(id, name, publicKey, secretKeyHash, _clock.UtcNow);

        await _projects.AddAsync(project, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ProjectCreationResult(project, secretKey);
    }

    /// <summary>
    /// Registers a project with its onboarding profile (game type, multiplayer mode,
    /// persistence needs) and optional owning organization (project creation v2).
    /// </summary>
    public async Task<ProjectCreationResult> CreateAsync(CreateProjectCommand command, CancellationToken ct = default)
    {
        var id = _ids.NewId("proj");
        var publicKey = _keys.GeneratePublicKey();
        var secretKey = _keys.GenerateSecretKey();
        var secretKeyHash = _hasher.Hash(secretKey);

        var project = Project.Create(
            id, command.Name, publicKey, secretKeyHash, _clock.UtcNow,
            organizationId: command.OrganizationId,
            gameType: command.GameType,
            multiplayerMode: command.MultiplayerMode,
            persistenceFeatures: command.PersistenceFeatures,
            targetPlatform: command.TargetPlatform,
            environment: command.Environment);

        await _projects.AddAsync(project, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ProjectCreationResult(project, secretKey);
    }

    public Task<Project?> GetAsync(string id, CancellationToken ct = default)
        => _projects.GetByIdAsync(id, ct);

    /// <summary>
    /// Generates a fresh secret key for a project owned by the given organization, stores its
    /// hash, and returns the new plaintext once. Null if the project doesn't exist or isn't
    /// owned by that organization.
    /// </summary>
    public async Task<string?> RotateSecretKeyAsync(string projectId, string organizationId, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OrganizationId != organizationId)
        {
            return null;
        }

        var secretKey = _keys.GenerateSecretKey();
        project.RotateSecretKey(_hasher.Hash(secretKey), _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return secretKey;
    }

    /// <summary>
    /// Generates a fresh public key for a project owned by the given organization and returns it.
    /// Shipped clients using the old key stop connecting. Null if not found / not owned.
    /// </summary>
    public async Task<string?> RotatePublicKeyAsync(string projectId, string organizationId, CancellationToken ct = default)
    {
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project is null || project.OrganizationId != organizationId)
        {
            return null;
        }

        var publicKey = _keys.GeneratePublicKey();
        project.RotatePublicKey(publicKey, _clock.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
        return publicKey;
    }

    /// <summary>Lists projects owned by an organization (dashboard, signed-in scope).</summary>
    public Task<IReadOnlyList<Project>> ListByOrganizationAsync(string organizationId, CancellationToken ct = default)
        => _projects.ListByOrganizationAsync(organizationId, ct);

    /// <summary>Looks up a project by its public key (used to authenticate realtime connections).</summary>
    public Task<Project?> GetByPublicKeyAsync(string publicKey, CancellationToken ct = default)
        => _projects.GetByPublicKeyAsync(publicKey, ct);

    /// <summary>Lists the most recently created projects (admin/dashboard).</summary>
    public Task<IReadOnlyList<Project>> ListAsync(int limit = 100, CancellationToken ct = default)
        => _projects.ListAsync(limit, ct);
}
