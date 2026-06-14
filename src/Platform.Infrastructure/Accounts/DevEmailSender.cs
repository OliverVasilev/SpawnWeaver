using Microsoft.Extensions.Logging;
using Platform.Application.Accounts;

namespace Platform.Infrastructure.Accounts;

/// <summary>
/// Development email sender: logs the message instead of sending it, and flags itself as the dev
/// sender so the UI/API can reveal the magic link locally. Replace with a real provider
/// (Postmark/Resend/SES) for production by registering another <see cref="IEmailSender"/>.
/// </summary>
internal sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger) => _logger = logger;

    public bool IsDevSender => true;

    public Task SendMagicLinkAsync(string email, string link, CancellationToken ct = default)
    {
#pragma warning disable CA1848 // dev-only logging, not a hot path
        _logger.LogInformation("[DEV EMAIL] Magic link for {Email}: {Link}", email, link);
#pragma warning restore CA1848
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string email, string link, CancellationToken ct = default)
    {
#pragma warning disable CA1848 // dev-only logging, not a hot path
        _logger.LogInformation("[DEV EMAIL] Verification link for {Email}: {Link}", email, link);
#pragma warning restore CA1848
        return Task.CompletedTask;
    }
}
