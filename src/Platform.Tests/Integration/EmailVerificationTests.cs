using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Application.Accounts;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

/// <summary>
/// Verifies the hard email-verification gate that applies when a real email provider is
/// configured: sign-up does not sign in, sign-in is blocked until the emailed link is followed.
/// Uses a fake non-dev <see cref="IEmailSender"/> that captures the link instead of sending it.
/// </summary>
public sealed class EmailVerificationTests : IClassFixture<EmailVerificationTests.VerifyingApiFactory>
{
    private readonly VerifyingApiFactory _factory;

    public EmailVerificationTests(VerifyingApiFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Sign_up_with_real_provider_requires_verification_before_sign_in()
    {
        var email = $"verify-{Guid.NewGuid():N}@example.com";
        var password = "supersecret123";
        var signupClient = _factory.CreateClient();

        // Sign-up succeeds but does NOT sign in: it routes to the "check your email" page.
        var signup = await signupClient.PostAsJsonAsync(
            "/api/auth/signup", new SignUpRequest(email, "Vee", password));
        Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
        var auth = await signup.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("/dashboard/verify-pending", auth!.Redirect);

        // No auth cookie was set — the account is not accessible yet.
        var account = await signupClient.GetAsync("/api/account");
        Assert.Equal(HttpStatusCode.Unauthorized, account.StatusCode);

        // Signing in before verifying is refused with a stable code.
        var earlySignin = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/signin", new SignInRequest(email, password));
        Assert.Equal(HttpStatusCode.Forbidden, earlySignin.StatusCode);

        // Follow the captured verification link → signed in and redirected to the dashboard.
        var link = _factory.Sender.LinkFor(email);
        Assert.NotNull(link);
        var verifyClient = NoRedirectClient();
        var verify = await verifyClient.GetAsync(new Uri(link!).PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, verify.StatusCode);
        Assert.Equal("/dashboard", verify.Headers.Location?.ToString());

        var verifiedAccount = await verifyClient.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, verifiedAccount!.Email);

        // Password sign-in now works.
        var signin = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/signin", new SignInRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, signin.StatusCode);
    }

    [Fact]
    public async Task Verify_resend_always_returns_ok_even_for_unknown_email()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/verify/resend", new ResendVerificationRequest($"nobody-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>API factory wired with a capturing (non-dev) email sender on an isolated DB.</summary>
    public sealed class VerifyingApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath =
            Path.Combine(Path.GetTempPath(), $"spawnweaver-verify-{Guid.NewGuid():N}.db");

        public CapturingEmailSender Sender { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Sender);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(_databasePath))
            {
                try { File.Delete(_databasePath); }
                catch (IOException) { /* best-effort */ }
            }
        }
    }

    /// <summary>Records verification links per email instead of sending them; reports as a real sender.</summary>
    public sealed class CapturingEmailSender : IEmailSender
    {
        private readonly ConcurrentDictionary<string, string> _verificationLinks = new(StringComparer.OrdinalIgnoreCase);

        public bool IsDevSender => false;

        public Task SendMagicLinkAsync(string email, string link, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SendEmailVerificationAsync(string email, string link, CancellationToken ct = default)
        {
            _verificationLinks[email] = link;
            return Task.CompletedTask;
        }

        public string? LinkFor(string email)
            => _verificationLinks.TryGetValue(email, out var link) ? link : null;
    }
}
