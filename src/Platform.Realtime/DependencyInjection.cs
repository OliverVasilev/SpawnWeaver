using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Realtime.Connections;
using Platform.Realtime.Diagnostics;
using Platform.Realtime.Lobbies;
using Platform.Realtime.Matchmaking;
using Platform.Realtime.Protocol;
using Platform.Realtime.Protocol.Handlers;
using Platform.Realtime.Rooms;
using Platform.Realtime.State;
using Platform.Realtime.Transport;

namespace Platform.Realtime;

public static class DependencyInjection
{
    public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RealtimeOptions>(configuration.GetSection("Realtime"));

        services.AddSingleton<ConnectionManager>();

        // Rooms (in-memory, single-node MVP).
        services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
        services.AddSingleton<RoomManager>();
        services.AddSingleton<RoomService>();
        services.AddSingleton<LobbyService>();
        services.AddHostedService<RoomExpiryService>();

        // Matchmaking (in-memory queue).
        services.AddSingleton<MatchQueue>();
        services.AddSingleton<MatchmakingService>();
        services.AddHostedService<MatchmakingTimeoutService>();

        // State sync (Milestone 23).
        services.Configure<StateOptions>(configuration.GetSection("State"));
        services.AddSingleton<StateService>();

        // Protocol message handlers (registry). New message types register here.
        services.AddSingleton<IRealtimeMessageHandler, PingMessageHandler>();
        services.AddSingleton<IRealtimeMessageHandler, RoomCreateHandler>();
        services.AddSingleton<IRealtimeMessageHandler, RoomJoinHandler>();
        services.AddSingleton<IRealtimeMessageHandler, RoomLeaveHandler>();
        services.AddSingleton<IRealtimeMessageHandler, RoomPlayersHandler>();
        services.AddSingleton<IRealtimeMessageHandler, GameEventHandler>();
        services.AddSingleton<IRealtimeMessageHandler, LobbyCreateHandler>();
        services.AddSingleton<IRealtimeMessageHandler, LobbyListHandler>();
        services.AddSingleton<IRealtimeMessageHandler, LobbyJoinHandler>();
        services.AddSingleton<IRealtimeMessageHandler, MatchmakingJoinHandler>();
        services.AddSingleton<IRealtimeMessageHandler, MatchmakingLeaveHandler>();
        services.AddSingleton<IRealtimeMessageHandler, StateRoomPatchHandler>();
        services.AddSingleton<IRealtimeMessageHandler, StateEntitySetHandler>();
        services.AddSingleton<IRealtimeMessageHandler, StateEntityPatchHandler>();
        services.AddSingleton<IRealtimeMessageHandler, StateEntityDeleteHandler>();
        services.AddSingleton<MessageDispatcher>();

        services.AddSingleton<RealtimeConnectionHandler>();

        // Diagnostics (read-only view for the dashboard / admin API).
        services.AddSingleton<SessionTracker>();
        services.AddSingleton<RealtimeActivity>();
        services.AddSingleton(sp => new RealtimeDiagnostics(
            sp.GetRequiredService<ConnectionManager>(),
            sp.GetRequiredService<RoomManager>(),
            sp.GetRequiredService<SessionTracker>(),
            sp.GetRequiredService<RealtimeActivity>(),
            sp.GetRequiredService<MatchQueue>()));
        services.AddSingleton(sp => new RealtimeMetrics(
            sp.GetRequiredService<ConnectionManager>(),
            sp.GetRequiredService<RoomManager>()));

        return services;
    }
}
