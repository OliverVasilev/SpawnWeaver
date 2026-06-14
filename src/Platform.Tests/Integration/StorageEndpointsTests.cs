using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class StorageEndpointsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public StorageEndpointsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    private async Task<(string ProjectId, string Secret)> CreateProjectAsync(string name = "Storage Game")
    {
        var http = await _factory.CreateAuthenticatedClientAsync();
        var response = await http.PostAsJsonAsync("/api/projects", new CreateProjectRequest(name));
        var created = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        return (created!.Id, created.SecretKey);
    }

    private HttpClient AuthedClient(string secret)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return client;
    }

    private static StringContent Body(string value) => new(value, Encoding.UTF8, "text/plain");

    [Fact]
    public async Task Save_then_load_returns_the_value()
    {
        var (projectId, secret) = await CreateProjectAsync();
        var client = AuthedClient(secret);

        var save = await client.PutAsync($"/api/storage/{projectId}/players/player_1/keys/score", Body("42"));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var load = await client.GetAsync($"/api/storage/{projectId}/players/player_1/keys/score");
        Assert.Equal(HttpStatusCode.OK, load.StatusCode);
        var value = await load.Content.ReadFromJsonAsync<StorageValueResponse>();
        Assert.Equal("42", value!.Value);
    }

    [Fact]
    public async Task Load_missing_key_returns_404()
    {
        var (projectId, secret) = await CreateProjectAsync();
        var client = AuthedClient(secret);

        var load = await client.GetAsync($"/api/storage/{projectId}/players/player_1/keys/nope");

        Assert.Equal(HttpStatusCode.NotFound, load.StatusCode);
    }

    [Fact]
    public async Task Save_overwrites_existing_value()
    {
        var (projectId, secret) = await CreateProjectAsync();
        var client = AuthedClient(secret);

        await client.PutAsync($"/api/storage/{projectId}/players/p/keys/k", Body("first"));
        await client.PutAsync($"/api/storage/{projectId}/players/p/keys/k", Body("second"));

        var value = await client.GetFromJsonAsync<StorageValueResponse>($"/api/storage/{projectId}/players/p/keys/k");
        Assert.Equal("second", value!.Value);
    }

    [Fact]
    public async Task Data_is_isolated_between_projects()
    {
        var (projectA, secretA) = await CreateProjectAsync("A");
        var (projectB, secretB) = await CreateProjectAsync("B");

        await AuthedClient(secretA).PutAsync($"/api/storage/{projectA}/players/p/keys/k", Body("a-data"));

        // Same player id + key under project B does not see A's data.
        var underB = await AuthedClient(secretB).GetAsync($"/api/storage/{projectB}/players/p/keys/k");
        Assert.Equal(HttpStatusCode.NotFound, underB.StatusCode);
    }

    [Fact]
    public async Task Wrong_secret_is_rejected()
    {
        var (projectA, _) = await CreateProjectAsync("A");
        var (_, secretB) = await CreateProjectAsync("B");

        // Project B's secret cannot access project A's storage.
        var response = await AuthedClient(secretB).GetAsync($"/api/storage/{projectA}/players/p/keys/k");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_authorization_is_rejected()
    {
        var (projectId, _) = await CreateProjectAsync();
        var client = _factory.CreateClient(); // no auth header

        var response = await client.GetAsync($"/api/storage/{projectId}/players/p/keys/k");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Oversized_value_is_rejected()
    {
        var (projectId, secret) = await CreateProjectAsync();
        var client = AuthedClient(secret);

        var big = new string('x', 64 * 1024 + 1); // exceeds the 64 KB default
        var response = await client.PutAsync($"/api/storage/{projectId}/players/p/keys/big", Body(big));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task List_and_delete_keys()
    {
        var (projectId, secret) = await CreateProjectAsync();
        var client = AuthedClient(secret);

        await client.PutAsync($"/api/storage/{projectId}/players/p/keys/a", Body("1"));
        await client.PutAsync($"/api/storage/{projectId}/players/p/keys/b", Body("2"));

        var keys = await client.GetFromJsonAsync<StorageKeysResponse>($"/api/storage/{projectId}/players/p/keys");
        Assert.Equal(2, keys!.Keys.Count);

        var delete = await client.DeleteAsync($"/api/storage/{projectId}/players/p/keys/a");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var keysAfter = await client.GetFromJsonAsync<StorageKeysResponse>($"/api/storage/{projectId}/players/p/keys");
        Assert.Single(keysAfter!.Keys);
    }
}
