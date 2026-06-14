using System.Net;
using System.Net.Http.Json;
using Platform.Contracts.Http;
using Xunit;

namespace Platform.Tests.Integration;

public sealed class AccountEndpointsTests : IClassFixture<SpawnWeaverApiFactory>
{
    private readonly SpawnWeaverApiFactory _factory;

    public AccountEndpointsTests(SpawnWeaverApiFactory factory) => _factory = factory;

    private static SignUpRequest NewSignup(string? email = null)
        => new(email ?? $"dev-{Guid.NewGuid():N}@example.com", "Jane Dev", "supersecret123");

    [Fact]
    public async Task Sign_up_signs_in_and_account_is_retrievable()
    {
        var client = _factory.CreateClient();
        var signup = NewSignup();

        var response = await client.PostAsJsonAsync("/api/auth/signup", signup);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(signup.Email, auth!.Email);
        Assert.StartsWith("user_", auth.UserId);

        // The signup set an auth cookie; the account is now retrievable on this client.
        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.NotNull(account);
        Assert.Equal(signup.Email, account!.Email);
        Assert.Equal("Jane Dev", account.DisplayName);
        Assert.NotNull(account.OrganizationId);
        Assert.NotNull(account.OrganizationName);
    }

    [Fact]
    public async Task Sign_up_with_short_password_is_rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/signup",
            new SignUpRequest($"weak-{Guid.NewGuid():N}@example.com", "Weak", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sign_up_with_duplicate_email_returns_conflict()
    {
        var client = _factory.CreateClient();
        var signup = NewSignup();

        var first = await client.PostAsJsonAsync("/api/auth/signup", signup);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _factory.CreateClient().PostAsJsonAsync("/api/auth/signup", signup);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_account_request_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_with_wrong_password_is_unauthorized()
    {
        var signup = NewSignup();
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/signup", signup);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/signin",
            new SignInRequest(signup.Email, "the-wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_then_sign_out_revokes_access()
    {
        var signup = NewSignup();
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/signup", signup);

        var client = _factory.CreateClient();
        var signin = await client.PostAsJsonAsync("/api/auth/signin",
            new SignInRequest(signup.Email, signup.Password));
        Assert.Equal(HttpStatusCode.OK, signin.StatusCode);

        // Authenticated.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account")).StatusCode);

        // Sign out, then the same client can no longer read the account.
        var signout = await client.PostAsync("/api/auth/signout", content: null);
        Assert.Equal(HttpStatusCode.OK, signout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task Display_name_can_be_updated()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/signup", NewSignup());

        var update = await client.PutAsJsonAsync("/api/account", new UpdateAccountRequest("Renamed Dev"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal("Renamed Dev", account!.DisplayName);
    }

    [Fact]
    public async Task Active_sessions_list_includes_the_current_session()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/signup", NewSignup());

        var sessions = await client.GetFromJsonAsync<SessionsResponse>("/api/account/sessions");

        Assert.NotNull(sessions);
        Assert.NotEmpty(sessions!.Sessions);
        Assert.Contains(sessions.Sessions, s => s.Current);
    }
}
