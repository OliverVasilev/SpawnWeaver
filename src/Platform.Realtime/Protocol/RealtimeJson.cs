using System.Text.Json;

namespace Platform.Realtime.Protocol;

/// <summary>Shared JSON options for reading inbound payloads.</summary>
internal static class RealtimeJson
{
    public static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);
}
