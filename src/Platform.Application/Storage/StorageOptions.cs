namespace Platform.Application.Storage;

/// <summary>Quota/validation limits for player storage (bound from the <c>Storage</c> config section).</summary>
public sealed class StorageOptions
{
    /// <summary>Maximum size, in bytes, of a single stored value.</summary>
    public int MaxValueBytes { get; set; } = 64 * 1024;

    /// <summary>Maximum length of a key.</summary>
    public int MaxKeyLength { get; set; } = 128;

    /// <summary>Maximum number of distinct keys a single player may store.</summary>
    public int MaxKeysPerPlayer { get; set; } = 100;
}
