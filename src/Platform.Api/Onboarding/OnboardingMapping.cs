using Platform.Application.Onboarding;
using Platform.Contracts.Http;
using Platform.Domain.Accounts;

namespace Platform.Api.Onboarding;

/// <summary>
/// Maps between onboarding wire strings (enum names) and domain enums, builds the
/// wizard option lists, and projects a <see cref="SetupPlan"/> to its DTO.
/// </summary>
public static class OnboardingMapping
{
    public static GameType ParseGameType(string? value)
        => Enum.TryParse<GameType>(value, ignoreCase: true, out var parsed) ? parsed : GameType.Unspecified;

    public static MultiplayerMode ParseMultiplayerMode(string? value)
        => Enum.TryParse<MultiplayerMode>(value, ignoreCase: true, out var parsed) ? parsed : MultiplayerMode.Unspecified;

    public static ProjectEnvironment ParseEnvironment(string? value)
        => Enum.TryParse<ProjectEnvironment>(value, ignoreCase: true, out var parsed) ? parsed : ProjectEnvironment.Development;

    /// <summary>Parses persistence feature names, ignoring blanks and the "none" sentinel.</summary>
    public static IReadOnlyList<PersistenceFeature> ParsePersistenceFeatures(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var result = new List<PersistenceFeature>();
        foreach (var value in values)
        {
            if (Enum.TryParse<PersistenceFeature>(value, ignoreCase: true, out var feature) && !result.Contains(feature))
            {
                result.Add(feature);
            }
        }

        return result;
    }

    public static IReadOnlyList<string> FeatureNames(IReadOnlyList<PersistenceFeature> features)
        => features.Select(f => f.ToString()).ToArray();

    public static SetupPlanDto ToDto(SetupPlan plan)
        => new(
            plan.Headline,
            plan.ExampleProject,
            plan.Steps.Select(s => new SetupStepDto(s.Title, s.Done)).ToArray());

    public static OnboardingOptionsResponse Options() => new(
        GameTypes:
        [
            new("TurnBased", "Turn-based"),
            new("CasualCoop", "Casual co-op"),
            new("Arena1v1", "1v1 arena"),
            new("PartyGame", "Small party game"),
            new("LobbyBased", "Lobby-based game"),
            new("RealtimeAction", "Realtime action"),
            new("PersistentProgression", "Persistent progression game"),
            new("Other", "Other"),
        ],
        MultiplayerModes:
        [
            new("RoomsOnly", "Rooms only"),
            new("LobbiesAndRooms", "Lobbies + rooms"),
            new("MatchmakingAndRooms", "Matchmaking + rooms"),
            new("EventRelayOnly", "Event relay only"),
            new("StateSync", "State sync"),
            new("PersistenceOnly", "Persistence only"),
            new("NotSure", "Not sure"),
        ],
        PersistenceFeatures:
        [
            new("PlayerProfile", "Player profile"),
            new("Inventory", "Inventory"),
            new("Progression", "Progression / XP"),
            new("MatchHistory", "Match history"),
            new("SaveSlots", "Save slots"),
            new("CustomKeyValue", "Custom key-value data"),
        ]);
}
