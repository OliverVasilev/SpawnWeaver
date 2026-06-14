using Platform.Application.Projects;
using Platform.Application.Security;
using Platform.Application.Storage;
using Platform.Contracts.Http;

namespace Platform.Api.Storage;

/// <summary>
/// Project-scoped player key-value storage. All operations require the project's
/// <em>secret</em> key as a bearer token (<c>Authorization: Bearer sk_…</c>).
/// </summary>
public static class StorageEndpoints
{
    public static IEndpointRouteBuilder MapStorageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/storage/{projectId}/players/{playerId}").WithTags("Storage");

        group.MapPut("/keys/{key}", SaveAsync);
        group.MapGet("/keys/{key}", LoadAsync);
        group.MapDelete("/keys/{key}", DeleteAsync);
        group.MapGet("/keys", ListKeysAsync);

        return app;
    }

    private static async Task<IResult> SaveAsync(
        string projectId,
        string playerId,
        string key,
        HttpContext context,
        ProjectService projects,
        IApiKeyHasher hasher,
        PlayerStorageService storage)
    {
        if (!await AuthorizeAsync(context, projectId, projects, hasher))
        {
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(context.Request.Body);
        var value = await reader.ReadToEndAsync(context.RequestAborted);

        var result = await storage.SaveAsync(projectId, playerId, key, value, context.RequestAborted);
        return result.Status switch
        {
            StorageSaveStatus.Saved =>
                Results.Ok(new StorageSavedResponse(result.Entry!.Key, result.Entry.UpdatedAtUtc)),
            StorageSaveStatus.InvalidKey =>
                Results.ValidationProblem(new Dictionary<string, string[]> { ["key"] = ["Invalid or too-long key."] }),
            StorageSaveStatus.ValueTooLarge =>
                Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            StorageSaveStatus.QuotaExceeded =>
                Results.StatusCode(StatusCodes.Status409Conflict),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> LoadAsync(
        string projectId,
        string playerId,
        string key,
        HttpContext context,
        ProjectService projects,
        IApiKeyHasher hasher,
        PlayerStorageService storage)
    {
        if (!await AuthorizeAsync(context, projectId, projects, hasher))
        {
            return Results.Unauthorized();
        }

        var entry = await storage.GetAsync(projectId, playerId, key, context.RequestAborted);
        return entry is null
            ? Results.NotFound()
            : Results.Ok(new StorageValueResponse(entry.Key, entry.Value, entry.UpdatedAtUtc));
    }

    private static async Task<IResult> DeleteAsync(
        string projectId,
        string playerId,
        string key,
        HttpContext context,
        ProjectService projects,
        IApiKeyHasher hasher,
        PlayerStorageService storage)
    {
        if (!await AuthorizeAsync(context, projectId, projects, hasher))
        {
            return Results.Unauthorized();
        }

        var deleted = await storage.DeleteAsync(projectId, playerId, key, context.RequestAborted);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListKeysAsync(
        string projectId,
        string playerId,
        HttpContext context,
        ProjectService projects,
        IApiKeyHasher hasher,
        PlayerStorageService storage)
    {
        if (!await AuthorizeAsync(context, projectId, projects, hasher))
        {
            return Results.Unauthorized();
        }

        var keys = await storage.ListKeysAsync(projectId, playerId, context.RequestAborted);
        return Results.Ok(new StorageKeysResponse(keys));
    }

    private static async Task<bool> AuthorizeAsync(
        HttpContext context, string projectId, ProjectService projects, IApiKeyHasher hasher)
    {
        var secret = ExtractBearerToken(context);
        if (secret is null)
        {
            return false;
        }

        var project = await projects.GetAsync(projectId, context.RequestAborted);
        return project is { IsActive: true } && hasher.Verify(secret, project.SecretKeyHash);
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }
}
