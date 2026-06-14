using Microsoft.Extensions.Options;
using Platform.Infrastructure.Security;
using Platform.Tests.TestDoubles;
using Xunit;

namespace Platform.Tests.Unit;

public sealed class PlayerTokenServiceTests
{
    private readonly MutableClock _clock = new();

    private PlayerTokenService CreateService(string secret = "unit-test-secret", TimeSpan? lifetime = null)
    {
        var options = Options.Create(new PlayerTokenOptions
        {
            TokenSecret = secret,
            TokenLifetime = lifetime ?? TimeSpan.FromHours(1),
        });

        return new PlayerTokenService(options, _clock);
    }

    [Fact]
    public void Issued_token_validates_with_same_ids()
    {
        var service = CreateService();
        var token = service.Issue("proj_a", "player_1");

        var result = service.Validate(token.Value);

        Assert.True(result.IsValid);
        Assert.False(result.IsExpired);
        Assert.Equal("player_1", result.PlayerId);
        Assert.Equal("proj_a", result.ProjectId);
    }

    [Fact]
    public void Expired_token_is_reported_as_expired()
    {
        var service = CreateService(lifetime: TimeSpan.FromMinutes(30));
        var token = service.Issue("proj_a", "player_1");

        _clock.Advance(TimeSpan.FromMinutes(31));
        var result = service.Validate(token.Value);

        Assert.False(result.IsValid);
        Assert.True(result.IsExpired);
        Assert.Equal("player_1", result.PlayerId);
    }

    [Fact]
    public void Tampered_token_is_invalid()
    {
        var service = CreateService();
        var token = service.Issue("proj_a", "player_1");

        // Flip the player id but keep the original signature.
        var parts = token.Value.Split('.');
        var tampered = string.Join('.', "player_hacker", parts[1], parts[2], parts[3]);

        Assert.False(service.Validate(tampered).IsValid);
    }

    [Fact]
    public void Garbage_token_is_invalid()
    {
        var service = CreateService();

        Assert.False(service.Validate("not-a-real-token").IsValid);
        Assert.False(service.Validate(string.Empty).IsValid);
    }

    [Fact]
    public void Token_from_a_different_secret_is_invalid()
    {
        var issuer = CreateService(secret: "secret-one");
        var token = issuer.Issue("proj_a", "player_1");

        var verifier = CreateService(secret: "secret-two");

        Assert.False(verifier.Validate(token.Value).IsValid);
    }
}
