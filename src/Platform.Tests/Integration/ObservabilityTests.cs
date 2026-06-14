using System.Net.Http.Json;
using Platform.Contracts.Admin;
using Platform.Contracts.Realtime;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class ObservabilityTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public ObservabilityTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Metrics_endpoint_reflects_connections_and_messages()
    {
        var key = await _factory.CreateProjectKeyAsync();
        var http = _factory.CreateClient();

        using var socket = await _factory.ConnectReadyAsync(key);
        for (var i = 0; i < 3; i++)
        {
            await socket.SendMessageAsync(RealtimeMessageTypes.Ping, $"p{i}", null);
            await socket.ReceiveEnvelopeAsync(); // pong
        }

        MetricsSnapshot? metrics = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            metrics = await http.GetFromJsonAsync<MetricsSnapshot>("/api/admin/metrics");
            if (metrics!.MessagesReceived >= 3 && metrics.ActiveConnections >= 1)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.True(metrics!.ConnectionsOpened >= 1, "expected at least one connection opened");
        Assert.True(metrics.MessagesReceived >= 3, "expected at least three messages received");
    }

    [Fact]
    public async Task Response_includes_a_correlation_id_header()
    {
        var http = _factory.CreateClient();

        var response = await http.GetAsync("/health");

        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Provided_correlation_id_is_echoed_back()
    {
        var http = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "corr-abc-123");

        var response = await http.SendAsync(request);

        Assert.Equal("corr-abc-123", response.Headers.GetValues("X-Correlation-Id").First());
    }
}
