namespace Platform.Realtime.Rooms;

internal enum RoomJoinStatus
{
    Joined,
    NotFound,
    Full,
}

/// <summary>Result of a join attempt (by code or id).</summary>
internal sealed record RoomJoinOutcome(RoomJoinStatus Status, Room? Room, IReadOnlyList<RoomMember> Members)
{
    public static RoomJoinOutcome NotFound { get; } = new(RoomJoinStatus.NotFound, null, []);

    public static RoomJoinOutcome Full(Room room) => new(RoomJoinStatus.Full, room, []);

    public static RoomJoinOutcome Joined(Room room, IReadOnlyList<RoomMember> members)
        => new(RoomJoinStatus.Joined, room, members);
}

/// <summary>Result of a successful leave/removal: the room and the remaining roster.</summary>
internal sealed record RoomLeaveOutcome(Room Room, IReadOnlyList<RoomMember> RemainingMembers);
