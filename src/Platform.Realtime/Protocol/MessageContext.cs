using System.Text.Json;
using Platform.Contracts.Realtime;
using Platform.Realtime.Connections;
using Platform.Realtime.Diagnostics;

namespace Platform.Realtime.Protocol;

/// <summary>Per-message context handed to an <see cref="IRealtimeMessageHandler"/>.</summary>
public sealed class MessageContext
{
    private readonly RealtimeActivity? _activity;

    public MessageContext(
        RealtimeConnection connection,
        RealtimeEnvelope envelope,
        CancellationToken cancellationToken,
        RealtimeActivity? activity = null)
    {
        Connection = connection;
        Envelope = envelope;
        CancellationToken = cancellationToken;
        _activity = activity;
    }

    public RealtimeConnection Connection { get; }

    public RealtimeEnvelope Envelope { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Sends a message back to this connection, echoing the inbound <c>requestId</c>
    /// (if any) so the client can correlate the response with its request.
    /// </summary>
    public Task RespondAsync(string type, object? payload = null)
        => RealtimeMessageSender.SendAsync(Connection, type, Envelope.RequestId, payload, CancellationToken);

    /// <summary>Deserializes the envelope payload to <typeparamref name="T"/>, or returns default if absent.</summary>
    public T? PayloadAs<T>()
        => Envelope.Payload is { } payload ? payload.Deserialize<T>(RealtimeJson.ReadOptions) : default;

    /// <summary>Sends a structured <c>error</c> response (echoing the request id) and records
    /// it on the session timeline + error explorer.</summary>
    public Task RespondErrorAsync(string code, string message)
    {
        _activity?.Error(Connection.Id, code, message);
        return RespondAsync(RealtimeMessageTypes.Error, new RealtimeError(code, message));
    }
}
