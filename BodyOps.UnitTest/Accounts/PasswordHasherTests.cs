using BodyOps.WebApi.Extensions.Authentication;

namespace BodyOps.UnitTest.Accounts;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_VerifiesOnlyTheSamePassword()
    {
        var hash = PasswordHasher.Hash("squat-2026");

        Assert.StartsWith("pbkdf2-sha512$", hash);
        Assert.True(PasswordHasher.Verify("squat-2026", hash));
        Assert.False(PasswordHasher.Verify("Pikachu123", hash));
    }

    [Fact]
    public void Hash_UsesANewSaltEveryTime() =>
        Assert.NotEqual(PasswordHasher.Hash("squat-2026"), PasswordHasher.Hash("squat-2026"));

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("pbkdf2-sha512$x$abc$def")]
    [InlineData("pbkdf2-sha512$1000$not-base64!$def")]
    public void Verify_MalformedHash_IsFalse(string stored) => Assert.False(PasswordHasher.Verify("squat-2026", stored));

    [Fact]
    public void GenerateTemporary_IsReadable()
    {
        var password = PasswordHasher.GenerateTemporary();

        Assert.Equal(12, password.Length);
        Assert.DoesNotContain(password, character => "0O1lI".Contains(character));
    }
}
