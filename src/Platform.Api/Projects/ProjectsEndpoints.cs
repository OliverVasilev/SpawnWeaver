using Platform.Api.Auth;
using Platform.Api.Onboarding;
using Platform.Application.Onboarding;
using Platform.Application.Projects;
using Platform.Contracts.Http;
using Platform.Domain.Projects;

namespace Platform.Api.Projects;

/// <summary>
/// Project registration endpoints (control plane). Creating a project captures the
/// onboarding profile (game type, multiplayer mode, persistence needs), attaches the
/// signed-in developer's organization when present, and returns a tailored setup plan.
/// The plaintext secret key is returned exactly once and is never retrievable again.
/// </summary>
public static class ProjectsEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects", async (
            CreateProjectRequest request, ProjectService projects, CurrentUser current, CancellationToken ct) =>
        {
            // Creating a project requires a signed-in developer; it is attached to their workspace.
            if (!current.IsAuthenticated || current.UserId is null)
            {
                return Results.Json(
                    new { message = "Sign in to create a project." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["Name is required."],
                });
            }

            if (name.Length > Project.MaxNameLength)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = [$"Name must be at most {Project.MaxNameLength} characters."],
                });
            }

            var organization = await current.GetOrganizationAsync(ct);
            if (organization is null)
            {
                return Results.Json(
                    new { message = "Your account has no workspace yet." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var gameType = OnboardingMapping.ParseGameType(request.GameType);
            var multiplayerMode = OnboardingMapping.ParseMultiplayerMode(request.MultiplayerMode);
            var persistence = OnboardingMapping.ParsePersistenceFeatures(request.PersistenceFeatures);
            var environment = OnboardingMapping.ParseEnvironment(request.Environment);

            var command = new CreateProjectCommand(
                name, organization.Id, gameType, multiplayerMode, persistence, request.TargetPlatform, environment);

            var result = await projects.CreateAsync(command, ct);
            var project = result.Project;

            var plan = SetupRecommendation.Build(project.GameType, project.MultiplayerMode, project.PersistenceFeatures);

            var response = new CreateProjectResponse(
                project.Id,
                project.Name,
                project.PublicKey,
                result.PlaintextSecretKey,
                project.CreatedAtUtc,
                project.Slug,
                project.OrganizationId,
                project.GameType.ToString(),
                project.MultiplayerMode.ToString(),
                OnboardingMapping.FeatureNames(project.PersistenceFeatures),
                project.Environment.ToString(),
                OnboardingMapping.ToDto(plan));

            return Results.Created($"/api/projects/{project.Id}", response);
        }).WithTags("Projects");

        app.MapGet("/api/projects/{projectId}", async (string projectId, ProjectService projects, CancellationToken ct) =>
        {
            var project = await projects.GetAsync(projectId, ct);
            if (project is null)
            {
                return Results.NotFound();
            }

            var response = new ProjectResponse(
                project.Id,
                project.Name,
                project.PublicKey,
                project.IsActive,
                project.CreatedAtUtc,
                project.Slug,
                project.OrganizationId,
                project.GameType.ToString(),
                project.MultiplayerMode.ToString(),
                OnboardingMapping.FeatureNames(project.PersistenceFeatures),
                project.Environment.ToString());

            return Results.Ok(response);
        }).WithTags("Projects");

        // Regenerate the secret key. Requires the signed-in owner; returns the new plaintext once.
        app.MapPost("/api/projects/{projectId}/keys/secret", async (
            string projectId, ProjectService projects, CurrentUser current, CancellationToken ct) =>
        {
            var organization = await RequireOwnerOrgAsync(current, ct);
            if (organization is null)
            {
                return Results.Json(new { message = "Sign in to manage this project." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var secretKey = await projects.RotateSecretKeyAsync(projectId, organization.Id, ct);
            return secretKey is null
                ? Results.NotFound()
                : Results.Ok(new { secretKey });
        }).WithTags("Projects");

        // Rotate the public key. Disruptive — shipped clients using the old key stop connecting.
        app.MapPost("/api/projects/{projectId}/keys/public", async (
            string projectId, ProjectService projects, CurrentUser current, CancellationToken ct) =>
        {
            var organization = await RequireOwnerOrgAsync(current, ct);
            if (organization is null)
            {
                return Results.Json(new { message = "Sign in to manage this project." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var publicKey = await projects.RotatePublicKeyAsync(projectId, organization.Id, ct);
            return publicKey is null
                ? Results.NotFound()
                : Results.Ok(new { publicKey });
        }).WithTags("Projects");

        app.MapGet("/api/onboarding/options", () => Results.Ok(OnboardingMapping.Options()))
            .WithTags("Onboarding");

        return app;
    }

    /// <summary>Returns the signed-in user's organization, or null if not authenticated.</summary>
    private static async Task<Platform.Domain.Accounts.Organization?> RequireOwnerOrgAsync(
        CurrentUser current, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is null)
        {
            return null;
        }

        return await current.GetOrganizationAsync(ct);
    }
}
