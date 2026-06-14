using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class ProjectEndpointsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public ProjectEndpointsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_project_returns_201_with_keys_and_secret_once()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("My Game"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        Assert.NotNull(created);
        Assert.StartsWith("proj_", created!.Id);
        Assert.Equal("My Game", created.Name);
        Assert.StartsWith("pk_", created.PublicKey);
        Assert.StartsWith("sk_", created.SecretKey);
        Assert.Equal($"/api/projects/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Create_project_without_signing_in_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Anon Game"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_project_returns_project_without_secret()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Lookup Game"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>();

        var getResponse = await client.GetAsync($"/api/projects/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.PublicKey, fetched.PublicKey);
        Assert.True(fetched.IsActive);

        // The GET contract has no secret field at all — the secret is never retrievable again.
        var rawBody = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(created.SecretKey, rawBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_unknown_project_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/projects/proj_does_not_exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_project_with_blank_name_returns_400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Each_created_project_gets_distinct_keys()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var first = await (await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("A")))
            .Content.ReadFromJsonAsync<CreateProjectResponse>();
        var second = await (await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("B")))
            .Content.ReadFromJsonAsync<CreateProjectResponse>();

        Assert.NotEqual(first!.Id, second!.Id);
        Assert.NotEqual(first.PublicKey, second.PublicKey);
        Assert.NotEqual(first.SecretKey, second.SecretKey);
    }
}
