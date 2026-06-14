using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public HealthEndpointTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_returns_ok_status_payload()
    {
        var client = _factory.CreateClient();

        var payload = await client.GetFromJsonAsync<HealthResponse>("/health");

        Assert.NotNull(payload);
        Assert.Equal("ok", payload!.Status);
        Assert.Equal("Platform.Api", payload.Service);
    }
}
