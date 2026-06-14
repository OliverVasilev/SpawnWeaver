namespace Platform.Contracts.Http;

/// <summary>Request body for <c>POST /api/feedback</c>.</summary>
public sealed record FeedbackRequest(string? Email, string Message);

/// <summary>Response for a submitted feedback item.</summary>
public sealed record FeedbackResponse(string Id, DateTimeOffset CreatedAtUtc);

/// <summary>A feedback item in the admin list.</summary>
public sealed record FeedbackItem(string Id, string? Email, string Message, DateTimeOffset CreatedAtUtc);

/// <summary>Response for <c>GET /api/admin/feedback</c>.</summary>
public sealed record FeedbackListResponse(IReadOnlyList<FeedbackItem> Items);
