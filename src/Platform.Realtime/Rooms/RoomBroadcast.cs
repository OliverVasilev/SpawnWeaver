using System.Net.WebSockets;
using Platform.Contracts.Realtime;
using Platform.Realtime.Protocol;

namespace Platform.Realtime.Rooms;

/// <summary>Shared helpers for sending messages to room members and building rosters.</summary>
internal static class RoomBroadcast
{
    public static async Task SendToMembersAsync(
        IReadOnlyList<RoomMember> members,
        string? excludePlayerId,
        string type,
        object payload,
        CancellationToken ct)
    {
        // Serialize the envelope once and reuse the bytes for every recipient.
        byte[]? bytes = null;

        foreach (var member in members)
        {
            if (excludePlayerId is not null && string.Equals(member.PlayerId, excludePlayerId, StringComparison.Ordinal))
            {
                continue;
            }

            bytes ??= RealtimeMessageSender.Serialize(type, requestId: null, payload);

            try
            {
                await RealtimeMessageSender.SendRawAsync(member.Connection, bytes, ct).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Member's socket is gone; its own disconnect handling will clean it up.
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public static RoomPlayer[] ToPlayers(IReadOnlyList<RoomMember> members)
    {
        var players = new RoomPlayer[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            players[i] = new RoomPlayer(members[i].PlayerId, members[i].PlayerName);
        }

        return players;
    }
}
