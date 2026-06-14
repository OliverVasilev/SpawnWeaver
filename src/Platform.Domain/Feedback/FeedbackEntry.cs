namespace Platform.Domain.Feedback;

/// <summary>A piece of feedback submitted by an alpha tester.</summary>
public sealed class FeedbackEntry
{
    public const int MaxMessageLength = 4000;
    public const int MaxEmailLength = 256;

    public string Id { get; private set; }
    public string? Email { get; private set; }
    public string Message { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    // EF Core materialization.
    private FeedbackEntry()
    {
        Id = null!;
        Message = null!;
    }

    private FeedbackEntry(string id, string? email, string message, DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;
        Message = message;
        CreatedAtUtc = createdAtUtc;
    }

    public static FeedbackEntry Create(string id, string? email, string message, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var trimmedMessage = message.Trim();
        if (trimmedMessage.Length > MaxMessageLength)
        {
            throw new ArgumentException($"Message must be at most {MaxMessageLength} characters.", nameof(message));
        }

        var trimmedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (trimmedEmail is { Length: > MaxEmailLength })
        {
            throw new ArgumentException($"Email must be at most {MaxEmailLength} characters.", nameof(email));
        }

        return new FeedbackEntry(id, trimmedEmail, trimmedMessage, now);
    }
}
