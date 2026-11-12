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

public class ShortcutTests
{
    [Fact]
    public async Task Shortcuts_work_without_keyboard_focus_and_respect_modal_dialogs()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var shell = app.Get<ShellViewModel>();
        await shell.InitializeAsync();
        var page = Assert.IsType<Vernuko.Presentation.Pages.AppointmentsViewModel>(shell.CurrentPage);
        var day = page.SelectedDate;

        Assert.True(shell.HandleShortcut(Shortcut.Next));
        Assert.Equal(day.AddDays(1), page.SelectedDate);
        Assert.True(shell.HandleShortcut(Shortcut.Today));
        Assert.Equal(day, page.SelectedDate);

        // Ctrl+N opens the quick booking panel; Esc closes it.
        Assert.True(shell.HandleShortcut(Shortcut.NewAppointment));
        await WaitUntilAsync(() => page.Editor is not null);
        Assert.False(shell.HandleShortcut(Shortcut.Previous)); // date keys are ignored while the panel is open
        Assert.True(shell.HandleShortcut(Shortcut.Close));
        await WaitUntilAsync(() => page.Editor is null);

        // A modal dialog swallows everything except Esc.
        var dialog = app.Get<Vernuko.Presentation.Dialogs.PaymentDialogViewModel>();
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        shell.ShowOverlay(dialog);
        Assert.False(shell.HandleShortcut(Shortcut.NewAppointment));
        Assert.True(shell.HandleShortcut(Shortcut.Close));
        Assert.True(closed);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
    }
}

public class TypedCustomerTests
{
    [Fact]
    public async Task Waitlist_accepts_a_typed_name_that_matches_one_customer_and_explains_otherwise()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        await app.AddCustomerAsync("Hakan", "Cımbız", "0533 222 33 44");
        var page = app.Get<Vernuko.Presentation.Pages.WaitlistViewModel>();
        await page.OnNavigatedToAsync(null);

        // Unknown name: clear instruction instead of "Müşteri seçin."
        page.CustomerSearch = "Hakan Cımvbız";
        await page.AddCommand.ExecuteAsync(null);
        Assert.Contains("kayıtlı müşteri yok", page.ErrorMessage);
        Assert.Empty(page.Items);

        // Typed (not picked) but unique: accepted.
        page.CustomerSearch = "hakan cımbız";
        await page.AddCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.Equal("Hakan Cımbız", Assert.Single(page.Items).CustomerName);
    }
}

public class EndToEndScenarioTests
{
    [Fact]
    public async Task Success_flow_scenario_passes_through_the_view_models()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var shell = app.Get<ShellViewModel>();
        await shell.InitializeAsync();
        var shots = new List<string>();

        var steps = await Vernuko.Presentation.Diagnostics.EndToEndScenario.RunAsync(
            shell, app.Get<IServiceProvider>(), Path.Combine(app.Root, "senaryo-yedek"), name => { shots.Add(name); return Task.CompletedTask; });

        Assert.True(steps.All(s => s.Ok), string.Join("\n", steps.Select(s => $"{(s.Ok ? "ok  " : "FAIL")} {s.Name}: {s.Detail}")));
        Assert.Equal(["müşteri ekle", "paket sat", "randevu ver", "tamamla", "seans düştü", "yedek al", "geri yükle"], steps.Select(s => s.Name));
        Assert.Equal(6, shots.Count);
    }
}
