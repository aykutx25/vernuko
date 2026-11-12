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

public class MoveToNewComputerTests
{
    [Fact]
    public async Task New_computer_restores_a_backup_from_the_setup_wizard_and_signs_in_with_old_credentials()
    {
        // Old computer: data and an encrypted backup.
        await using var oldPc = await TestApp.CreateAsync();
        var key = (await oldPc.SetupBeautyAsync()).RecoveryKey!;
        var customer = await oldPc.AddCustomerAsync("Taşınan", "Müşteri", "0532 111 22 33");
        var backup = (await oldPc.Get<BackupService>().CreateBackupAsync(Vernuko.Backups.BackupTrigger.Manual)).Value;
        var file = Path.Combine(oldPc.Root, "backups", backup.FileName);

        // New computer: fresh installation, different Windows account key.
        await using var newPc = await TestApp.CreateAsync(protector: new TestSecretProtector());
        var root = newPc.Get<RootViewModel>();
        await root.StartAsync();
        var wizard = Assert.IsType<SetupWizardViewModel>(root.Screen);
        var dialogs = newPc.Get<FakeDialogs>();

        // Wrong recovery key: refused, wizard stays.
        dialogs.OpenFileAnswers.Enqueue(file);
        dialogs.PromptAnswers.Enqueue("AAAA-BBBB-CCCC-DDDD-EEEE-FFFF-GGGG-HHHH");
        await wizard.RestoreFromBackupCommand.ExecuteAsync(null);
        Assert.NotNull(wizard.ErrorMessage);
        Assert.IsType<SetupWizardViewModel>(root.Screen);

        // Right key: data moves over, login screen with a hint, old credentials work.
        dialogs.OpenFileAnswers.Enqueue(file);
        dialogs.PromptAnswers.Enqueue(key);
        await wizard.RestoreFromBackupCommand.ExecuteAsync(null);
        await root.LastScreenChange;
        var login = Assert.IsType<LoginViewModel>(root.Screen);
        Assert.Contains("Yedek bu bilgisayara yüklendi", login.InfoMessage);
        Assert.True((await newPc.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        Assert.Contains((await newPc.Get<CustomerService>().SearchAsync("Taşınan")).Value, c => c.Id == customer);

        // Backups keep working on the new computer (key re-protected for this Windows account).
        Assert.True((await newPc.Get<BackupService>().GetStatusAsync()).KeyConfigured);

        // Once the computer is set up, the unauthenticated restore path is closed.
        var again = await newPc.Get<BackupService>().RestoreIntoNewInstallationAsync(file, key);
        Assert.Equal("backup.not_new_installation", again.Error.Code);
    }
}
