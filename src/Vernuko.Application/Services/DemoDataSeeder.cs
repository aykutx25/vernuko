using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Payments;

namespace Vernuko.Application.Services;

/// <summary>
/// Fills a SEPARATE demo database (never the real one) with fictional sample data for training and screenshots.
/// </summary>
public sealed class DemoDataSeeder(SetupService setup, AuthService auth, CustomerService customers, CatalogService catalog, AppointmentService appointments, PackageService packages, PaymentService payments, TimeProvider time)
{
    public const string DemoUsername = "demo";
    public const string DemoPassword = "Randevu-Deneme-2026";

    public async Task<Result> SeedAsync(CancellationToken ct = default)
    {
        if (!await setup.IsCompletedAsync(ct))
        {
            var profile = BusinessProfileCatalog.Get(BusinessProfileId.BeautyAndLaser);
            var r = await setup.CompleteAsync(
                new SetupRequest(
                    BusinessProfileId.BeautyAndLaser,
                    "Demo Güzellik ve Lazer Merkezi",
                    "0212 000 00 00",
                    null,
                    TimeZones.Turkey,
                    WeeklySchedule.DefaultBusinessWeek(),
                    [new SetupStaff("Selin Demir", "Lazer Uzmanı"), new SetupStaff("Derya Arslan", "Güzellik Uzmanı"), new SetupStaff("Merve Kaya", "Cilt Bakım Uzmanı")],
                    profile.SuggestedServices.Select(s => new SetupServiceItem(s.Name, s.Category, s.DurationMinutes, 150_000, s.PrepMinutes, s.CleanupMinutes, s.RequiredResource, s.ResourceKeywords)).ToList(),
                    profile.SuggestedResources.Select(x => new SetupResource(x.Name, x.Kind)).ToList(),
                    "Demo Yönetici",
                    DemoUsername,
                    DemoPassword,
                    false,
                    null,
                    IsDemo: true),
                ct);
            if (r.IsFailure)
            {
                return r.Error;
            }
        }

        var login = await auth.LoginAsync(DemoUsername, DemoPassword, ct);
        if (login.IsFailure)
        {
            return login;
        }

        var existing = await customers.SearchAsync(null, false, 1, ct);
        if (existing.IsSuccess && existing.Value.Count > 0)
        {
            return Result.Success();
        }

        var people = new (string First, string Last, string Phone)[]
        {
            ("Ayşe", "Kara", "0532 456 78 90"), ("Zeynep", "Ak", "0551 234 56 78"), ("Elif", "Yılmaz", "0544 987 65 43"),
            ("Derya", "Koç", "0530 112 23 34"), ("Ceren", "Kılıç", "0507 665 44 33"), ("Sema", "Kaya", "0533 778 90 11"),
            ("Hande", "Erdem", "0542 338 77 66"), ("Aslı", "Yıldız", "0505 221 33 44"),
        };
        var customerIds = new List<Guid>();
        foreach (var (first, last, phone) in people)
        {
            var c = await customers.CreateAsync(new CustomerDetails(first, last, phone), allowDuplicates: true, ct);
            if (c.IsFailure)
            {
                return c.Error;
            }

            customerIds.Add(c.Value);
        }

        var services = (await catalog.GetServicesAsync(false, ct)).Value;
        var staff = (await catalog.GetStaffAsync(false, ct)).Value;
        var allResources = (await catalog.GetResourcesAsync(false, ct)).Value;
        var rooms = allResources.Where(r => r.Kind == ResourceKind.Room).ToList();
        var laser = services.First(s => s.Name == "Lazer Epilasyon");
        var package = await packages.SellAsync(new SellPackageRequest(customerIds[0], laser.Id, null, "Lazer Epilasyon 8 Seans", "Bacak", 8, 800_000, 365, 300_000, PaymentMethod.Card, Guid.NewGuid().ToString("N")), ct);
        if (package.IsFailure)
        {
            return package.Error;
        }

        var zone = TimeZones.Find(TimeZones.Turkey);
        var today = TimeZones.LocalDate(time.GetUtcNow().UtcDateTime, zone);
        var day = today.DayOfWeek == DayOfWeek.Sunday ? today.AddDays(1) : today;
        // Room hints follow the service rules: laser → a laser room + the device, skin care → the skin care room.
        var plan = new (int Customer, string Service, int Staff, string Room, int Hour, int Minute)[]
        {
            (0, "Lazer Epilasyon", 0, "Lazer Odası 1", 9, 0), (1, "Lazer Epilasyon", 1, "Lazer Odası 2", 10, 0), (2, "Cilt Bakımı", 2, "Cilt Bakım Odası", 10, 30),
            (3, "Bölgesel İncelme", 0, "Lazer Odası 1", 11, 15), (4, "Lazer Epilasyon", 2, "Lazer Odası 2", 13, 30), (5, "Cilt Bakımı", 1, "Cilt Bakım Odası", 14, 0),
            (6, "Medikal Cilt Bakımı", 0, "Cilt Bakım Odası", 15, 30), (7, "Lazer Epilasyon", 1, "Lazer Odası 2", 16, 30),
        };
        foreach (var p in plan)
        {
            var service = services.First(s => s.Name == p.Service);
            var start = day.ToDateTime(new TimeOnly(p.Hour, p.Minute));
            var resources = ResourcesFor(service, allResources, rooms, p.Room);
            var r = await appointments.CreateAsync(
                new CreateAppointmentRequest(customerIds[p.Customer], service.Id, staff[p.Staff % staff.Count].Id, resources, start, null, null,
                    p.Customer == 0 ? "Bacak" : null, p.Customer == 0 ? package.Value : null),
                confirmWarnings: true,
                ct);
            if (r.IsFailure)
            {
                return r.Error;
            }

            if (!r.Value.Saved)
            {
                // Demo data must stay consistent with the booking rules; surface the reason instead of silently skipping.
                return new Error("demo.appointment", $"Demo randevusu oluşturulamadı ({p.Service} {p.Hour:00}:{p.Minute:00}): {string.Join("; ", r.Value.Issues.Select(i => i.Message))}");
            }
        }

        _ = payments;
        return Result.Success();
    }

    /// <summary>Respects the service's room/device rules (e.g. laser → a laser room + the laser device).</summary>
    private static List<Guid> ResourcesFor(ServiceDto service, IReadOnlyList<ResourceDto> all, IReadOnlyList<ResourceDto> rooms, string roomHint)
    {
        var hinted = rooms.FirstOrDefault(r => r.Name == roomHint) ?? rooms[0];
        if (service.AllowedResourceIds is not { Count: > 0 } allowedIds)
        {
            return [hinted.Id];
        }

        var allowed = all.Where(r => allowedIds.Contains(r.Id)).ToList();
        return allowed.GroupBy(r => r.Kind).Select(g => (g.FirstOrDefault(r => r.Id == hinted.Id) ?? g.First()).Id).ToList();
    }
}
