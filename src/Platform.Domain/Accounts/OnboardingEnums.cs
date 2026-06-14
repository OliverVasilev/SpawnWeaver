namespace Platform.Domain.Accounts;

/// <summary>The kind of game a developer is building (Milestone 19.4).</summary>
public enum GameType
{
    Unspecified = 0,
    TurnBased,
    CasualCoop,
    Arena1v1,
    PartyGame,
    LobbyBased,
    RealtimeAction,
    PersistentProgression,
    Other,
}

/// <summary>The multiplayer style a project needs (Milestone 19.5).</summary>
public enum MultiplayerMode
{
    Unspecified = 0,
    RoomsOnly,
    LobbiesAndRooms,
    MatchmakingAndRooms,
    EventRelayOnly,
    StateSync,
    PersistenceOnly,
    NotSure,
}

/// <summary>A category of data a project wants to persist (Milestone 19.6). Multi-select.</summary>
public enum PersistenceFeature
{
    PlayerProfile,
    Inventory,
    Progression,
    MatchHistory,
    SaveSlots,
    CustomKeyValue,
}

/// <summary>Deployment environment for a project's credentials.</summary>
public enum ProjectEnvironment
{
    Development = 0,
    Production,
}
