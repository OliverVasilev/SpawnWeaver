using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class MagicLinkTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public MagicLinkTests(SpawnWeaverApiFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Magic_link_signs_in_provisioning_the_account_and_is_single_use()
    {
        var client = NoRedirectClient();
        var email = $"magic-{Guid.NewGuid():N}@example.com";

        // Request a link — dev environment returns the link so we can follow it.
        var request = await client.PostAsJsonAsync("/api/auth/magic/request", new MagicLinkRequest(email));
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var body = await request.Content.ReadFromJsonAsync<MagicLinkResponse>();
        Assert.True(body!.Sent);
        Assert.NotNull(body.DevLink);

        var consumePath = new Uri(body.DevLink!).PathAndQuery;

        // Following the link signs in (302 → /dashboard) and sets the auth cookie.
        var consume = await client.GetAsync(consumePath);
        Assert.Equal(HttpStatusCode.Redirect, consume.StatusCode);
        Assert.Equal("/dashboard", consume.Headers.Location?.ToString());

        // The account exists and we're authenticated on this client.
        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, account!.Email);
        Assert.NotNull(account.OrganizationId);

        // The token is single-use — a second visit is rejected.
        var reuse = await client.GetAsync(consumePath);
        Assert.Equal(HttpStatusCode.Redirect, reuse.StatusCode);
        Assert.Contains("/dashboard/signin", reuse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Magic_link_requests_are_throttled_per_email()
    {
        var client = _factory.CreateClient();
        var email = $"throttle-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/api/auth/magic/request", new MagicLinkRequest(email));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/magic/request", new MagicLinkRequest(email));
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task An_invalid_token_redirects_to_sign_in()
    {
        var client = NoRedirectClient();

        var response = await client.GetAsync("/api/auth/magic?token=not-a-real-token");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/dashboard/signin", response.Headers.Location?.ToString());
    }
}
