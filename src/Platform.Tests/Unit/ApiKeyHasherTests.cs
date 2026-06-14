using Platform.Infrastructure.Security;
using Xunit;

namespace Platform.Tests.Unit;

public sealed class ApiKeyHasherTests
{
    private readonly ApiKeyHasher _hasher = new();

    [Fact]
    public void Hash_does_not_contain_the_plaintext_key()
    {
        const string secret = "sk_super_secret_value";

        var hash = _hasher.Hash(secret);

        Assert.NotEqual(secret, hash);
        Assert.DoesNotContain("super_secret_value", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_is_deterministic()
    {
        const string secret = "sk_abc123";

        Assert.Equal(_hasher.Hash(secret), _hasher.Hash(secret));
    }

    [Fact]
    public void Verify_matches_correct_key_and_rejects_wrong_key()
    {
        const string secret = "sk_correct";
        var hash = _hasher.Hash(secret);

        Assert.True(_hasher.Verify(secret, hash));
        Assert.False(_hasher.Verify("sk_wrong", hash));
    }
}
