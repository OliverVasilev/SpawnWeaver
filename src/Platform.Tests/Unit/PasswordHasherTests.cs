using Platform.Infrastructure.Security;
using Xunit;

namespace Platform.Tests.Unit;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_does_not_contain_the_plaintext()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.DoesNotContain("correct horse", hash, StringComparison.Ordinal);
        Assert.StartsWith("pbkdf2$sha256$", hash);
    }

    [Fact]
    public void Hash_is_salted_so_two_hashes_of_the_same_password_differ()
    {
        var a = _hasher.Hash("same-password");
        var b = _hasher.Hash("same-password");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Verify_matches_correct_password_and_rejects_wrong_one()
    {
        var hash = _hasher.Hash("s3cret-password");

        Assert.True(_hasher.Verify("s3cret-password", hash));
        Assert.False(_hasher.Verify("wrong-password", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-hash")]
    [InlineData("pbkdf2$sha256$bad")]
    public void Verify_rejects_malformed_hashes(string hash)
    {
        Assert.False(_hasher.Verify("anything", hash));
    }
}
