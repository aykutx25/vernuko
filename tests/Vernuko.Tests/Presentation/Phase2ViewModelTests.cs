using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Customers;
using Vernuko.Payments;
using Vernuko.Presentation.Dialogs;
using Vernuko.Presentation.Pages;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Presentation;

/// <summary>Headless checks of the Phase 2 screens' view models against the real services and database.</summary>
public class Phase2ViewModelTests
{
    [Fact]
    public async Task Editor_creates_a_weekly_series_and_row_menu_cancels_the_rest()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customerId = await app.AddCustomerAsync();
        var page = app.Get<AppointmentsViewModel>();
        page.SelectedDate = new DateTime(2026, 11, 13);
        await page.OnNavigatedToAsync(null);

        await page.OpenEditorAsync(customerId);
        var editor = page.Editor!;
        Assert.True(editor.ShowRecurrence);
        editor.SelectedService = editor.Services.Single(s => s.Name == "Lazer Epilasyon");
        await Task.Delay(100);
        editor.Time = "11:00";
        editor.IsRecurring = true;
        editor.Frequency = editor.FrequencyOptions.Single(f => f.Value == RecurrenceFrequency.Weekly);
        editor.RecurrenceInterval = "1";
        editor.RecurrenceCount = "4";
        Assert.Equal("Her hafta, 4 kez", editor.RecurrenceSummary);
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
        await Task.Delay(200);
        Assert.Null(page.Editor);

        var all = (await app.Get<AppointmentService>().GetRangeAsync(new DateOnly(2026, 11, 13), new DateOnly(2026, 12, 31), new AppointmentFilter(null, null, null))).Value;
        Assert.Equal(4, all.Count(a => a.IsInSeries));
        Assert.All(all, a => Assert.Equal(new TimeOnly(11, 0), TimeOnly.FromDateTime(a.StartLocal)));

        var row = Assert.Single(page.Rows.OfType<AppointmentRow>());
        Assert.True(row.CanCancelSeries);
        app.Get<FakeDialogs>().PromptAnswers.Enqueue("Müşteri taşındı");
        await page.CancelSeriesCommand.ExecuteAsync(row);
        Assert.Contains("4 randevu iptal edildi", page.InfoMessage);
    }

    [Fact]
    public async Task Stock_page_creates_product_receives_and_sells_on_account()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customerId = await app.AddCustomerAsync();
        var page = app.Get<InventoryViewModel>();
        await page.OnNavigatedToAsync(null);

        page.NewCommand.Execute(null);
        page.Name = "Yatıştırıcı jel";
        page.Unit = "adet";
        page.SalePrice = "150";
        page.Cost = "60";
        page.MinimumStock = "3";
        await page.SaveCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.NotNull(page.Selected);

        var dialogs = app.Get<FakeDialogs>();
        dialogs.PromptAnswers.Enqueue("10");
        dialogs.PromptAnswers.Enqueue("Fatura 123");
        await page.ReceiveCommand.ExecuteAsync(null);
        Assert.Equal(10, page.Selected!.Stock);

        var customer = (await app.Get<CustomerService>().SearchAsync("Ayşe")).Value.Single(c => c.Id == customerId);
        page.SaleCustomer = customer;
        page.SaleQuantity = "2";
        page.SalePriceText = "150";
        page.SalePayment = page.PaymentOptions[0]; // on account
        await page.SellCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.Equal(8, page.Selected!.Stock);
        Assert.Equal(30_000, (await app.Get<PaymentService>().GetBalanceAsync(customerId)).Value);
        Assert.Contains(page.Movements, m => m.Kind == Inventory.StockMovementKind.Sold && m.QuantityDelta == -2);
    }

    [Fact]
    public async Task Waitlist_entry_is_offered_when_a_matching_appointment_is_cancelled()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var waiting = await app.AddCustomerAsync("Zeynep", "Aksoy", "0533 111 22 33");
        var booked = await app.AddCustomerAsync("Merve", "Kaya", "0533 444 55 66");
        var catalog = await app.CatalogAsync();

        var waitlist = app.Get<WaitlistViewModel>();
        await waitlist.OnNavigatedToAsync(null);
        waitlist.Customer = (await app.Get<CustomerService>().SearchAsync("Zeynep")).Value.Single(c => c.Id == waiting);
        waitlist.Service = waitlist.ServiceOptions.Single(o => o.Value == catalog.Laser.Id);
        waitlist.From = new DateTime(2026, 11, 12);
        waitlist.To = new DateTime(2026, 11, 20);
        await waitlist.AddCommand.ExecuteAsync(null);
        Assert.Null(waitlist.ErrorMessage);
        Assert.Single(waitlist.Items);

        var created = await app.Get<AppointmentService>().CreateAsync(
            new CreateAppointmentRequest(booked, catalog.Laser.Id, catalog.Selin.Id, [catalog.Room1.Id], TestApp.Local(15), null, null, null, null), confirmWarnings: true);
        Assert.True(created.Value.Saved);

        var page = app.Get<AppointmentsViewModel>();
        page.SelectedDate = new DateTime(2026, 11, 12);
        await page.OnNavigatedToAsync(null);
        var row = Assert.Single(page.Rows.OfType<AppointmentRow>());
        var dialogs = app.Get<FakeDialogs>();
        dialogs.PromptAnswers.Enqueue("Hastalandı");
        await page.ChangeStatusCommand.ExecuteAsync(new object[] { row, row.Actions.Single(a => a.Target == AppointmentStatus.Cancelled) });
        Assert.Contains(dialogs.Shown, m => m.StartsWith("Boşalan saat", StringComparison.Ordinal) && m.Contains("Zeynep Aksoy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Customer_card_records_consent_and_previews_an_encrypted_photo()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customerId = await app.AddCustomerAsync();

        var templates = app.Get<ConsentTemplatesViewModel>();
        await templates.LoadAsync();
        templates.NewCommand.Execute(null);
        templates.TemplateTitle = "Lazer epilasyon bilgilendirme ve onam";
        templates.Body = "Uygulamanın riskleri ve bakım talimatları hakkında bilgilendirildim.";
        await templates.SaveCommand.ExecuteAsync(null);
        Assert.Null(templates.ErrorMessage);

        var records = app.Get<CustomerRecordsViewModel>();
        await records.LoadAsync(customerId);
        Assert.NotNull(records.SelectedTemplate);
        records.Method = records.Methods.Single(m => m.Value == ConsentMethod.PaperSigned);
        await records.RecordConsentCommand.ExecuteAsync(null);
        var consent = Assert.Single(records.Consents);
        Assert.True(consent.IsActive);
        Assert.Equal(1, consent.TemplateVersion);

        var photo = Path.Combine(app.Root, "before.png");
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];
        await File.WriteAllBytesAsync(photo, png);
        var dialogs = app.Get<FakeDialogs>();
        dialogs.OpenFileAnswers.Enqueue(photo);
        dialogs.PromptAnswers.Enqueue("1. seans öncesi");
        records.NewFileKind = records.FileKinds.Single(k => k.Value == CustomerFileKind.Photo);
        await records.AddFileCommand.ExecuteAsync(null);
        Assert.Null(records.ErrorMessage);
        var file = Assert.Single(records.Files);
        Assert.True(file.IsImage);

        await records.PreviewCommand.ExecuteAsync(file);
        Assert.Equal(png, records.PreviewImage);
        Assert.True(records.HasPreview);

        // The stored blob is not the plain image.
        var stored = Directory.EnumerateFiles(app.Get<Vernuko.Application.Common.IAppEnvironment>().AttachmentsDirectory).Single();
        Assert.NotEqual(png, await File.ReadAllBytesAsync(stored));

        dialogs.PromptAnswers.Enqueue("Müşteri talebi");
        await records.RevokeConsentCommand.ExecuteAsync(consent);
        Assert.False(Assert.Single(records.Consents).IsActive);
    }

    [Fact]
    public async Task Package_sale_dialog_passes_the_seller_for_commission()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customerId = await app.AddCustomerAsync();
        var catalog = await app.CatalogAsync();
        var rules = app.Get<CommissionRulesViewModel>();
        await rules.LoadAsync();
        rules.Basis = rules.BasisOptions.Single(b => b.Value == CommissionBasis.PackageSold);
        rules.RateKind = rules.RateKindOptions.Single(k => k.Value == CommissionRateKind.Percent);
        rules.Value = "5";
        await rules.SaveCommand.ExecuteAsync(null);
        Assert.Null(rules.ErrorMessage);
        Assert.Single(rules.Rules);

        var dialog = app.Get<SellPackageDialogViewModel>();
        await dialog.InitializeAsync(customerId);
        dialog.Service = dialog.Services.Single(s => s.Id == catalog.Laser.Id);
        dialog.Name = "Lazer 8 seans";
        dialog.Sessions = "8";
        dialog.Price = "8000";
        dialog.SoldBy = dialog.SellerOptions.Single(o => o.Value == catalog.Selin.Id);
        await dialog.SaveCommand.ExecuteAsync(null);
        Assert.Null(dialog.ErrorMessage);

        var report = (await app.Get<CommissionService>().GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))).Value;
        var selin = Assert.Single(report.Totals);
        Assert.Equal(catalog.Selin.Id, selin.StaffMemberId);
        Assert.Equal(40_000, selin.PackageSaleMinor);
    }
}
