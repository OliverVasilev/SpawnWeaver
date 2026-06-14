using Platform.Domain.Accounts;

namespace Platform.Application.Onboarding;

/// <summary>A single recommended setup step shown after project creation (Milestone 19.7).</summary>
/// <param name="Title">Human-readable action, e.g. "Enable matchmaking".</param>
/// <param name="Done">Whether the project's onboarding selections already satisfy this step.</param>
public sealed record SetupStep(string Title, bool Done = false);

/// <summary>The full recommended setup path for a project.</summary>
/// <param name="Headline">Short summary tailored to the chosen game type.</param>
/// <param name="ExampleProject">The example project that best matches this game type.</param>
/// <param name="Steps">Ordered checklist of recommended steps.</param>
public sealed record SetupPlan(string Headline, string ExampleProject, IReadOnlyList<SetupStep> Steps);

/// <summary>
/// Turns a project's onboarding selections (game type, multiplayer mode, persistence)
/// into a recommended setup checklist and the best-matching example project. Pure logic,
/// so it is trivially testable and reused by both the API and the dashboard.
/// </summary>
public static class SetupRecommendation
{
    /// <summary>Builds a tailored setup plan from the onboarding selections.</summary>
    public static SetupPlan Build(
        GameType gameType,
        MultiplayerMode multiplayerMode,
        IReadOnlyList<PersistenceFeature> persistence)
    {
        var steps = new List<SetupStep>();

        // Multiplayer feature steps, driven primarily by game type with the explicit
        // multiplayer-mode selection refining them.
        var wantsMatchmaking = gameType is GameType.Arena1v1 or GameType.RealtimeAction
            || multiplayerMode == MultiplayerMode.MatchmakingAndRooms;
        var wantsLobby = gameType is GameType.PartyGame or GameType.LobbyBased or GameType.CasualCoop
            || multiplayerMode == MultiplayerMode.LobbiesAndRooms;
        var wantsStateSync = gameType is GameType.Arena1v1 or GameType.RealtimeAction or GameType.CasualCoop
            || multiplayerMode == MultiplayerMode.StateSync;

        if (wantsMatchmaking)
        {
            steps.Add(new SetupStep("Enable matchmaking"));
        }

        if (wantsLobby)
        {
            steps.Add(new SetupStep("Enable lobbies + ready checks"));
        }

        // Every multiplayer project uses rooms + event relay as the baseline.
        steps.Add(new SetupStep("Enable rooms & event relay"));

        if (gameType == GameType.TurnBased)
        {
            steps.Add(new SetupStep("Enable room state (turn/phase)"));
        }

        if (wantsStateSync)
        {
            steps.Add(new SetupStep("Enable entity state sync"));
        }

        // Persistence steps, from the explicit persistence selections.
        if (persistence.Contains(PersistenceFeature.PlayerProfile))
        {
            steps.Add(new SetupStep("Enable player profile persistence", Done: true));
        }

        if (persistence.Contains(PersistenceFeature.Progression))
        {
            steps.Add(new SetupStep("Enable progression/XP persistence", Done: true));
        }

        if (persistence.Contains(PersistenceFeature.Inventory))
        {
            steps.Add(new SetupStep("Enable inventory persistence", Done: true));
        }

        if (persistence.Count > 0
            && !persistence.Contains(PersistenceFeature.PlayerProfile)
            && !persistence.Contains(PersistenceFeature.Progression)
            && !persistence.Contains(PersistenceFeature.Inventory))
        {
            steps.Add(new SetupStep("Enable key-value persistence", Done: true));
        }

        // Universal closing steps.
        steps.Add(new SetupStep("Install the Godot SDK"));
        var example = ExampleFor(gameType, wantsMatchmaking, wantsLobby);
        steps.Add(new SetupStep($"Run the {example} example"));

        return new SetupPlan(HeadlineFor(gameType), example, steps);
    }

    private static string HeadlineFor(GameType gameType) => gameType switch
    {
        GameType.TurnBased => "Turn-based games work great with room state + persistence.",
        GameType.CasualCoop => "Casual co-op pairs rooms + entity state + events.",
        GameType.Arena1v1 => "1v1 arenas use matchmaking + rooms + entity state sync.",
        GameType.PartyGame => "Party games shine with lobbies, ready checks, and rooms.",
        GameType.LobbyBased => "Lobby-based games center on lobbies + rooms.",
        GameType.RealtimeAction => "Realtime action uses matchmaking + rooms + state sync.",
        GameType.PersistentProgression => "Progression games lean on persistence + rooms.",
        _ => "Here's a recommended setup to get your multiplayer running.",
    };

    private static string ExampleFor(GameType gameType, bool wantsMatchmaking, bool wantsLobby) => gameType switch
    {
        GameType.Arena1v1 => "1v1 Matchmaking Arena",
        GameType.RealtimeAction => "Simple State Sync Demo",
        GameType.PartyGame or GameType.LobbyBased => "Lobby + Ready Check",
        GameType.PersistentProgression => "Persistent Player Profile",
        GameType.TurnBased => "Realtime Chat Room",
        _ when wantsMatchmaking => "1v1 Matchmaking Arena",
        _ when wantsLobby => "Lobby + Ready Check",
        _ => "Realtime Chat Room",
    };
}
