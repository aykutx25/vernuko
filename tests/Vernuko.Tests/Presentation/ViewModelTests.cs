using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Presentation;
using Vernuko.Presentation.Pages;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Presentation;

public class ViewModelTests
{
    [Fact]
    public async Task Setup_wizard_walks_through_all_steps_and_signs_in()
    {
        await using var app = await TestApp.CreateAsync();
        var root = app.Get<RootViewModel>();
        await root.StartAsync();
        var wizard = Assert.IsType<SetupWizardViewModel>(root.Screen);

        wizard.NextCommand.Execute(null); // welcome
        wizard.NextCommand.Execute(null); // profile not chosen
        Assert.True(wizard.HasError);
        wizard.SelectProfileCommand.Execute(wizard.Profiles.Single(p => p.Name == "Güzellik ve Lazer Merkezi"));
        wizard.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Business, wizard.Step);
        Assert.Contains(wizard.Services, s => s.Name == "Lazer Epilasyon");
        Assert.Contains(wizard.Resources, r => r.Name == "Lazer Odası 1");

        wizard.BusinessName = "Işıltı Güzellik";
        wizard.Phone = "0212 555 00 11";
        wizard.NextCommand.Execute(null);
        wizard.NextCommand.Execute(null); // days (Mon–Sat default)
        wizard.NextCommand.Execute(null); // hours default
        wizard.Staff[0].Name = "Selin Demir";
        wizard.NextCommand.Execute(null);
        wizard.Services[0].Price = "1.000";
        wizard.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Admin, wizard.Step);
        wizard.AdminDisplayName = "Ayşe Yılmaz";
        wizard.AdminUsername = "ayse";
        wizard.AdminPassword = "Guclu-Parola-2026";
        wizard.AdminPasswordConfirm = "farkli";
        wizard.NextCommand.Execute(null);
        Assert.Equal("Parolalar eşleşmiyor.", wizard.ErrorMessage);
        wizard.AdminPasswordConfirm = "Guclu-Parola-2026";
        wizard.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Backup, wizard.Step);

        await wizard.FinishCommand.ExecuteAsync(null);
        Assert.Equal(SetupStep.Finish, wizard.Step);
        Assert.NotNull(wizard.RecoveryKey);
        Assert.False(wizard.StartUsingCommand.CanExecute(null));
        wizard.RecoveryKeyWrittenDown = true;
        await wizard.StartUsingCommand.ExecuteAsync(null);
        await Task.Delay(200);

        var shell = Assert.IsType<ShellViewModel>(root.Screen);
        Assert.Equal("Işıltı Güzellik", shell.BusinessName);
        Assert.Contains(shell.NavItems, n => n.Title == "Paket & Seanslar");
        Assert.IsType<AppointmentsViewModel>(shell.CurrentPage);
        var services = (await app.Get<CatalogService>().GetServicesAsync()).Value;
        Assert.Equal(100_000, services.Single(s => s.Name == "Lazer Epilasyon").PriceMinor);
    }

    [Fact]
    public async Task Front_desk_books_with_quick_customer_and_suggested_slot()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var page = app.Get<AppointmentsViewModel>();
        page.SelectedDate = new DateTime(2026, 11, 12);
        await page.OnNavigatedToAsync(null);
        Assert.Contains(page.Rows, r => r is BreakRow);

        await page.OpenEditorAsync(null);
        var editor = page.Editor!;
        editor.CustomerSearch = "Gizem Ateş";
        editor.StartAddCustomerCommand.Execute(null);
        Assert.Equal("Gizem", editor.NewFirstName);
        editor.NewPhone = "0555 123 45 67";
        await editor.AddCustomerCommand.ExecuteAsync(null);
        Assert.NotNull(editor.SelectedCustomer);

        editor.SelectedService = editor.Services.Single(s => s.Name == "Lazer Epilasyon");
        await Task.Delay(100);
        Assert.Contains(editor.Resources, r => r.IsSelected && r.Resource.Kind == Core.ResourceKind.Room);
        Assert.NotEmpty(editor.Slots);
        editor.PickSlotCommand.Execute(editor.Slots.First(s => s.Time == new TimeOnly(14, 0)));
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        await Task.Delay(200);

        Assert.Null(page.Editor);
        var row = Assert.Single(page.Rows.OfType<AppointmentRow>());
        Assert.Equal("Gizem Ateş", row.Customer);
        Assert.Equal("14:00", row.TimeText);
        Assert.Contains(row.Actions, a => a.Target == AppointmentStatus.Cancelled);

        // Cancel with a reason from the prompt.
        app.Get<FakeDialogs>().PromptAnswers.Enqueue("Müşteri aradı");
        await page.ChangeStatusCommand.ExecuteAsync(new object[] { row, row.Actions.Single(a => a.Target == AppointmentStatus.Cancelled) });
        Assert.Equal(AppointmentStatus.Cancelled, page.Rows.OfType<AppointmentRow>().Single().Status);
    }

    [Fact]
    public async Task Week_and_month_views_load()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var page = app.Get<AppointmentsViewModel>();
        page.SelectedDate = new DateTime(2026, 11, 12);
        await page.OnNavigatedToAsync(null);
        page.View = CalendarView.Week;
        await page.RefreshAsync();
        Assert.Equal(7, page.WeekDays.Count);
        Assert.Equal(DayOfWeek.Monday, page.WeekDays[0].Date.DayOfWeek);
        page.View = CalendarView.Month;
        await page.RefreshAsync();
        Assert.Equal(42, page.MonthCells.Count);
        Assert.Contains(page.MonthCells, c => c.Date == new DateOnly(2026, 11, 1) && c.InMonth);
    }

    [Fact]
    public async Task Payment_dialog_is_idempotent_on_double_click()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync();
        var dialog = app.Get<Vernuko.Presentation.Dialogs.PaymentDialogViewModel>();
        dialog.Initialize(customer, "Ayşe Kara", null, 0);
        dialog.Amount = "250";
        await Task.WhenAll(dialog.SaveCommand.ExecuteAsync(null), Task.Run(() => app.Get<PaymentService>().RecordAsync(
            new RecordTransactionRequest(customer, Payments.TransactionKind.Payment, 25_000, Payments.PaymentMethod.Cash, false, null, null, null, "other-dialog"))));
        await dialog.SaveCommand.ExecuteAsync(null);
        Assert.Equal(-50_000, (await app.Get<PaymentService>().GetBalanceAsync(customer)).Value);
    }

    [Fact]
    public async Task Demo_seeder_fills_a_separate_database()
    {
        await using var app = await TestApp.CreateAsync();
        var r = await app.Get<DemoDataSeeder>().SeedAsync();
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        var info = await app.Get<SettingsService>().GetBusinessInfoAsync();
        Assert.True(info!.IsDemo);
        var day = await app.Get<AppointmentService>().GetRangeAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 13), new AppointmentFilter());
        Assert.Equal(8, day.Value.Count);
    }
}
