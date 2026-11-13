using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Kuaför: boya bekleme süresi (personel boşta, koltuk dolu) ve danışmanlık: çevrim içi görüşme bağlantısı.</summary>
public class HairAndMeetingTests
{
    private sealed record Salon(TestApp App, ServiceDto Dye, ServiceDto Cut, StaffDto Stylist, ResourceDto Chair1, ResourceDto Chair2, Guid CustomerA, Guid CustomerB);

    private static async Task<Salon> SetupSalonAsync()
    {
        var app = await TestApp.CreateAsync();
        var profile = BusinessProfileCatalog.Get(BusinessProfileId.HairAndBarber);
        var result = await app.Get<SetupService>().CompleteAsync(new SetupRequest(
            BusinessProfileId.HairAndBarber, "Makas Kuaför", "0212 555 44 33", null, TimeZones.Turkey, WeeklySchedule.DefaultBusinessWeek(),
            [new SetupStaff("Ece Usta", "Kuaför")],
            profile.SuggestedServices.Select(s => new SetupServiceItem(s.Name, s.Category, s.DurationMinutes, 100_000, s.PrepMinutes, s.CleanupMinutes, s.RequiredResource, s.ResourceKeywords, s.ProcessingStartMinutes, s.ProcessingMinutes)).ToList(),
            profile.SuggestedResources.Select(x => new SetupResource(x.Name, x.Kind)).ToList(),
            "Ece Usta", TestApp.AdminUser, TestApp.AdminPassword, false, null));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Assert.True((await app.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        var catalog = app.Get<CatalogService>();
        var services = (await catalog.GetServicesAsync()).Value;
        var resources = (await catalog.GetResourcesAsync()).Value;
        return new Salon(app, services.Single(s => s.Name == "Saç Boyama"), services.Single(s => s.Name == "Saç Kesimi"), (await catalog.GetStaffAsync()).Value.Single(),
            resources.Single(r => r.Name == "Koltuk 1"), resources.Single(r => r.Name == "Koltuk 2"),
            await app.AddCustomerAsync("Boya", "Müşterisi", "0532 111 11 11"), await app.AddCustomerAsync("Kesim", "Müşterisi", "0532 222 22 22"));
    }

    private static Task<Result<BookingOutcome>> BookAsync(Salon s, Guid customer, ServiceDto service, ResourceDto chair, int hour, int minute = 0, int? duration = null) =>
        s.App.Get<AppointmentService>().CreateAsync(new CreateAppointmentRequest(customer, service.Id, s.Stylist.Id, [chair.Id], TestApp.Local(hour, minute), duration, null, null, null), confirmWarnings: true);

    [Fact]
    public void Slot_with_a_waiting_part_frees_the_staff_member_only_in_the_middle()
    {
        var staff = Guid.NewGuid();
        var chair = Guid.NewGuid();
        var start = new DateTime(2026, 11, 12, 7, 0, 0, DateTimeKind.Utc);
        var slot = new AppointmentSlot(start, 90, 0, 0, staff, [chair], 30, 40);
        var blocks = slot.Blocks().ToList();
        Assert.Equal(3, blocks.Count);
        Assert.Equal(new UtcRange(start, start.AddMinutes(30)), blocks[0].Block);
        Assert.Equal(new UtcRange(start.AddMinutes(70), start.AddMinutes(90)), blocks[1].Block);
        Assert.Equal(new UtcRange(start, start.AddMinutes(90)), blocks.Single(b => b.Id == chair).Block);

        // Shortened below the waiting part: the staff member is simply busy the whole time.
        Assert.False((slot with { DurationMinutes = 60 }).HasProcessing);
        Assert.Single((slot with { DurationMinutes = 60 }).Blocks(), b => b.Id == staff);

        Assert.True(ServiceDefinition.ValidateProcessing(90, 30, 40).IsSuccess);
        Assert.True(ServiceDefinition.ValidateProcessing(90, 0, 0).IsSuccess);
        Assert.True(ServiceDefinition.ValidateProcessing(90, 30, 60).IsFailure); // no work left at the end
        Assert.True(ServiceDefinition.ValidateProcessing(90, 0, 40).IsFailure);
    }

    [Fact]
    public async Task During_the_dye_wait_the_stylist_takes_a_haircut_in_another_chair_but_not_in_the_same_chair()
    {
        var s = await SetupSalonAsync();
        await using var _ = s.App;
        Assert.Equal((30, 40), (s.Dye.ProcessingStartMinutes, s.Dye.ProcessingMinutes)); // from the hair profile

        var dye = await BookAsync(s, s.CustomerA, s.Dye, s.Chair1, 10);
        Assert.True(dye.Value.Saved, string.Join("; ", dye.Value.Issues.Select(i => i.Message)));

        // During the colour (10:30–11:10) the stylist is free, but chair 1 is still taken by the dye customer.
        var other = await s.App.AddCustomerAsync("Üçüncü", "Müşteri", "0532 333 33 33");
        var sameChair = await BookAsync(s, other, s.Cut, s.Chair1, 10, 30);
        Assert.False(sameChair.Value.Saved);
        Assert.Contains(sameChair.Value.Issues, i => i.Code == "conflict.resource");
        Assert.DoesNotContain(sameChair.Value.Issues, i => i.Code == "conflict.staff");

        // 10:30–11:00 haircut in chair 2 while the colour develops.
        var cut = await BookAsync(s, s.CustomerB, s.Cut, s.Chair2, 10, 30);
        Assert.True(cut.Value.Saved, string.Join("; ", cut.Value.Issues.Select(i => i.Message)));

        // 11:00 overlaps the rinse/finish (11:10–11:30): the stylist is busy again.
        var tooLate = await BookAsync(s, other, s.Cut, s.Chair2, 11, 0);
        Assert.Contains(tooLate.Value.Issues, i => i.Code == "conflict.staff");

        // Suggestions for the stylist in chair 2 include the waiting time.
        var slots = (await s.App.Get<AppointmentService>().GetAvailableSlotsAsync(new SlotQuery(DateOnly.FromDateTime(TestApp.Local(10)), s.Cut.Id, s.Stylist.Id, [s.Chair2.Id]))).Value;
        Assert.DoesNotContain(slots, x => x.LocalStart == new TimeOnly(10, 30)); // now taken by the haircut
        Assert.Contains(slots, x => x.LocalStart == new TimeOnly(11, 30));

        // The calendar shows the waiting part.
        var agenda = (await s.App.Get<AppointmentService>().GetDayAsync(DateOnly.FromDateTime(TestApp.Local(10)), new AppointmentFilter())).Value;
        Assert.Equal("Bekleme 10:30–11:10 (personel boşta)", agenda.Items.Single(i => i.ServiceId == s.Dye.Id).ProcessingText);
    }

    [Fact]
    public async Task Moving_the_dye_appointment_keeps_its_waiting_part()
    {
        var s = await SetupSalonAsync();
        await using var _ = s.App;
        var dye = (await BookAsync(s, s.CustomerA, s.Dye, s.Chair1, 10)).Value.AppointmentId!.Value;
        var moved = await s.App.Get<AppointmentService>().RescheduleAsync(new RescheduleRequest(dye, TestApp.Local(13), null, null), confirmWarnings: true);
        Assert.True(moved.IsSuccess && moved.Value.Saved, moved.IsFailure ? moved.Error.ToString() : string.Join("; ", moved.Value.Issues.Select(i => i.Message)));
        Assert.True((await BookAsync(s, s.CustomerB, s.Cut, s.Chair2, 13, 30)).Value.Saved);
    }

    [Fact]
    public async Task Catalog_validates_the_waiting_part()
    {
        var s = await SetupSalonAsync();
        await using var _ = s.App;
        var catalog = s.App.Get<CatalogService>();
        var bad = await catalog.SaveServiceAsync(s.Dye with { ProcessingStartMinutes = 30, ProcessingMinutes = 70 });
        Assert.True(bad.IsFailure);
        Assert.Contains("Bekleme", bad.Error.Message, StringComparison.Ordinal);
        Assert.True((await catalog.SaveServiceAsync(s.Cut with { ProcessingStartMinutes = 0, ProcessingMinutes = 0 })).IsSuccess);
    }

    [Fact]
    public async Task Meeting_links_must_be_https_and_appear_in_the_calendar()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var c = await app.CatalogAsync();
        var customer = await app.AddCustomerAsync();
        var appointments = app.Get<AppointmentService>();
        var request = new CreateAppointmentRequest(customer, c.SkinCare.Id, c.Derya.Id, [c.Room2.Id], TestApp.Local(15), null, null, null, null, MeetingUrl: "javascript:alert(1)");
        var bad = await appointments.CreateAsync(request, confirmWarnings: true);
        Assert.True(bad.IsFailure);
        Assert.Equal("validation.appointment.meeting_url", bad.Error.Code);
        Assert.True((await appointments.CreateAsync(request with { MeetingUrl = "http://zoom.us/j/1" }, confirmWarnings: true)).IsFailure);

        var ok = await appointments.CreateAsync(request with { MeetingUrl = "https://meet.google.com/abc-defg-hij" }, confirmWarnings: true);
        Assert.True(ok.IsSuccess && ok.Value.Saved);
        var item = (await appointments.GetDayAsync(DateOnly.FromDateTime(TestApp.Local(15)), new AppointmentFilter())).Value.Items.Single();
        Assert.Equal("https://meet.google.com/abc-defg-hij", item.MeetingUrl);

        Assert.True((await appointments.SetMeetingUrlAsync(item.Id, null)).IsSuccess);
        Assert.Null((await appointments.GetDayAsync(DateOnly.FromDateTime(TestApp.Local(15)), new AppointmentFilter())).Value.Items.Single().MeetingUrl);
    }
}
