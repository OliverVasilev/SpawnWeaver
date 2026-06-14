using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class SecurityTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public SecurityTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Invalid_project_key_cannot_connect()
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = _factory.BuildConnectUri("pk_not_a_real_key");

        await Assert.ThrowsAnyAsync<Exception>(() => wsClient.ConnectAsync(uri, CancellationToken.None));
    }

    [Fact]
    public async Task Per_project_connection_limit_is_enforced()
    {
        // A server variant with a 2-connection-per-project cap.
        using var limited = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Realtime:MaxConnectionsPerProject"] = "2",
                })));

        var key = await limited.CreateProjectKeyAsync("Capped Project");

        using var first = await limited.ConnectReadyAsync(key);
        using var second = await limited.ConnectReadyAsync(key);

        // The third connection should be rejected.
        await Assert.ThrowsAnyAsync<Exception>(() => limited.ConnectAsync(key));
    }

    [Fact]
    public async Task Lobby_with_oversized_metadata_is_rejected()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await _factory.ConnectReadyAsync(key);

        var hugeValue = new string('x', 1024); // exceeds the 256-char default
        await socket.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new
        {
            visibility = "public",
            metadata = new { big = hugeValue },
        });

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.InvalidPayload, reply.DeserializePayload<RealtimeError>()!.Code);
    }

    [Fact]
    public async Task Lobby_with_too_many_metadata_entries_is_rejected()
    {
        var key = await _factory.CreateProjectKeyAsync();
        using var socket = await _factory.ConnectReadyAsync(key);

        var metadata = new Dictionary<string, string>();
        for (var i = 0; i < 32; i++) // exceeds the 16-entry default
        {
            metadata[$"k{i}"] = "v";
        }

        await socket.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new
        {
            visibility = "public",
            metadata,
        });

        var reply = await socket.ReceiveEnvelopeAsync();
        Assert.Equal(RealtimeMessageTypes.Error, reply.Type);
        Assert.Equal(ProtocolErrorCodes.InvalidPayload, reply.DeserializePayload<RealtimeError>()!.Code);
    }

    [Fact]
    public async Task Public_lobbies_do_not_leak_across_projects()
    {
        var keyA = await _factory.CreateProjectKeyAsync("Project A");
        var keyB = await _factory.CreateProjectKeyAsync("Project B");

        // Project A creates a public lobby.
        using var a = await _factory.ConnectReadyAsync(keyA);
        await a.SendMessageAsync(RealtimeMessageTypes.LobbyCreate, "c1", new { visibility = "public" });
        var created = (await a.ReceiveEnvelopeAsync()).DeserializePayload<LobbyCreatedPayload>()!;

        // Project B lists lobbies and must not see project A's lobby.
        using var b = await _factory.ConnectReadyAsync(keyB);
        await b.SendMessageAsync(RealtimeMessageTypes.LobbyList, "l1", null);
        var list = (await b.ReceiveEnvelopeAsync()).DeserializePayload<LobbyListPayload>()!;

        Assert.DoesNotContain(list.Lobbies, l => l.LobbyId == created.LobbyId);
    }
}
