using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class ProjectOnboardingTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public ProjectOnboardingTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_project_with_onboarding_echoes_profile_and_returns_recommended_setup()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var request = new CreateProjectRequest(
            "Duel Game",
            GameType: "Arena1v1",
            MultiplayerMode: "MatchmakingAndRooms",
            PersistenceFeatures: ["PlayerProfile"],
            Environment: "Development");

        var response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.NotNull(created);
        Assert.Equal("Arena1v1", created!.GameType);
        Assert.Equal("MatchmakingAndRooms", created.MultiplayerMode);
        Assert.Contains("PlayerProfile", created.PersistenceFeatures);
        Assert.Equal("duel-game", created.Slug);

        Assert.NotNull(created.RecommendedSetup);
        Assert.Equal("1v1 Matchmaking Arena", created.RecommendedSetup.ExampleProject);
        Assert.Contains(created.RecommendedSetup.Steps,
            s => s.Title.Contains("matchmaking", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Name_only_creation_works_and_attaches_the_workspace()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Plain Project"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.Equal("Unspecified", created!.GameType);
        Assert.NotNull(created.OrganizationId);
        Assert.NotNull(created.RecommendedSetup);
    }

    [Fact]
    public async Task Authenticated_creation_attaches_the_developers_organization()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");

        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Owned Project"));
        var created = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();

        Assert.Equal(account!.OrganizationId, created!.OrganizationId);
    }

    [Fact]
    public async Task Onboarding_options_are_served()
    {
        var client = _factory.CreateClient();

        var options = await client.GetFromJsonAsync<OnboardingOptionsResponse>("/api/onboarding/options");

        Assert.NotNull(options);
        Assert.Contains(options!.GameTypes, o => o.Value == "Arena1v1");
        Assert.Contains(options.MultiplayerModes, o => o.Value == "MatchmakingAndRooms");
        Assert.Contains(options.PersistenceFeatures, o => o.Value == "PlayerProfile");
    }

    [Fact]
    public async Task Get_project_returns_onboarding_fields()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/projects",
            new CreateProjectRequest("Lookup Onboarding", GameType: "TurnBased"));
        var created = await create.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var fetched = await client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{created!.Id}");

        Assert.Equal("TurnBased", fetched!.GameType);
        Assert.Equal(created.Slug, fetched.Slug);
    }
}
