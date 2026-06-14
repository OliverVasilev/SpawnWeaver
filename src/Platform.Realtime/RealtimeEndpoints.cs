using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions;
using Platform.Application.Projects;
using Platform.Application.Security;
using Platform.Contracts.Realtime;
using Platform.Realtime.Connections;
using Platform.Realtime.Rooms;
using Platform.Realtime.Transport;

namespace Platform.Realtime;

public static class RealtimeEndpoints
{
    /// <summary>
    /// Maps the realtime gateway endpoints:
    /// <list type="bullet">
    ///   <item><c>GET /connect</c> — WebSocket handshake (requires a valid <c>projectKey</c>).</item>
    ///   <item><c>GET /connect/stats</c> — active connection count (diagnostics).</item>
    /// </list>
    /// </summary>
    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/connect", ConnectAsync);

        app.MapGet("/connect/stats", (ConnectionManager connections, RoomManager rooms) =>
            Results.Ok(new RealtimeStatsResponse(connections.Count, rooms.RoomCount)));

        return app;
    }

    private static async Task ConnectAsync(
        HttpContext context,
        ProjectService projects,
        RealtimeConnectionHandler handler,
        IPlayerTokenService tokens,
        IIdGenerator ids,
        ConnectionManager connections,
        IOptions<RealtimeOptions> options,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("Platform.Realtime.Connect");

        if (!context.WebSockets.IsWebSocketRequest)
        {
            RealtimeLog.Rejected(logger, "not a WebSocket request");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Optional Origin allowlist (CSWSH protection for browser clients).
        if (!IsOriginAllowed(context, configuration))
        {
            RealtimeLog.Rejected(logger, "origin not allowed");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var projectKey = context.Request.Query["projectKey"].ToString();
        if (string.IsNullOrWhiteSpace(projectKey))
        {
            RealtimeLog.Rejected(logger, "missing projectKey");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var project = await projects.GetByPublicKeyAsync(projectKey, context.RequestAborted);
        if (project is null || !project.IsActive)
        {
            RealtimeLog.Rejected(logger, "unknown or inactive projectKey");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Per-project connection limit.
        var maxPerProject = options.Value.MaxConnectionsPerProject;
        if (maxPerProject > 0 && connections.CountForProject(project.Id) >= maxPerProject)
        {
            RealtimeLog.Rejected(logger, $"connection limit ({maxPerProject}) reached for project {project.Id}");
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        // Resume an existing player from a token, or mint a new anonymous player.
        var providedToken = context.Request.Query["playerToken"].ToString();
        string playerId;
        if (!string.IsNullOrEmpty(providedToken))
        {
            var validation = tokens.Validate(providedToken);
            if (!validation.IsValid || !string.Equals(validation.ProjectId, project.Id, StringComparison.Ordinal))
            {
                RealtimeLog.Rejected(logger, validation.IsExpired ? "expired player token" : "invalid player token");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            playerId = validation.PlayerId!;
        }
        else
        {
            playerId = ids.NewId("player");
        }

        // Issue a fresh token each connect (sliding expiration); returned in the welcome message.
        var issued = tokens.Issue(project.Id, playerId);
        var identity = new PlayerIdentity(playerId, issued.Value, issued.ExpiresAtUtc);

        // Diagnostics metadata for the connection inspector (Milestone 22): client IP plus the
        // SDK/engine versions the client optionally reports as query params.
        var metadata = new ConnectionMetadata(
            context.Connection.RemoteIpAddress?.ToString(),
            NullIfBlank(context.Request.Query["sdkVersion"].ToString()),
            NullIfBlank(context.Request.Query["engine"].ToString()));

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(socket, project, identity, metadata, context.RequestAborted);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// True unless <c>Security:AllowedOrigins</c> is configured and the request's Origin
    /// header is not in the comma-separated allowlist. Default (unset) allows all origins,
    /// which is fine for native game clients that send no Origin.
    /// </summary>
    private static bool IsOriginAllowed(HttpContext context, IConfiguration configuration)
    {
        var allowedOrigins = configuration["Security:AllowedOrigins"];
        if (string.IsNullOrWhiteSpace(allowedOrigins))
        {
            return true;
        }

        var origin = context.Request.Headers.Origin.ToString();
        var allowed = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowed.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}
