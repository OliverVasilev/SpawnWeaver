using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Accounts;

namespace Platform.Infrastructure.Accounts;

/// <summary>
/// Production email sender backed by the Resend HTTP API (https://resend.com). Posts transactional
/// emails to <c>https://api.resend.com/emails</c> using a bearer API key. Registered as the
/// <see cref="IEmailSender"/> when an API key is configured (see DI selection).
/// </summary>
internal sealed class ResendEmailSender : IEmailSender
{
    private const string Endpoint = "https://api.resend.com/emails";

    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsDevSender => false;

    public Task SendMagicLinkAsync(string email, string link, CancellationToken ct = default)
        => SendAsync(
            email,
            "Your SpawnWeaver sign-in link",
            BuildButtonEmail(
                "Sign in to SpawnWeaver",
                "Click the button below to sign in. This link expires in 15 minutes.",
                "Sign in",
                link),
            ct);

    public Task SendEmailVerificationAsync(string email, string link, CancellationToken ct = default)
        => SendAsync(
            email,
            "Confirm your SpawnWeaver email",
            BuildButtonEmail(
                "Confirm your email",
                "Welcome to SpawnWeaver! Confirm your email address to activate your account. This link expires in 24 hours.",
                "Confirm email",
                link),
            ct);

    private async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var payload = new
        {
            from = FormatFrom(),
            to = new[] { to },
            subject,
            html,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
#pragma warning disable CA1848 // transactional email, not a hot path
                _logger.LogError(
                    "Resend send failed ({Status}) for {To}: {Body}", (int)response.StatusCode, to, body);
#pragma warning restore CA1848
            }
        }
        catch (HttpRequestException ex)
        {
#pragma warning disable CA1848
            _logger.LogError(ex, "Resend send threw for {To}", to);
#pragma warning restore CA1848
        }
    }

    /// <summary>Returns the configured From, formatting "Name &lt;addr&gt;" when only a bare address is set.</summary>
    private string FormatFrom()
    {
        var from = _options.FromAddress;
        if (from.Contains('<', StringComparison.Ordinal))
        {
            return from;
        }

        return string.IsNullOrWhiteSpace(_options.FromName) ? from : $"{_options.FromName} <{from}>";
    }

    private static string BuildButtonEmail(string heading, string body, string buttonText, string link)
    {
        var safeLink = System.Net.WebUtility.HtmlEncode(link);
        return $"""
            <div style="font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1a1a2e">
              <h1 style="font-size:20px;margin:0 0 12px">{heading}</h1>
              <p style="font-size:15px;line-height:1.5;margin:0 0 24px">{body}</p>
              <p style="margin:0 0 24px">
                <a href="{safeLink}" style="display:inline-block;background:#5b4bff;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600">{buttonText}</a>
              </p>
              <p style="font-size:12px;color:#6b7280;margin:0">If the button doesn't work, copy this link:<br>{safeLink}</p>
            </div>
            """;
    }
}
