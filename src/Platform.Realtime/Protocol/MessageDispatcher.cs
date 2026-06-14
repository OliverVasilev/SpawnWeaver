using Platform.Contracts.Realtime;
using Platform.Realtime.Connections;
using Platform.Realtime.Diagnostics;

namespace Platform.Realtime.Protocol;

/// <summary>
/// Routes an inbound message to its registered handler. Malformed messages and unknown
/// types are answered with a structured <c>error</c> envelope.
/// </summary>
public sealed class MessageDispatcher
{
    private const string MalformedMessageText = "The message could not be parsed as a protocol envelope.";

    private readonly Dictionary<string, IRealtimeMessageHandler> _handlers;
    private readonly RealtimeMetrics _metrics;
    private readonly RealtimeActivity _activity;

    public MessageDispatcher(
        IEnumerable<IRealtimeMessageHandler> handlers, RealtimeMetrics metrics, RealtimeActivity activity)
    {
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.Ordinal);
        _metrics = metrics;
        _activity = activity;
    }

    public async Task HandleMessageAsync(
        RealtimeConnection connection,
        ReadOnlyMemory<byte> utf8Json,
        CancellationToken ct)
    {
        if (!EnvelopeReader.TryParse(utf8Json.Span, out var envelope, out _))
        {
            _metrics.ErrorOccurred();
            _activity.Error(connection.Id, ProtocolErrorCodes.MalformedMessage, MalformedMessageText);
            await SendErrorAsync(
                connection, requestId: null, ProtocolErrorCodes.MalformedMessage, MalformedMessageText, ct)
                .ConfigureAwait(false);
            return;
        }

        if (!_handlers.TryGetValue(envelope!.Type, out var handler))
        {
            _metrics.ErrorOccurred();
            var unknownText = $"Unknown message type '{envelope.Type}'.";
            _activity.Error(connection.Id, ProtocolErrorCodes.UnknownMessageType, unknownText);
            await SendErrorAsync(
                connection, envelope.RequestId, ProtocolErrorCodes.UnknownMessageType, unknownText, ct)
                .ConfigureAwait(false);
            return;
        }

        // Record the action on the session timeline (skip noisy keepalive pings).
        if (!string.Equals(envelope.Type, "ping", StringComparison.Ordinal))
        {
            _activity.Action(connection.Id, envelope.Type, "request");
        }

        var context = new MessageContext(connection, envelope, ct, _activity);
        await handler.HandleAsync(context).ConfigureAwait(false);
    }

    private static Task SendErrorAsync(
        RealtimeConnection connection,
        string? requestId,
        string code,
        string message,
        CancellationToken ct)
        => RealtimeMessageSender.SendAsync(
            connection,
            RealtimeMessageTypes.Error,
            requestId,
            new RealtimeError(code, message),
            ct);
}
