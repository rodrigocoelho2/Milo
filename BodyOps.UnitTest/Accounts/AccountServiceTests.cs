using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;
using BodyOps.WebApi.Features.Accounts.Services;

namespace BodyOps.UnitTest.Accounts;

public sealed class AccountServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeUserSession _session = new();

    private AccountService Service(bool allowRegistration = true) =>
        new(_users, _session, TestSupport.Globalization, TestSupport.Options(options => options.AllowRegistration = allowRegistration));

    private static CredentialsDTO Credentials(string username, string password = "squat-2026") => new() { Username = username, Password = password };

    [Theory]
    [InlineData("ab", "squat-2026")]
    [InlineData("has space", "squat-2026")]
    [InlineData("Rodas", "short")]
    public async Task Register_InvalidUsernameOrPassword_IsAValidationProblem(string username, string password)
    {
        var response = await Service().RegisterAsync(Credentials(username, password));

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, response.Status);
        Assert.NotNull(response.Problems!.Detail);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Register_FirstAccount_IsAdminCreatedAndSignedIn()
    {
        var response = await Service().RegisterAsync(Credentials("  Rodas  "));

        Assert.Equal(ServiceApiResponseStatus.Created, response.Status);
        Assert.Equal("Rodas", response.Result!.Username);
        Assert.True(response.Result.IsAdmin);
        Assert.Equal("Rodas", _session.SignedIn!.Username);
        Assert.NotNull(_users.Users[0].LastLoginAt);
    }

    [Fact]
    public async Task Register_SameNameDifferentCase_IsAConflict()
    {
        await Service().RegisterAsync(Credentials("Rodas"));

        var response = await Service().RegisterAsync(Credentials("rodas"));

        Assert.Equal(ServiceApiResponseStatus.Conflict, response.Status);
        Assert.Equal("That username is taken.", response.Problems!.Detail);
    }

    [Fact]
    public async Task Register_ClosedRegistration_AllowsOnlyTheFirstAccount()
    {
        Assert.Equal(ServiceApiResponseStatus.Created, (await Service(allowRegistration: false).RegisterAsync(Credentials("Rodas"))).Status);

        var second = await Service(allowRegistration: false).RegisterAsync(Credentials("Ana"));

        Assert.Equal(ServiceApiResponseStatus.Forbidden, second.Status);
        Assert.False((await Service().RegisterAsync(Credentials("Ana"))).Result!.IsAdmin);
    }

    [Fact]
    public async Task Login_UnknownUserAndWrongPassword_LookTheSame()
    {
        await Service().RegisterAsync(Credentials("Rodas"));

        var unknown = await Service().LoginAsync(Credentials("Nobody"));
        var wrong = await Service().LoginAsync(Credentials("Rodas", "wrong-password"));

        Assert.Equal(ServiceApiResponseStatus.Unauthorized, unknown.Status);
        Assert.Equal(ServiceApiResponseStatus.Unauthorized, wrong.Status);
        Assert.Equal(unknown.Problems!.Detail, wrong.Problems!.Detail);
    }

    [Fact]
    public async Task Login_IgnoresCase()
    {
        await Service().RegisterAsync(Credentials("Rodas"));
        await _session.SignOutAsync();

        var response = await Service().LoginAsync(Credentials("RODAS"));

        Assert.Equal(ServiceApiResponseStatus.Success, response.Status);
        Assert.Equal("Rodas", _session.SignedIn!.Username);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsRefused()
    {
        var me = (await Service().RegisterAsync(Credentials("Rodas"))).Result!;

        var response = await Service().ChangePasswordAsync(me.Id, new PasswordChangeDTO { CurrentPassword = "nope", NewPassword = "deadlift-99" });

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, response.Status);
        Assert.Equal("Your current password is not correct.", response.Problems!.Detail);
    }

    [Fact]
    public async Task ChangePassword_NewStampAndFreshCookie()
    {
        var me = (await Service().RegisterAsync(Credentials("Rodas"))).Result!;
        var oldStamp = _users.Users[0].SecurityStamp;

        var response = await Service().ChangePasswordAsync(me.Id, new PasswordChangeDTO { CurrentPassword = "squat-2026", NewPassword = "deadlift-99" });

        Assert.Equal(ServiceApiResponseStatus.Success, response.Status);
        Assert.NotEqual(oldStamp, _users.Users[0].SecurityStamp);
        Assert.Equal(_users.Users[0].SecurityStamp, _session.SignedIn!.SecurityStamp);
        Assert.True(PasswordHasher.Verify("deadlift-99", _users.Users[0].PasswordHash));
    }

    [Fact]
    public async Task DeleteAccount_NeedsThePassword_ThenSignsOut()
    {
        var me = (await Service().RegisterAsync(Credentials("Rodas"))).Result!;

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await Service().DeleteAccountAsync(me.Id, new AccountDeleteDTO { Password = "nope" })).Status);
        Assert.Single(_users.Users);

        Assert.Equal(ServiceApiResponseStatus.Success, (await Service().DeleteAccountAsync(me.Id, new AccountDeleteDTO { Password = "squat-2026" })).Status);
        Assert.Empty(_users.Users);
        Assert.True(_session.SignedOut);
    }
}
