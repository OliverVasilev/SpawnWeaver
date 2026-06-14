namespace Platform.Contracts.Http;

/// <summary>A recommended setup step shown after project creation (Milestone 19.7).</summary>
public sealed record SetupStepDto(string Title, bool Done);

/// <summary>The tailored recommended setup path for a newly created project.</summary>
public sealed record SetupPlanDto(string Headline, string ExampleProject, IReadOnlyList<SetupStepDto> Steps);

/// <summary>A selectable option (enum value + human label) for the onboarding wizard.</summary>
public sealed record OnboardingOption(string Value, string Label);

/// <summary>
/// Response for <c>GET /api/onboarding/options</c> — the choices that drive the
/// project creation wizard (game type, multiplayer mode, persistence features).
/// </summary>
public sealed record OnboardingOptionsResponse(
    IReadOnlyList<OnboardingOption> GameTypes,
    IReadOnlyList<OnboardingOption> MultiplayerModes,
    IReadOnlyList<OnboardingOption> PersistenceFeatures);
