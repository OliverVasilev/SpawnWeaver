using Platform.Application.Feedback;
using Platform.Application.Projects;
using Platform.Contracts.Admin;
using Platform.Contracts.Http;
using Platform.Infrastructure.Observability;
using Platform.Realtime.Diagnostics;

namespace Platform.Api.Admin;

/// <summary>
/// Read-only admin/diagnostics API backing the dashboard. Open by default for local/internal
/// use; set <c>Admin:ApiKey</c> to require <c>Authorization: Bearer &lt;key&gt;</c>.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        group.MapGet("/projects", async (ProjectService projects, HttpContext context, IConfiguration config) =>
        {
            if (!Authorized(context, config))
            {
                return Results.Unauthorized();
            }

            var list = await projects.ListAsync(100, context.RequestAborted);
            var summaries = list
                .Select(p => new AdminProjectSummary(p.Id, p.Name, p.IsActive, p.CreatedAtUtc))
                .ToArray();
            return Results.Ok(new AdminProjectsResponse(summaries));
        });

        group.MapGet("/projects/{id}", async (string id, ProjectService projects, HttpContext context, IConfiguration config) =>
        {
            if (!Authorized(context, config))
            {
                return Results.Unauthorized();
            }

            var project = await projects.GetAsync(id, context.RequestAborted);
            return project is null
                ? Results.NotFound()
                : Results.Ok(new AdminProjectSummary(project.Id, project.Name, project.IsActive, project.CreatedAtUtc));
        });

        group.MapGet("/realtime", (RealtimeDiagnostics diagnostics, HttpContext context, IConfiguration config) =>
            Authorized(context, config)
                ? Results.Ok(new AdminRealtimeResponse(
                    diagnostics.ActiveConnections, diagnostics.ActiveRooms,
                    diagnostics.GetConnections(), diagnostics.GetRooms()))
                : Results.Unauthorized());

        group.MapGet("/sessions", (RealtimeDiagnostics diagnostics, HttpContext context, IConfiguration config) =>
            Authorized(context, config)
                ? Results.Ok(new AdminSessionsResponse(diagnostics.GetRecentSessions()))
                : Results.Unauthorized());

        group.MapGet("/sessions/{connectionId}", (string connectionId, RealtimeDiagnostics diagnostics, HttpContext context, IConfiguration config) =>
        {
            if (!Authorized(context, config))
            {
                return Results.Unauthorized();
            }

            var detail = diagnostics.GetSessionDetail(connectionId);
            return detail is null ? Results.NotFound() : Results.Ok(new AdminSessionDetailResponse(detail));
        });

        group.MapGet("/errors", (RealtimeDiagnostics diagnostics, HttpContext context, IConfiguration config) =>
            Authorized(context, config)
                ? Results.Ok(new AdminErrorsResponse(diagnostics.GetErrors()))
                : Results.Unauthorized());

        group.MapGet("/matchmaking", (RealtimeDiagnostics diagnostics, HttpContext context, IConfiguration config) =>
            Authorized(context, config)
                ? Results.Ok(diagnostics.GetMatchmaking())
                : Results.Unauthorized());

        group.MapGet("/rooms/{roomId}", (string roomId, RealtimeDiagnostics diagnostics, HttpContext context, IConfiguration config) =>
        {
            if (!Authorized(context, config))
            {
                return Results.Unauthorized();
            }

            var detail = diagnostics.GetRoomDetail(roomId);
            return detail is null ? Results.NotFound() : Results.Ok(new AdminRoomDetailResponse(detail));
        });

        group.MapGet("/logs", (RecentLogStore logs, string? level, HttpContext context, IConfiguration config) =>
            Authorized(context, config)
                ? Results.Ok(new AdminLogsResponse(logs.GetRecent(level, 200)))
                : Results.Unauthorized());

        group.MapGet("/metrics", (RealtimeMetrics metrics, HttpContext context, IConfiguration config) =>
            Authorized(context, config)
                ? Results.Ok(metrics.GetSnapshot())
                : Results.Unauthorized());

        group.MapGet("/feedback", async (FeedbackService feedback, HttpContext context, IConfiguration config) =>
        {
            if (!Authorized(context, config))
            {
                return Results.Unauthorized();
            }

            var items = (await feedback.ListAsync(200, context.RequestAborted))
                .Select(f => new FeedbackItem(f.Id, f.Email, f.Message, f.CreatedAtUtc))
                .ToArray();
            return Results.Ok(new FeedbackListResponse(items));
        });

        return app;
    }

    private static bool Authorized(HttpContext context, IConfiguration config)
    {
        var key = config["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            return true; // open by default; set Admin:ApiKey to require a token.
        }

        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && string.Equals(header[prefix.Length..].Trim(), key, StringComparison.Ordinal);
    }
}
