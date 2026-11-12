using Vernuko.Application.Services;
using Vernuko.Presentation;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Regressions found on the first run on a real Windows PC.</summary>
public class WindowsFeedbackTests
{
    [Fact]
    public async Task Empty_password_is_a_form_error_and_never_locks_the_account()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var auth = app.Get<AuthService>();
        auth.Logout();

        for (var i = 0; i < 10; i++)
        {
            var r = await auth.LoginAsync(TestApp.AdminUser, string.Empty);
            Assert.Equal("validation.auth.empty", r.Error.Code);
        }

        Assert.Equal("validation.auth.empty", (await auth.LoginAsync("  ", "x")).Error.Code);
        Assert.True((await auth.LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
    }

    [Fact]
    public async Task Login_screen_reports_missing_fields()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        app.Get<AuthService>().Logout();
        var login = app.Get<LoginViewModel>();
        login.Username = TestApp.AdminUser;
        await login.LoginCommand.ExecuteAsync(null);
        Assert.Equal("Kullanıcı adı ve parolayı girin.", login.ErrorMessage);
    }
}
