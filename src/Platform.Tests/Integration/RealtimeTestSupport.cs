using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Platform.Contracts.Http;
using Platform.Contracts.Realtime;

namespace Platform.Tests.Integration;

/// <summary>Shared helpers for driving the realtime gateway over a TestServer WebSocket.</summary>
internal static class RealtimeTestSupport
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<string> CreateProjectKeyAsync(
        this WebApplicationFactory<Program> factory, string name = "Realtime Game")
    {
        // Project creation requires a signed-in developer, so sign up a throwaway account first.
        var http = await factory.CreateAuthenticatedClientAsync();
        var response = await http.PostAsJsonAsync("/api/projects", new CreateProjectRequest(name));
        var created = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
        return created!.PublicKey;
    }

    /// <summary>Signs up a throwaway account on the given client (sets the auth cookie).</summary>
    public static async Task SignUpAsync(this HttpClient http, string? email = null)
    {
        var response = await http.PostAsJsonAsync("/api/auth/signup",
            new SignUpRequest(email ?? $"t-{Guid.NewGuid():N}@example.com", "Tester", "supersecret123"));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>A client with an authenticated (cookie-backed) throwaway account.</summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this WebApplicationFactory<Program> factory)
    {
        var http = factory.CreateClient();
        await http.SignUpAsync();
        return http;
    }

    public static Uri BuildConnectUri(this WebApplicationFactory<Program> factory, string? publicKey, string? playerToken = null)
    {
        var query = new List<string>();
        if (publicKey is not null)
        {
            query.Add($"projectKey={Uri.EscapeDataString(publicKey)}");
        }

        if (playerToken is not null)
        {
            query.Add($"playerToken={Uri.EscapeDataString(playerToken)}");
        }

        var builder = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = "/connect" };
        if (query.Count > 0)
        {
            builder.Query = string.Join("&", query);
        }

        return builder.Uri;
    }

    public static async Task<WebSocket> ConnectWithTokenAsync(
        this WebApplicationFactory<Program> factory, string publicKey, string playerToken)
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        return await wsClient.ConnectAsync(factory.BuildConnectUri(publicKey, playerToken), CancellationToken.None);
    }

    public static async Task<WebSocket> ConnectAsync(this WebApplicationFactory<Program> factory, string publicKey)
    {
        var wsClient = factory.Server.CreateWebSocketClient();
        return await wsClient.ConnectAsync(factory.BuildConnectUri(publicKey), CancellationToken.None);
    }

    /// <summary>Connects and consumes the welcome envelope.</summary>
    public static async Task<WebSocket> ConnectReadyAsync(this WebApplicationFactory<Program> factory, string publicKey)
    {
        var socket = await factory.ConnectAsync(publicKey);
        await socket.ReceiveEnvelopeAsync();
        return socket;
    }

    public static async Task<RealtimeEnvelope> ReceiveEnvelopeAsync(this WebSocket socket)
    {
        var buffer = new byte[8192];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await socket.ReceiveAsync(buffer, cts.Token);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        return JsonSerializer.Deserialize<RealtimeEnvelope>(json, JsonOptions)!;
    }

    public static async Task SendTextAsync(this WebSocket socket, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cts.Token);
    }

    /// <summary>Serializes and sends a protocol envelope.</summary>
    public static Task SendMessageAsync(this WebSocket socket, string type, string? requestId, object? payload)
    {
        var json = JsonSerializer.Serialize(new { type, requestId, payload }, JsonOptions);
        return socket.SendTextAsync(json);
    }

    public static T? DeserializePayload<T>(this RealtimeEnvelope envelope)
        => envelope.Payload!.Value.Deserialize<T>(JsonOptions);

    public static async Task<int> PollConnectionCountAsync(this WebApplicationFactory<Program> factory, int expected)
    {
        var client = factory.CreateClient();
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var stats = await client.GetFromJsonAsync<RealtimeStatsResponse>("/connect/stats");
            if (stats!.ActiveConnections == expected)
            {
                return stats.ActiveConnections;
            }

            await Task.Delay(50);
        }

        var final = await client.GetFromJsonAsync<RealtimeStatsResponse>("/connect/stats");
        return final!.ActiveConnections;
    }
}
