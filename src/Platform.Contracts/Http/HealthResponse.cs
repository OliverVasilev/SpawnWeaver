namespace Platform.Contracts.Http;

/// <summary>
/// Response body for the <c>/health</c> endpoint.
/// Public HTTP contract — lives in Platform.Contracts so SDK and tests share it.
/// </summary>
public sealed record HealthResponse(string Status, string Service, string Version);
