using System.Text.Json;
using Platform.Contracts.Realtime;

namespace Platform.Realtime.Protocol;

/// <summary>Parses inbound UTF-8 JSON into a <see cref="RealtimeEnvelope"/>.</summary>
internal static class EnvelopeReader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Attempts to parse an envelope. Returns <c>false</c> with a reason for malformed JSON
    /// or a missing <c>type</c>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Json, out RealtimeEnvelope? envelope, out string? error)
    {
        try
        {
            envelope = JsonSerializer.Deserialize<RealtimeEnvelope>(utf8Json, Options);
        }
        catch (JsonException ex)
        {
            envelope = null;
            error = ex.Message;
            return false;
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Type))
        {
            envelope = null;
            error = "Message is missing a 'type'.";
            return false;
        }

        error = null;
        return true;
    }
}
