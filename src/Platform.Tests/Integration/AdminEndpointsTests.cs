using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Admin;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class AdminEndpointsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public AdminEndpointsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Projects_list_includes_created_project()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();
        var created = await (await http.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Admin Listed")))
            .Content.ReadFromJsonAsync<CreateProjectResponse>();

        var response = await http.GetFromJsonAsync<AdminProjectsResponse>("/api/admin/projects");

        Assert.Contains(response!.Projects, p => p.Id == created!.Id && p.Name == "Admin Listed");
    }

    [Fact]
    public async Task Project_details_returns_404_for_unknown()
    {
        var http = _factory.CreateClient();

        var response = await http.GetAsync("/api/admin/projects/proj_unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Realtime_reflects_an_active_connection()
    {
        var key = await _factory.CreateProjectKeyAsync();
        var http = _factory.CreateClient();

        using var socket = await _factory.ConnectReadyAsync(key);

        AdminRealtimeResponse? realtime = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            realtime = await http.GetFromJsonAsync<AdminRealtimeResponse>("/api/admin/realtime");
            if (realtime!.ActiveConnections >= 1)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.True(realtime!.ActiveConnections >= 1);
        Assert.Contains(realtime.Connections, c => !string.IsNullOrEmpty(c.PlayerId));
    }

    [Fact]
    public async Task Sessions_records_a_connection()
    {
        var key = await _factory.CreateProjectKeyAsync();
        var http = _factory.CreateClient();

        using var socket = await _factory.ConnectReadyAsync(key);

        AdminSessionsResponse? sessions = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            sessions = await http.GetFromJsonAsync<AdminSessionsResponse>("/api/admin/sessions");
            if (sessions!.Sessions.Count >= 1)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.NotEmpty(sessions!.Sessions);
    }

    [Fact]
    public async Task Logs_endpoint_returns_records()
    {
        var http = await _factory.CreateAuthenticatedClientAsync();

        // Trigger some logging activity.
        await http.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Log Maker"));

        var logs = await http.GetFromJsonAsync<AdminLogsResponse>("/api/admin/logs");

        Assert.NotNull(logs);
        Assert.NotEmpty(logs!.Logs);
    }
}
