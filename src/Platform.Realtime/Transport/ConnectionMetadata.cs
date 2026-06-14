namespace Platform.Realtime.Transport;

/// <summary>
/// Diagnostic metadata captured at connection time for the dashboard's connection inspector:
/// client IP and, when the SDK reports them, its version and the Godot engine version.
/// </summary>
public sealed record ConnectionMetadata(string? IpAddress, string? SdkVersion, string? Engine)
{
    public static readonly ConnectionMetadata Empty = new(null, null, null);
}
