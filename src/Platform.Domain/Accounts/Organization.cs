namespace Platform.Domain.Accounts;

/// <summary>
/// A workspace that owns projects. Every user gets a personal organization on sign-up;
/// the model already allows multiple members/orgs later (Milestone 19.2).
/// </summary>
public sealed class Organization
{
    public const int MaxNameLength = 100;

    public string Id { get; private set; }
    public string Name { get; private set; }
    public string OwnerUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    // EF Core materialization.
    private Organization()
    {
        Id = null!;
        Name = null!;
        OwnerUserId = null!;
    }

    private Organization(string id, string name, string ownerUserId, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Name = name;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public static Organization Create(string id, string name, string ownerUserId, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            trimmed = trimmed[..MaxNameLength];
        }

        return new Organization(id, trimmed, ownerUserId, createdAtUtc);
    }

    public void Rename(string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        Name = trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
        UpdatedAtUtc = now;
    }
}
