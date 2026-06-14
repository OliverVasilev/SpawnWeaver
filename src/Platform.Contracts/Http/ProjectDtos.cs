namespace Platform.Contracts.Http;

/// <summary>
/// Request body for <c>POST /api/projects</c>. Onboarding fields are optional so
/// name-only creation keeps working; enum fields carry the enum <em>name</em>
/// (e.g. <c>Arena1v1</c>, <c>MatchmakingAndRooms</c>, <c>PlayerProfile</c>).
/// </summary>
public sealed record CreateProjectRequest(
    string Name,
    string? GameType = null,
    string? MultiplayerMode = null,
    IReadOnlyList<string>? PersistenceFeatures = null,
    string? TargetPlatform = null,
    string? Environment = null);

/// <summary>
/// Response for <c>POST /api/projects</c>. <see cref="SecretKey"/> is returned
/// exactly once at creation time and is never retrievable again.
/// </summary>
public sealed record CreateProjectResponse(
    string Id,
    string Name,
    string PublicKey,
    string SecretKey,
    DateTimeOffset CreatedAtUtc,
    string Slug,
    string? OrganizationId,
    string GameType,
    string MultiplayerMode,
    IReadOnlyList<string> PersistenceFeatures,
    string Environment,
    SetupPlanDto RecommendedSetup);

/// <summary>Response for <c>GET /api/projects/{projectId}</c>. Never includes the secret.</summary>
public sealed record ProjectResponse(
    string Id,
    string Name,
    string PublicKey,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    string Slug,
    string? OrganizationId,
    string GameType,
    string MultiplayerMode,
    IReadOnlyList<string> PersistenceFeatures,
    string Environment);
