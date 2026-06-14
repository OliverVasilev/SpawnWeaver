using Platform.Domain.Accounts;

namespace Platform.Domain.Projects;

/// <summary>
/// A registered game project (control plane). Owns its public key and the
/// hash of its secret key — the plaintext secret is never stored on the server.
/// Carries the onboarding profile (game type, multiplayer mode, persistence needs)
/// captured during project creation (Milestone 19.3).
/// </summary>
public sealed class Project
{
    /// <summary>Maximum allowed length for a project name.</summary>
    public const int MaxNameLength = 100;

    public string Id { get; private set; }
    public string? OrganizationId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string PublicKey { get; private set; }
    public string SecretKeyHash { get; private set; }
    public GameType GameType { get; private set; }
    public MultiplayerMode MultiplayerMode { get; private set; }

    /// <summary>Selected persistence features stored as a comma-separated string (EF-mapped).</summary>
    public string PersistenceFeaturesCsv { get; private set; }

    public string? TargetPlatform { get; private set; }
    public ProjectEnvironment Environment { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>The selected persistence features, decoded from <see cref="PersistenceFeaturesCsv"/>.</summary>
    public IReadOnlyList<PersistenceFeature> PersistenceFeatures => DecodeFeatures(PersistenceFeaturesCsv);

    // Used by EF Core when materializing from the database.
    private Project()
    {
        Id = null!;
        Name = null!;
        Slug = null!;
        PublicKey = null!;
        SecretKeyHash = null!;
        PersistenceFeaturesCsv = string.Empty;
    }

    private Project(
        string id,
        string? organizationId,
        string name,
        string slug,
        string publicKey,
        string secretKeyHash,
        GameType gameType,
        MultiplayerMode multiplayerMode,
        string persistenceFeaturesCsv,
        string? targetPlatform,
        ProjectEnvironment environment,
        DateTimeOffset createdAtUtc,
        bool isActive)
    {
        Id = id;
        OrganizationId = organizationId;
        Name = name;
        Slug = slug;
        PublicKey = publicKey;
        SecretKeyHash = secretKeyHash;
        GameType = gameType;
        MultiplayerMode = multiplayerMode;
        PersistenceFeaturesCsv = persistenceFeaturesCsv;
        TargetPlatform = targetPlatform;
        Environment = environment;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        IsActive = isActive;
    }

    /// <summary>
    /// Creates a new active project. The caller is responsible for generating the
    /// id, public key, and the <em>hash</em> of the secret key. Onboarding fields are
    /// optional so legacy/anonymous creation (name only) keeps working.
    /// </summary>
    public static Project Create(
        string id,
        string name,
        string publicKey,
        string secretKeyHash,
        DateTimeOffset createdAtUtc,
        string? organizationId = null,
        GameType gameType = GameType.Unspecified,
        MultiplayerMode multiplayerMode = MultiplayerMode.Unspecified,
        IEnumerable<PersistenceFeature>? persistenceFeatures = null,
        string? targetPlatform = null,
        ProjectEnvironment environment = ProjectEnvironment.Development)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKeyHash);

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Project name must be at most {MaxNameLength} characters.", nameof(name));
        }

        var slug = Slugify(trimmed);
        var csv = EncodeFeatures(persistenceFeatures);

        return new Project(
            id, organizationId, trimmed, slug, publicKey, secretKeyHash,
            gameType, multiplayerMode, csv,
            string.IsNullOrWhiteSpace(targetPlatform) ? null : targetPlatform.Trim(),
            environment, createdAtUtc, isActive: true);
    }

    /// <summary>Deactivates the project so it can no longer be used.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Replaces the secret key with a new one (only its hash is stored). The previous secret
    /// stops working immediately; the caller surfaces the new plaintext once.
    /// </summary>
    public void RotateSecretKey(string newSecretKeyHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecretKeyHash);
        SecretKeyHash = newSecretKeyHash;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Replaces the public key. Any already-shipped game client using the old key can no longer
    /// connect, so this is a deliberate, disruptive action.
    /// </summary>
    public void RotatePublicKey(string newPublicKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPublicKey);
        PublicKey = newPublicKey;
        UpdatedAtUtc = now;
    }

    /// <summary>Produces a URL-friendly slug from a project name.</summary>
    public static string Slugify(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }
        slug = slug.Trim('-');
        return slug.Length == 0 ? "project" : slug;
    }

    private static string EncodeFeatures(IEnumerable<PersistenceFeature>? features)
    {
        if (features is null)
        {
            return string.Empty;
        }

        var distinct = features.Distinct().OrderBy(f => (int)f);
        return string.Join(',', distinct.Select(f => f.ToString()));
    }

    private static List<PersistenceFeature> DecodeFeatures(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        var result = new List<PersistenceFeature>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<PersistenceFeature>(part, out var feature))
            {
                result.Add(feature);
            }
        }

        return result;
    }
}
