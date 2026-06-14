namespace Platform.Application.Accounts;

/// <summary>
/// Sends transactional emails. The default implementation logs the message (dev); wire a real
/// provider (Postmark/Resend/SES) for production.
/// </summary>
public interface IEmailSender
{
    Task SendMagicLinkAsync(string email, string link, CancellationToken ct = default);

    /// <summary>Sends a "confirm your email address" link for a newly registered account.</summary>
    Task SendEmailVerificationAsync(string email, string link, CancellationToken ct = default);

    /// <summary>True when this is the dev (non-sending) implementation, so the UI may reveal the link.</summary>
    bool IsDevSender { get; }
}
