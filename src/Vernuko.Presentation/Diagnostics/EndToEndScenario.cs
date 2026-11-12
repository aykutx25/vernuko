using Microsoft.Extensions.DependencyInjection;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.Backups;
using Vernuko.Presentation.Dialogs;
using Vernuko.Presentation.Pages;

namespace Vernuko.Presentation.Diagnostics;

public sealed record ScenarioStep(string Name, bool Ok, string Detail);

/// <summary>
/// The product's success flow, driven through the same view-model commands the buttons use:
/// customer → 8-session package → booking from the package → arrived → completed → 7 left → encrypted backup →
/// a later change → restore → the change is gone and 7 sessions remain.
/// Runs on Windows inside the real WPF app (smoke test / kontrol.cmd, with a screenshot after each step) and on
/// Linux in the test suite. Dialogs must be auto-answered by the host (confirm = yes).
/// </summary>
public static class EndToEndScenario
{
    public const string ServiceName = "Lazer Epilasyon";

    public static async Task<IReadOnlyList<ScenarioStep>> RunAsync(ShellViewModel shell, IServiceProvider services, string backupFolder, Func<string, Task> snapshot)
    {
        var steps = new List<ScenarioStep>();
        try
        {
            await RunCoreAsync(shell, services, backupFolder, snapshot, steps);
        }
        catch (Exception ex)
        {
            steps.Add(new ScenarioStep("beklenmeyen hata", false, $"{ex.GetType().Name}: {ex.Message}"));
        }

        return steps;
    }

    private static async Task RunCoreAsync(ShellViewModel shell, IServiceProvider services, string backupFolder, Func<string, Task> snapshot, List<ScenarioStep> steps)
    {
        var customersService = services.GetRequiredService<ICustomerService>();
        var packages = services.GetRequiredService<IPackageService>();
        var backups = services.GetRequiredService<IBackupService>();
        var time = services.GetRequiredService<TimeProvider>();

        void Ok(string name, string detail) => steps.Add(new ScenarioStep(name, true, detail));
        void Fail(string name, string? detail) =>
            steps.Add(new ScenarioStep(name, false, string.IsNullOrWhiteSpace(detail) ? "(ayrıntı yok)" : detail));

        // 1) Customer
        await shell.NavigateAsync("customers");
        if (shell.CurrentPage is not CustomersViewModel customers)
        {
            Fail("müşteri ekle", "Müşteriler sayfası açılamadı");
            return;
        }

        customers.NewCommand.Execute(null);
        customers.FirstName = "Senaryo";
        customers.LastName = "Müşterisi";
        customers.Phone = "0532 000 00 07";
        await customers.SaveCommand.ExecuteAsync(null);
        if (customers.Details is not { } details)
        {
            Fail("müşteri ekle", customers.ErrorMessage);
            return;
        }

        var customerId = details.Id;
        Ok("müşteri ekle", details.FullName);
        await snapshot("01-musteri");

        // 2) 8-session package
        await customers.SellPackageCommand.ExecuteAsync(null);
        if (shell.Overlay is not SellPackageDialogViewModel sale)
        {
            Fail("paket sat", "Paket satış penceresi açılmadı");
            return;
        }

        sale.Service = sale.Services.FirstOrDefault(s => s.Name == ServiceName);
        sale.Name = "Lazer 8 Seans (otomatik test)";
        sale.Sessions = "8";
        sale.Price = "8000";
        sale.InitialPayment = "0";
        await snapshot("02-paket-satis");
        await sale.SaveCommand.ExecuteAsync(null);
        if (!await WaitUntilAsync(() => shell.Overlay is null))
        {
            Fail("paket sat", sale.ErrorMessage);
            return;
        }

        var package = (await packages.GetCustomerPackagesAsync(customerId)).Value.SingleOrDefault();
        if (package?.RemainingSessions != 8)
        {
            Fail("paket sat", $"kalan seans {package?.RemainingSessions.ToString() ?? "yok"} (beklenen 8)");
            return;
        }

        Ok("paket sat", "kalan 8 seans");

        // 3) Booking from the package in the next hour
        await shell.NavigateAsync("appointments", new OpenNewAppointment(customerId));
        if (shell.CurrentPage is not AppointmentsViewModel page || page.Editor is not { } editor)
        {
            Fail("randevu ver", "Randevu paneli açılmadı");
            return;
        }

        editor.SelectedService = editor.Services.FirstOrDefault(s => s.Name == ServiceName);
        if (editor.SelectedService is null)
        {
            Fail("randevu ver", $"\"{ServiceName}\" hizmeti yok");
            return;
        }

        await WaitUntilAsync(() => editor.Packages.Count > 0);
        editor.SelectedPackage = editor.Packages.FirstOrDefault(p => p.Id == package.Id);
        if (editor.SelectedPackage is null)
        {
            Fail("randevu ver", "Satılan paket randevu panelinde listelenmedi");
            return;
        }

        var now = time.GetLocalNow().DateTime;
        var chosen = await PickSlotInNextHourAsync(editor, now);
        if (chosen is null)
        {
            // No free suggested slot in the next hour: enter the next 5-minute mark by hand (warnings auto-confirmed).
            var t = now.AddMinutes(5);
            editor.Time = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute / 5 * 5, 0).ToString("HH:mm", Core.TurkishText.Culture);
        }

        await snapshot("03-randevu-paneli");
        await editor.SaveCommand.ExecuteAsync(null);
        if (!await WaitUntilAsync(() => page.Editor is null))
        {
            Fail("randevu ver", editor.ErrorMessage);
            return;
        }

        await page.RefreshAsync();
        var row = FindRow(page, customerId);
        if (row is null)
        {
            Fail("randevu ver", "Randevu listede görünmüyor");
            return;
        }

        Ok("randevu ver", $"{row.TimeText} · {row.Staff} · {row.Resources}");

        // 4) Arrived → completed
        var arrive = row.Actions.FirstOrDefault(a => a.Target == AppointmentStatus.Arrived);
        if (arrive is null)
        {
            Fail("müşteri geldi", "\"Müşteri geldi\" seçeneği yok");
            return;
        }

        await page.ChangeStatusCommand.ExecuteAsync(new object[] { row, arrive });
        row = FindRow(page, customerId)!;
        var complete = row.Actions.FirstOrDefault(a => a.Target == AppointmentStatus.Completed);
        if (complete is null)
        {
            Fail("tamamla", $"\"Tamamla\" seçeneği yok (durum: {row.StatusName}, saat {row.TimeText}) {page.ErrorMessage}");
            return;
        }

        await page.ChangeStatusCommand.ExecuteAsync(new object[] { row, complete });
        row = FindRow(page, customerId)!;
        if (row.Status != AppointmentStatus.Completed)
        {
            Fail("tamamla", $"durum {row.StatusName}: {page.ErrorMessage}");
            return;
        }

        Ok("tamamla", "durum Tamamlandı");
        await snapshot("04-tamamlandi");

        // 5) Package decremented
        var after = (await packages.GetCustomerPackagesAsync(customerId)).Value.Single();
        if (after.RemainingSessions != 7)
        {
            Fail("seans düştü", $"kalan {after.RemainingSessions} (beklenen 7)");
            return;
        }

        Ok("seans düştü", "8 → 7");
        await shell.NavigateAsync("customers", new OpenCustomer(customerId));
        await snapshot("05-musteri-karti");

        // 6) Encrypted backup
        var status = await backups.GetStatusAsync();
        if (!status.KeyConfigured)
        {
            var key = await backups.CreateRecoveryKeyAsync();
            if (key.IsFailure || (await backups.ConfirmRecoveryKeyAsync(key.Value)).IsFailure)
            {
                Fail("yedek al", key.IsFailure ? key.Error.Message : "kurtarma anahtarı doğrulanamadı");
                return;
            }
        }

        Directory.CreateDirectory(backupFolder);
        var configured = await backups.ConfigureAsync(backupFolder, BackupSchedule.Daily, false, new RetentionPolicy());
        if (configured.IsFailure)
        {
            Fail("yedek al", configured.Error.Message);
            return;
        }

        var backup = await backups.CreateBackupAsync(BackupTrigger.Manual);
        if (backup.IsFailure)
        {
            Fail("yedek al", backup.Error.Message);
            return;
        }

        Ok("yedek al", $"{backup.Value.FileName} ({backup.Value.SizeBytes / 1024} KB)");

        // 7) A change after the backup
        var later = await customersService.CreateAsync(new Customers.CustomerDetails("Yedekten", "Sonra", "0532 000 00 08"), allowDuplicates: true);
        if (later.IsFailure)
        {
            Fail("yedekten sonra değişiklik", later.Error.Message);
            return;
        }

        // 8) Restore
        var restored = await backups.RestoreAsync(Path.Combine(backupFolder, backup.Value.FileName), null);
        if (restored.IsFailure)
        {
            Fail("geri yükle", restored.Error.Message);
            return;
        }

        var laterStillThere = (await customersService.SearchAsync("Yedekten Sonra")).Value.Count > 0;
        var remaining = (await packages.GetCustomerPackagesAsync(customerId)).Value.SingleOrDefault()?.RemainingSessions;
        if (laterStillThere || remaining != 7)
        {
            Fail("geri yükle", $"yedekten sonraki kayıt {(laterStillThere ? "hâlâ var" : "silindi")}, kalan seans {remaining?.ToString() ?? "yok"} (beklenen: silindi, 7)");
            return;
        }

        Ok("geri yükle", "yedekten sonraki kayıt geri alındı, kalan 7 seans korundu");
        await shell.NavigateAsync("customers", new CustomerSearchRequest("Senaryo"));
        await snapshot("06-geri-yukleme-sonrasi");
    }

    private static async Task<SlotChoice?> PickSlotInNextHourAsync(AppointmentEditorViewModel editor, DateTime now)
    {
        // A service limited to specific rooms/devices: the editor has already selected allowed ones and its
        // suggestions cover the other allowed rooms too, so the selection is left as it is.
        var limited = editor.SelectedService?.AllowedResourceIds is { Count: > 0 };
        var kind = editor.SelectedService?.RequiredResourceKind;
        var rooms = limited ? new List<ResourceChoice>() : editor.Resources.Where(r => kind is null || r.Resource.Kind == kind).ToList();
        IEnumerable<ResourceChoice?> candidates = rooms.Count == 0 ? new ResourceChoice?[] { null } : rooms;
        foreach (var room in candidates)
        {
            if (!limited)
            {
                foreach (var r in editor.Resources)
                {
                    r.IsSelected = r == room;
                }
            }

            foreach (var staff in editor.Staff)
            {
                editor.SelectedStaff = staff;
                await editor.ResourcesChangedCommand.ExecuteAsync(null);
                var slot = editor.Slots.FirstOrDefault(s =>
                {
                    var start = editor.Date.Date + s.Time.ToTimeSpan();
                    return start >= now && start <= now.AddMinutes(55);
                });
                if (slot is not null)
                {
                    await editor.PickSlotCommand.ExecuteAsync(slot);
                    return slot;
                }
            }
        }

        return null;
    }

    private static AppointmentRow? FindRow(AppointmentsViewModel page, Guid customerId) =>
        page.Rows.OfType<AppointmentRow>().FirstOrDefault(r => r.Item.CustomerId == customerId);

    private static async Task<bool> WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }

        return condition();
    }
}
