using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.BusinessProfiles;
using Vernuko.Presentation;
using Vernuko.Presentation.Pages;
using Vernuko.ServiceDesk;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Presentation;

/// <summary>The Stok and Teknik Servis screens driven the way the desktop drives them (no WPF).</summary>
public class ModeViewModelTests
{
    [Fact]
    public async Task Stock_shop_setup_skips_the_calendar_steps_and_opens_on_the_sales_screen()
    {
        await using var app = await TestApp.CreateAsync();
        var root = app.Get<RootViewModel>();
        await root.StartAsync();
        var wizard = Assert.IsType<SetupWizardViewModel>(root.Screen);
        Assert.Equal(3, wizard.ProfileGroups.Count);
        Assert.Equal(string.Empty, wizard.ProgramName);

        wizard.NextCommand.Execute(null);
        wizard.SelectProfileCommand.Execute(wizard.Profiles.Single(p => p.Name == "Hırdavat ve Yapı Malzemesi"));
        Assert.Equal("Stok", wizard.ProgramName);
        wizard.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Business, wizard.Step);
        wizard.BusinessName = "Yıldız Hırdavat";
        wizard.Phone = "0224 555 00 11";
        wizard.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Admin, wizard.Step); // no working days, hours, staff or services for a shop
        wizard.BackCommand.Execute(null);
        Assert.Equal(SetupStep.Business, wizard.Step);
        wizard.NextCommand.Execute(null);
        wizard.AdminDisplayName = "Okan Yıldız";
        wizard.AdminUsername = "okan";
        wizard.AdminPassword = "Guclu-Parola-2026";
        wizard.AdminPasswordConfirm = "Guclu-Parola-2026";
        wizard.NextCommand.Execute(null);
        Assert.Equal(SetupStep.Backup, wizard.Step);
        await wizard.FinishCommand.ExecuteAsync(null);
        Assert.Null(wizard.ErrorMessage);
        wizard.RecoveryKeyWrittenDown = true;
        await wizard.StartUsingCommand.ExecuteAsync(null);
        await root.LastScreenChange;

        var shell = Assert.IsType<ShellViewModel>(root.Screen);
        Assert.Equal("Stok", shell.ProgramName);
        Assert.IsType<SalesViewModel>(shell.CurrentPage);
        var keys = shell.NavItems.Select(n => n.Key).ToList();
        Assert.Contains("sales", keys);
        Assert.Contains("purchases", keys);
        Assert.Contains("stocktools", keys);
        Assert.DoesNotContain("appointments", keys);
        Assert.DoesNotContain("packages", keys);
        foreach (var key in keys)
        {
            await shell.NavigateAsync(key);
            Assert.True(shell.CurrentPage is { HasError: false }, $"{key}: {shell.CurrentPage?.ErrorMessage}");
        }
    }

    [Fact]
    public async Task Purchase_sale_return_and_count_through_the_screens()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.HardwareStore);
        var inv = app.Get<IInventoryService>();
        var drill = (await inv.SaveProductAsync(new ProductDto(Guid.Empty, "Matkap ucu seti", "M-13", "adet", 45_000, 0, 5, true, 0, "8690000000017"))).Value;

        // Alış: new supplier, scan the barcode, 10 pieces at 250 ₺ + KDV, pay 1.000 ₺ now.
        var purchases = app.Get<PurchasesViewModel>();
        await purchases.OnNavigatedToAsync(null);
        purchases.SupplierName = "Demir Toptan";
        purchases.TaxNumber = "1234567890";
        await purchases.SaveSupplierCommand.ExecuteAsync(null);
        Assert.Null(purchases.ErrorMessage);
        Assert.NotNull(purchases.SelectedSupplier);
        purchases.Code = "8690000000017";
        await purchases.AddCodeCommand.ExecuteAsync(null);
        var line = Assert.Single(purchases.Lines);
        line.Quantity = "10";
        line.UnitCost = "250";
        Assert.Equal("KDV hariç ₺2.500,00 + KDV ₺500,00 = ₺3.000,00", purchases.TotalsText);
        purchases.PayNow = "1000";
        await purchases.SaveReceiptCommand.ExecuteAsync(null);
        Assert.Null(purchases.ErrorMessage);
        Assert.Empty(purchases.Lines);
        Assert.Equal(200_000, purchases.SelectedSupplier!.BalanceMinor);
        Assert.Single(purchases.RecentReceipts);

        // Satış: scan twice (quantity 2), all cash with 1.000 ₺ handed over → change.
        var sales = app.Get<SalesViewModel>();
        await sales.OnNavigatedToAsync(null);
        sales.Code = "8690000000017";
        await sales.AddCodeCommand.ExecuteAsync(null);
        sales.Code = "M-13";
        await sales.AddCodeCommand.ExecuteAsync(null);
        var cartLine = Assert.Single(sales.Cart);
        Assert.Equal(2, cartLine.QuantityValue);
        Assert.Equal(90_000, sales.TotalMinor);
        sales.Cash = "1000";
        Assert.Equal("Para üstü: ₺100,00", sales.PaymentText);
        await sales.CompleteCommand.ExecuteAsync(null);
        Assert.Null(sales.ErrorMessage);
        Assert.Contains("Para üstü ₺100,00", sales.InfoMessage, StringComparison.Ordinal);
        Assert.Empty(sales.Cart);
        var sold = Assert.Single(sales.TodaySales);
        Assert.Equal(90_000, sold.TotalMinor);
        Assert.Equal(90_000, sales.Today!.CashMinor);

        // An unknown code explains itself.
        sales.Code = "0000";
        await sales.AddCodeCommand.ExecuteAsync(null);
        Assert.NotNull(sales.ErrorMessage);

        // İade: one piece comes back, cash refunded.
        sales.SelectedSale = sold;
        await WaitUntil(() => sales.ReturnRows.Count == 1);
        sales.ReturnRows[0].Quantity = "1";
        sales.RefundCash = "450";
        await sales.ReturnCommand.ExecuteAsync(null);
        Assert.Null(sales.ErrorMessage);
        Assert.Equal(45_000, sales.Today!.NetSalesMinor);
        Assert.Equal(9, (await inv.GetProductsAsync()).Value.Single(p => p.Id == drill).Stock);

        // Sayım: 7 on the shelf → −2 posted; reorder is empty (7 > 5).
        var tools = app.Get<StockToolsViewModel>();
        await tools.OnNavigatedToAsync(null);
        tools.Filter = "matkap";
        var row = Assert.Single(tools.VisibleRows);
        row.Counted = "7";
        Assert.Equal("−2", row.DifferenceText);
        await tools.ApplyCountCommand.ExecuteAsync(null);
        Assert.Null(tools.ErrorMessage);
        Assert.Equal(7, (await inv.GetProductsAsync()).Value.Single(p => p.Id == drill).Stock);
        Assert.Empty(tools.Reorder);
        Assert.Equal(7, tools.Value!.UnitCount);
    }

    [Fact]
    public async Task Service_ticket_from_intake_to_delivery_through_the_screen()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupModeAsync(BusinessProfileId.PhoneRepair);
        var customerId = await app.AddCustomerAsync();
        var screen = (await app.Get<IInventoryService>().SaveProductAsync(new ProductDto(Guid.Empty, "Ekran T12", "EK-T12", "adet", 150_000, 90_000, 1, true, 0, "8690000000031"))).Value;
        await app.Get<IInventoryService>().ReceiveAsync(screen, 3, 90_000, null);

        var page = app.Get<ServiceTicketsViewModel>();
        await page.OnNavigatedToAsync(null);
        Assert.True(page.ShowEmpty);
        page.NewTicketCommand.Execute(null);
        Assert.True(page.IsEditingIntake);

        // Missing customer → explained, nothing saved.
        await page.SaveIntakeCommand.ExecuteAsync(null);
        Assert.NotNull(page.ErrorMessage);

        var customer = (await app.Get<ICustomerService>().SearchAsync("Ayşe")).Value.Single();
        page.PickCustomerCommand.Execute(customer);
        page.Device = page.Devices.Single(d => d.Value == DeviceKind.Phone);
        page.Brand = "Telsa";
        page.Model = "T12";
        page.Serial = "35-209900-176148-1";
        page.Complaint = "Ekran kırık";
        page.Accessories = "Kılıf";
        page.UnlockCode = "1234";
        await page.SaveIntakeCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.False(page.IsEditingIntake);
        Assert.True(page.ShowDetail);
        Assert.True(page.Ticket!.HasUnlockCode);
        Assert.Single(page.Tickets);
        Assert.Equal("0532 456 78 90", page.Tickets[0].CustomerPhone);
        Assert.Single((await app.Get<IServiceDeskService>().GetTicketsAsync(true, "0532 456")).Value);
        Assert.Single((await app.Get<IServiceDeskService>().GetTicketsAsync(true, "ayşe kara")).Value);
        Assert.Empty((await app.Get<IServiceDeskService>().GetTicketsAsync(true, "Başka Biri")).Value);

        await page.PrintCommand.ExecuteAsync("Intake");
        var slip = Assert.Single(app.Get<FakePlatform>().PrintedSlips);
        Assert.Equal(SlipKind.Intake, slip.Kind);
        Assert.DoesNotContain(slip.Notes, n => n.Contains("1234", StringComparison.Ordinal));

        await page.RevealCodeCommand.ExecuteAsync(null);
        Assert.Equal("1234", page.RevealedCode);

        // Price → approval → part + labour → ready → delivery.
        page.Estimate = "2000";
        await page.SetEstimateCommand.ExecuteAsync(null);
        Assert.True(page.IsApprovalPending);
        await page.MoveCommand.ExecuteAsync("Ready");
        Assert.NotNull(page.ErrorMessage); // not before the customer approves
        page.ApprovedBy = "Ayşe Kara";
        await page.ApproveCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.Equal(TicketStatus.Repairing, page.Ticket!.Summary.Status);

        page.PartCode = "EK-T12";
        await page.AddPartCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        page.LaborText = "Ekran değişimi";
        page.LaborPrice = "500";
        await page.AddLaborCommand.ExecuteAsync(null);
        Assert.Equal(2, page.Ticket!.Lines.Count);
        Assert.Equal(200_000, page.Ticket.AmountDueMinor);
        Assert.Equal(2, (await app.Get<IInventoryService>().GetProductsAsync()).Value.Single(p => p.Id == screen).Stock);

        page.Deposit = "500";
        await page.TakeDepositCommand.ExecuteAsync(null);
        Assert.Contains("kalan ₺1.500,00", page.AmountDueText, StringComparison.Ordinal);

        await page.MoveCommand.ExecuteAsync("Ready");
        Assert.Equal(TicketStatus.Ready, page.Ticket!.Summary.Status);
        Assert.Equal("Done", page.Tickets.Single().StatusGroup); // the status chip's colour (green)

        page.PayCash = "1500";
        await page.DeliverCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.False(page.IsOpenTicket);
        Assert.False(page.Ticket!.HasUnlockCode); // erased at delivery
        Assert.Empty(page.Tickets); // open only

        await page.PrintCommand.ExecuteAsync("Delivery");
        var delivery = app.Get<FakePlatform>().PrintedSlips[^1];
        Assert.Equal(SlipKind.Delivery, delivery.Kind);
        Assert.NotNull(delivery.SameFaultWarrantyUntil);

        // The device comes back within the warranty: the new intake shows its history and links the warranty.
        page.OpenOnly = false;
        page.NewTicketCommand.Execute(null);
        page.PickCustomerCommand.Execute(customer);
        page.Brand = "Telsa";
        page.Serial = "352099001761481";
        await WaitUntil(() => page.DeviceHistory.Count == 1);
        page.Complaint = "Ekran yine çalışmıyor";
        await page.SaveIntakeCommand.ExecuteAsync(null);
        Assert.True(page.CanLinkWarranty);
        await page.LinkWarrantyCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.StartsWith("Garanti kapsamında", page.WarrantyText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BusinessMode.Stok, "Vernuko Stok", "sales")]
    [InlineData(BusinessMode.TeknikServis, "Vernuko Teknik Servis", "servicetickets")]
    public async Task Each_program_shows_its_own_name_and_menu_and_hides_the_calendar(BusinessMode mode, string name, string startPage)
    {
        await using var app = await TestApp.CreateAsync();
        var seeded = await app.Get<ModeDemoSeeder>().SeedAsync(mode);
        Assert.True(seeded.IsSuccess, seeded.IsFailure ? seeded.Error.ToString() : null);
        app.Get<IAuthService>().Logout();

        var root = app.Get<RootViewModel>();
        await root.StartAsync();
        var login = Assert.IsType<LoginViewModel>(root.Screen);
        Assert.Equal(name, "Vernuko " + login.ProgramName);
        Assert.Equal(name, root.WindowTitle);
        login.Username = DemoDataSeeder.DemoUsername;
        login.Password = DemoDataSeeder.DemoPassword;
        await login.LoginCommand.ExecuteAsync(null);
        await root.LastScreenChange;

        var shell = Assert.IsType<ShellViewModel>(root.Screen);
        Assert.Equal(name, shell.ProgramFullName);
        Assert.Equal(name, root.WindowTitle);
        Assert.False(shell.IsAppointmentMode);
        Assert.Equal(startPage, shell.NavItems.Single(n => n.IsSelected).Key);
        Assert.DoesNotContain(shell.NavItems, n => n.Key is "appointments" or "packages" or "waitlist" or "reports" or "home");
        Assert.Equal(string.Empty, shell.MessagingStatusText); // no appointment reminders in the status bar

        foreach (var item in shell.NavItems.ToList())
        {
            await shell.NavigateAsync(item.Key);
            Assert.True(shell.CurrentPage is { HasError: false }, $"{item.Key}: {shell.CurrentPage?.ErrorMessage}");
        }

        var settings = Assert.IsType<SettingsViewModel>((await Navigate(shell, "settings")).CurrentPage);
        Assert.False(settings.Messaging.ShowReminders);
        Assert.Equal("Mesaj kuralları", settings.Messaging.RulesTitle);
        if (mode == BusinessMode.TeknikServis)
        {
            Assert.Equal("İşçilik", Assert.IsType<CatalogViewModel>((await Navigate(shell, "catalog")).CurrentPage).Title);
            var tickets = Assert.IsType<ServiceTicketsViewModel>((await Navigate(shell, "servicetickets")).CurrentPage);
            Assert.Equal(5, tickets.Tickets.Count); // open ones; the sixth was delivered
            Assert.Contains(tickets.LaborItems, l => l.Name == "Ekran değişimi");
            tickets.Selected = tickets.Tickets.First(t => t.Status == TicketStatus.Repairing);
            await WaitUntil(() => tickets.Ticket is not null);
            tickets.LaborItem = tickets.LaborItems.Single(l => l.Name == "Batarya değişimi");
            Assert.Equal("Batarya değişimi", tickets.LaborText);
            Assert.Equal("400,00", tickets.LaborPrice);
        }
        else
        {
            var sales = Assert.IsType<SalesViewModel>((await Navigate(shell, "sales")).CurrentPage);
            Assert.Equal(4, sales.TodaySales.Count(s => s.Kind == Vernuko.Commerce.SaleKind.Sale));
            Assert.Equal(1, sales.TodaySales.Count(s => s.Kind == Vernuko.Commerce.SaleKind.Return));
            var tools = Assert.IsType<StockToolsViewModel>((await Navigate(shell, "stocktools")).CurrentPage);
            Assert.Contains(tools.Reorder.SelectMany(g => g.Lines), l => l.ProductName.StartsWith("Su terazisi", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Randevu_keeps_its_name_calendar_and_reports()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var shell = app.Get<ShellViewModel>();
        await shell.InitializeAsync();
        Assert.Equal("Vernuko Randevu", shell.ProgramFullName);
        Assert.True(shell.IsAppointmentMode);
        Assert.Contains(shell.NavItems, n => n.Key == "appointments");
        Assert.Contains(shell.NavItems, n => n.Key == "reports");
    }

    [Fact]
    public void Demo_barcodes_are_valid_ean13() => Assert.Equal("8690000000012", ModeDemoSeeder.Ean13("869000000001"));

    private static async Task<ShellViewModel> Navigate(ShellViewModel shell, string key)
    {
        await shell.NavigateAsync(key);
        return shell;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }
}
