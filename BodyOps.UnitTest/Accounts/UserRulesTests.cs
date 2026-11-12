using BodyOps.WebApi.Features.Accounts.Services;

namespace BodyOps.UnitTest.Accounts;

/// <summary>
/// Discord ids, names and the suggested user name.
/// </summary>
public sealed class UserRulesTests
{
    [Theory]
    [InlineData("80351110224678912", true)]
    [InlineData("1570448224030826588", true)]
    [InlineData("1234", false)]
    [InlineData("12a45678", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDiscordId_OnlyDigits(string? value, bool expected) => Assert.Equal(expected, UserRules.IsDiscordId(value));

    [Fact]
    public void DiscordName_TrimsAndShortens()
    {
        Assert.Equal("Rodas", UserRules.DiscordName("  Rodas "));
        Assert.Null(UserRules.DiscordName("   "));
        Assert.Equal(UserRules.DiscordNameLength, UserRules.DiscordName(new string('x', 100))!.Length);
    }

    [Fact]
    public void SuggestUserName_KeepsAllowedCharacters()
    {
        Assert.Equal("rodas", UserRules.SuggestUserName("rodas", "Rodas ✨", 24));
        Assert.Equal("Rodas", UserRules.SuggestUserName("ab", "Rodas ✨", 24));
        Assert.Equal(string.Empty, UserRules.SuggestUserName("✨", "✨", 24));
        Assert.Equal(24, UserRules.SuggestUserName(new string('a', 40), null, 24).Length);
    }
}
