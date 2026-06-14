using Microsoft.Extensions.Options;
using Platform.Infrastructure.Ids;
using Platform.Realtime.Rooms;
using Platform.Tests.TestDoubles;
using Xunit;

namespace Platform.Tests.Unit;

public sealed class RoomManagerTests
{
    private readonly MutableClock _clock = new();

    private RoomManager CreateManager(TimeSpan? emptyRoomTtl = null)
    {
        var options = Options.Create(new RealtimeOptions
        {
            EmptyRoomTtl = emptyRoomTtl ?? TimeSpan.FromSeconds(60),
        });

        return new RoomManager(new IdGenerator(), _clock, new RoomCodeGenerator(), options);
    }

    [Fact]
    public void Create_registers_room_with_creator_as_member()
    {
        var manager = CreateManager();
        var creator = TestConnections.Create("proj_a");

        var room = manager.Create("proj_a", creator, "Alice");

        Assert.NotEqual(string.Empty, room.Code);
        Assert.Same(room, manager.Find(room.Id));
        var members = room.MembersSnapshot();
        Assert.Single(members);
        Assert.Equal(creator.Id, members[0].PlayerId);
    }

    [Fact]
    public void Join_by_code_adds_member()
    {
        var manager = CreateManager();
        var room = manager.Create("proj_a", TestConnections.Create("proj_a"), "Alice");

        var outcome = manager.Join("proj_a", room.Code, TestConnections.Create("proj_a"), "Bob");

        Assert.Equal(RoomJoinStatus.Joined, outcome.Status);
        Assert.Equal(2, outcome.Members.Count);
    }

    [Fact]
    public void Join_unknown_code_returns_not_found()
    {
        var manager = CreateManager();

        var outcome = manager.Join("proj_a", "ZZZZZZ", TestConnections.Create("proj_a"), null);

        Assert.Equal(RoomJoinStatus.NotFound, outcome.Status);
    }

    [Fact]
    public void Join_with_wrong_project_returns_not_found()
    {
        var manager = CreateManager();
        var room = manager.Create("proj_a", TestConnections.Create("proj_a"), null);

        var outcome = manager.Join("proj_b", room.Code, TestConnections.Create("proj_b"), null);

        Assert.Equal(RoomJoinStatus.NotFound, outcome.Status);
    }

    [Fact]
    public void Join_a_full_lobby_returns_full()
    {
        var manager = CreateManager();
        var attributes = new LobbyAttributes("Duel", LobbyVisibility.Public, MaxPlayers: 2, new Dictionary<string, string>());
        var lobby = manager.CreateLobby("proj_a", TestConnections.Create("proj_a"), "Host", attributes);
        manager.Join("proj_a", lobby.Code, TestConnections.Create("proj_a"), "Second"); // now 2/2

        var outcome = manager.Join("proj_a", lobby.Code, TestConnections.Create("proj_a"), "Third");

        Assert.Equal(RoomJoinStatus.Full, outcome.Status);
    }

    [Fact]
    public void ListPublicLobbies_excludes_private_and_plain_rooms()
    {
        var manager = CreateManager();
        manager.Create("proj_a", TestConnections.Create("proj_a"), null); // plain room
        manager.CreateLobby("proj_a", TestConnections.Create("proj_a"), null,
            new LobbyAttributes("Private", LobbyVisibility.Private, null, new Dictionary<string, string>()));
        var publicLobby = manager.CreateLobby("proj_a", TestConnections.Create("proj_a"), null,
            new LobbyAttributes("Public", LobbyVisibility.Public, null, new Dictionary<string, string>()));

        var listed = manager.ListPublicLobbies("proj_a");

        Assert.Single(listed);
        Assert.Equal(publicLobby.Id, listed[0].Id);
    }

    [Fact]
    public void Leave_removes_member_and_returns_remaining()
    {
        var manager = CreateManager();
        var room = manager.Create("proj_a", TestConnections.Create("proj_a"), "Alice");
        var bob = TestConnections.Create("proj_a");
        manager.Join("proj_a", room.Code, bob, "Bob");

        var outcome = manager.Leave(bob.Id, room.Id);

        Assert.NotNull(outcome);
        Assert.Single(outcome!.RemainingMembers);
        Assert.DoesNotContain(outcome.RemainingMembers, m => m.PlayerId == bob.Id);
    }

    [Fact]
    public void RemoveConnection_leaves_every_room_the_connection_was_in()
    {
        var manager = CreateManager();
        var connection = TestConnections.Create("proj_a");
        var room1 = manager.Create("proj_a", connection, null);
        var room2 = manager.Create("proj_a", TestConnections.Create("proj_a"), null);
        manager.Join("proj_a", room2.Code, connection, null);

        var outcomes = manager.RemoveConnection(connection.Id);

        Assert.Equal(2, outcomes.Count);
        Assert.Null(manager.Leave(connection.Id, room1.Id)); // already removed
    }

    [Fact]
    public void SweepExpired_removes_empty_room_only_after_ttl()
    {
        var ttl = TimeSpan.FromSeconds(30);
        var manager = CreateManager(ttl);
        var creator = TestConnections.Create("proj_a");
        var room = manager.Create("proj_a", creator, null);
        manager.Leave(creator.Id, room.Id); // room now empty

        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Empty(manager.SweepExpired());
        Assert.NotNull(manager.Find(room.Id));

        _clock.Advance(TimeSpan.FromSeconds(25)); // total 35s > ttl
        var expired = manager.SweepExpired();

        Assert.Single(expired);
        Assert.Null(manager.Find(room.Id));
    }

    [Fact]
    public void SweepExpired_keeps_non_empty_rooms()
    {
        var manager = CreateManager(TimeSpan.FromSeconds(1));
        var room = manager.Create("proj_a", TestConnections.Create("proj_a"), null);

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(manager.SweepExpired());
        Assert.NotNull(manager.Find(room.Id));
    }
}
