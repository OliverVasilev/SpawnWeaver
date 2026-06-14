using Platform.Application.Feedback;
using Platform.Contracts.Http;
using Platform.Domain.Feedback;

namespace Platform.Api.Feedback;

/// <summary>Alpha feedback: an open submission endpoint and an admin listing.</summary>
public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/feedback", async (FeedbackRequest request, FeedbackService feedback, CancellationToken ct) =>
        {
            var message = request.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["message"] = ["Message is required."],
                });
            }

            if (message.Length > FeedbackEntry.MaxMessageLength)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["message"] = [$"Message must be at most {FeedbackEntry.MaxMessageLength} characters."],
                });
            }

            var entry = await feedback.SubmitAsync(request.Email, message, ct);
            return Results.Ok(new FeedbackResponse(entry.Id, entry.CreatedAtUtc));
        }).WithTags("Feedback");

        return app;
    }
}
