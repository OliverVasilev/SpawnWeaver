using Platform.Application.Onboarding;
using Platform.Domain.Accounts;
using Xunit;

namespace Platform.Tests.Unit;

public sealed class SetupRecommendationTests
{
    [Fact]
    public void Arena_1v1_recommends_matchmaking_state_sync_and_the_arena_example()
    {
        var plan = SetupRecommendation.Build(GameType.Arena1v1, MultiplayerMode.Unspecified, []);

        Assert.Equal("1v1 Matchmaking Arena", plan.ExampleProject);
        Assert.Contains(plan.Steps, s => s.Title.Contains("matchmaking", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Steps, s => s.Title.Contains("entity state sync", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Steps, s => s.Title.Contains("Install the Godot SDK", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Turn_based_recommends_room_state_and_does_not_recommend_matchmaking()
    {
        var plan = SetupRecommendation.Build(GameType.TurnBased, MultiplayerMode.RoomsOnly, []);

        Assert.Contains(plan.Steps, s => s.Title.Contains("room state", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(plan.Steps, s => s.Title.Contains("matchmaking", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recommendation_differs_by_game_type()
    {
        var arena = SetupRecommendation.Build(GameType.Arena1v1, MultiplayerMode.Unspecified, []);
        var party = SetupRecommendation.Build(GameType.PartyGame, MultiplayerMode.Unspecified, []);

        Assert.NotEqual(arena.ExampleProject, party.ExampleProject);
        Assert.Contains(party.Steps, s => s.Title.Contains("lobbies", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Selected_persistence_features_appear_as_completed_steps()
    {
        var plan = SetupRecommendation.Build(
            GameType.PersistentProgression,
            MultiplayerMode.PersistenceOnly,
            [PersistenceFeature.PlayerProfile, PersistenceFeature.Progression]);

        Assert.Contains(plan.Steps, s => s.Done && s.Title.Contains("player profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Steps, s => s.Done && s.Title.Contains("progression", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Multiplayer_mode_can_drive_matchmaking_even_for_unspecified_game_type()
    {
        var plan = SetupRecommendation.Build(GameType.Unspecified, MultiplayerMode.MatchmakingAndRooms, []);

        Assert.Contains(plan.Steps, s => s.Title.Contains("matchmaking", StringComparison.OrdinalIgnoreCase));
    }
}
