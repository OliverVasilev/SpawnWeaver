namespace Platform.Realtime.Rooms;

/// <summary>Whether a lobby is discoverable in the public list or joinable by code only.</summary>
internal enum LobbyVisibility
{
    Public,
    Private,
}

/// <summary>Developer-facing attributes that turn a room into a lobby.</summary>
internal sealed record LobbyAttributes(
    string? Name,
    LobbyVisibility Visibility,
    int? MaxPlayers,
    IReadOnlyDictionary<string, string> Metadata);
