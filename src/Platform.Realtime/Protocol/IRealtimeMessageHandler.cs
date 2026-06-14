namespace Platform.Realtime.Protocol;

/// <summary>
/// Handles a single realtime message <see cref="Type"/>. Implementations are registered in DI
/// and resolved by the <see cref="MessageDispatcher"/>.
/// </summary>
public interface IRealtimeMessageHandler
{
    /// <summary>The envelope <c>type</c> this handler is responsible for.</summary>
    string Type { get; }

    Task HandleAsync(MessageContext context);
}
