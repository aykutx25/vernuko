using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Presentation.Pages;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>"Lazer yalnızca lazer odasında ve lazer cihazıyla yapılır" — per-service room/device rule.</summary>
public class ServiceResourceRuleTests
{
    private sealed record Rig(TestApp App, Catalog Catalog, ResourceDto SkinRoom, ResourceDto Device2, Guid Customer);

    private static async Task<Rig> CreateAsync()
    {
        var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var catalog = app.Get<CatalogService>();
        var skinRoomId = (await catalog.SaveResourceAsync(new ResourceDto(Guid.Empty, "Cilt Bakım Odası", ResourceKind.Room, true, null))).Value;
        var device2Id = (await catalog.SaveResourceAsync(new ResourceDto(Guid.Empty, "Lazer Cihazı 2", ResourceKind.Device, true, null))).Value;
        var c = await app.CatalogAsync();
        var saved = await catalog.SaveServiceAsync(c.Laser with { AllowedResourceIds = [c.Room1.Id, c.Room2.Id, c.LaserDevice.Id, device2Id] });
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.ToString() : null);
        var resources = (await catalog.GetResourcesAsync()).Value;
        return new Rig(app, await app.CatalogAsync(), resources.Single(r => r.Id == skinRoomId), resources.Single(r => r.Id == device2Id), await app.AddCustomerAsync());
    }

    private static Task<Result<BookingOutcome>> BookAsync(Rig rig, IReadOnlyList<Guid> resources, int hour, Guid? staff = null) =>
        rig.App.Get<AppointmentService>().CreateAsync(
            new CreateAppointmentRequest(rig.Customer, rig.Catalog.Laser.Id, staff ?? rig.Catalog.Selin.Id, resources, TestApp.Local(hour), null, null, null, null),
            confirmWarnings: true);

    [Fact]
    public async Task Laser_cannot_be_booked_in_the_skin_care_room_even_when_warnings_are_confirmed()
    {
        var rig = await CreateAsync();
        await using var _ = rig.App;
        var r = await BookAsync(rig, [rig.SkinRoom.Id, rig.Catalog.LaserDevice.Id], 10);

        Assert.True(r.IsSuccess);
        Assert.False(r.Value.Saved);
        var issue = Assert.Single(r.Value.Issues, i => i.Code == "resource.not_allowed");
        Assert.Equal(IssueSeverity.Blocking, issue.Severity);
        Assert.Contains("Cilt Bakım Odası", issue.Message, StringComparison.Ordinal);
        Assert.Contains("Lazer Odası 1", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Laser_without_the_device_is_blocked_and_with_room_and_device_it_is_saved()
    {
        var rig = await CreateAsync();
        await using var _ = rig.App;

        var noDevice = await BookAsync(rig, [rig.Catalog.Room1.Id], 10);
        Assert.False(noDevice.Value.Saved);
        Assert.Contains(noDevice.Value.Issues, i => i.Code == "resource.missing" && i.Severity == IssueSeverity.Blocking && i.Message.Contains("cihaz", StringComparison.Ordinal));

        var ok = await BookAsync(rig, [rig.Catalog.Room1.Id, rig.Catalog.LaserDevice.Id], 10);
        Assert.True(ok.Value.Saved, string.Join("; ", ok.Value.Issues.Select(i => i.Message)));
    }

    [Fact]
    public async Task Slot_search_offers_the_other_allowed_room_and_device_when_the_selected_ones_are_busy()
    {
        var rig = await CreateAsync();
        await using var _ = rig.App;
        Assert.True((await BookAsync(rig, [rig.Catalog.Room1.Id, rig.Catalog.LaserDevice.Id], 10)).Value.Saved);

        var appointments = rig.App.Get<AppointmentService>();
        var selected = new[] { rig.Catalog.Room1.Id, rig.Catalog.LaserDevice.Id };
        var day = DateOnly.FromDateTime(TestApp.Local(10));
        var only = (await appointments.GetAvailableSlotsAsync(new SlotQuery(day, rig.Catalog.Laser.Id, rig.Catalog.Derya.Id, selected))).Value;
        Assert.DoesNotContain(only, s => s.LocalStart == new TimeOnly(10, 0));

        var wide = (await appointments.GetAvailableSlotsAsync(new SlotQuery(day, rig.Catalog.Laser.Id, rig.Catalog.Derya.Id, selected, SearchAllowedResources: true))).Value;
        var ten = Assert.Single(wide, s => s.LocalStart == new TimeOnly(10, 0));
        Assert.Equal(new[] { rig.Catalog.Room2.Id, rig.Device2.Id }.Order(), ten.ResourceIds.Order());
        // Free times in the selected room/device keep the selection (tried first).
        var eleven = Assert.Single(wide, s => s.LocalStart == new TimeOnly(11, 0));
        Assert.Equal(selected.Order(), eleven.ResourceIds.Order());
        // The suggestion really books.
        var booked = await BookAsync(rig, ten.ResourceIds, 10, rig.Catalog.Derya.Id);
        Assert.True(booked.Value.Saved, string.Join("; ", booked.Value.Issues.Select(i => i.Message)));
    }

    [Fact]
    public async Task Saving_the_same_links_again_works_and_clearing_them_removes_the_rule()
    {
        var rig = await CreateAsync();
        await using var _ = rig.App;
        var catalog = rig.App.Get<CatalogService>();
        var laser = rig.Catalog.Laser;
        Assert.Equal(4, laser.AllowedResourceIds!.Count);

        var again = await catalog.SaveServiceAsync(laser with { AllowedResourceIds = [rig.Catalog.Room1.Id, rig.Catalog.Room2.Id, rig.Catalog.LaserDevice.Id] });
        Assert.True(again.IsSuccess, again.IsFailure ? again.Error.ToString() : null);
        Assert.Equal(3, (await rig.App.CatalogAsync()).Laser.AllowedResourceIds!.Count);

        Assert.True((await catalog.SaveServiceAsync(laser with { AllowedResourceIds = [] })).IsSuccess);
        Assert.Empty((await rig.App.CatalogAsync()).Laser.AllowedResourceIds!);
        var r = await BookAsync(rig, [rig.SkinRoom.Id], 10);
        Assert.True(r.Value.Saved, string.Join("; ", r.Value.Issues.Select(i => i.Message)));
    }

    [Fact]
    public async Task Editor_selects_an_allowed_room_and_device_and_picking_a_suggestion_switches_room()
    {
        var rig = await CreateAsync();
        await using var _ = rig.App;
        Assert.True((await BookAsync(rig, [rig.Catalog.Room1.Id, rig.Catalog.LaserDevice.Id], 10)).Value.Saved);

        var page = rig.App.Get<AppointmentsViewModel>();
        page.SelectedDate = new DateTime(2026, 11, 12);
        await page.OnNavigatedToAsync(null);
        await page.OpenEditorAsync(null);
        var editor = page.Editor!;
        editor.Resources.Single(r => r.Resource.Id == rig.SkinRoom.Id).IsSelected = true;
        editor.SelectedService = editor.Services.Single(s => s.Name == "Lazer Epilasyon");
        editor.SelectedStaff = editor.Staff.Single(s => s.Id == rig.Catalog.Derya.Id);
        await Task.Delay(200);

        Assert.False(editor.Resources.Single(r => r.Resource.Id == rig.SkinRoom.Id).IsSelected);
        Assert.Contains(editor.Resources, r => r.IsSelected && r.Resource.Kind == ResourceKind.Room);
        Assert.Contains(editor.Resources, r => r.IsSelected && r.Resource.Kind == ResourceKind.Device);
        Assert.True(editor.HasResourceRule);
        Assert.Contains("Lazer Odası 2", editor.ResourceRuleText, StringComparison.Ordinal);

        var ten = editor.Slots.Single(s => s.Time == new TimeOnly(10, 0));
        Assert.NotNull(ten.ResourceHint);
        await editor.PickSlotCommand.ExecuteAsync(ten);
        Assert.Equal("10:00", editor.Time);
        Assert.True(editor.Resources.Single(r => r.Resource.Id == rig.Catalog.Room2.Id).IsSelected);
        Assert.False(editor.Resources.Single(r => r.Resource.Id == rig.Catalog.Room1.Id).IsSelected);

        editor.SelectedCustomer = new CustomerListItem(rig.Customer, 1, "Ayşe Kara", "0532 456 78 90", null, nameof(Vernuko.Customers.CustomerState.Active), null);
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Null(editor.ErrorMessage);
    }

    [Fact]
    public async Task Catalog_page_saves_the_ticked_rooms_and_devices()
    {
        var rig = await CreateAsync();
        await using var _ = rig.App;
        var page = rig.App.Get<CatalogViewModel>();
        await page.OnNavigatedToAsync(null);
        page.SelectedService = page.Services.Single(s => s.Name == "Cilt Bakımı");
        Assert.DoesNotContain(page.ServiceResourceChoices, c => c.IsSelected);

        page.ServiceResourceChoices.Single(c => c.Resource.Id == rig.SkinRoom.Id).IsSelected = true;
        await page.SaveServiceCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        Assert.Equal([rig.SkinRoom.Id], (await rig.App.CatalogAsync()).SkinCare.AllowedResourceIds!);

        page.SelectedService = page.Services.Single(s => s.Name == "Lazer Epilasyon");
        Assert.Equal(4, page.ServiceResourceChoices.Count(c => c.IsSelected));
    }

    [Fact]
    public async Task Setup_links_profile_services_to_matching_rooms_and_devices()
    {
        await using var app = await TestApp.CreateAsync();
        var profile = BusinessProfileCatalog.Get(BusinessProfileId.BeautyAndLaser);
        var result = await app.Get<SetupService>().CompleteAsync(new SetupRequest(
            BusinessProfileId.BeautyAndLaser, "Işıl Güzellik", "0212 555 44 33", null, TimeZones.Turkey, WeeklySchedule.DefaultBusinessWeek(),
            [new SetupStaff("Selin Demir", "Uzman")],
            profile.SuggestedServices.Select(s => new SetupServiceItem(s.Name, s.Category, s.DurationMinutes, 100_000, s.PrepMinutes, s.CleanupMinutes, s.RequiredResource, s.ResourceKeywords)).ToList(),
            profile.SuggestedResources.Select(x => new SetupResource(x.Name, x.Kind)).ToList(),
            "Ayşe Yılmaz", TestApp.AdminUser, TestApp.AdminPassword, false, null));
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.ToString() : null);
        Assert.True((await app.Get<AuthService>().LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);

        var catalog = app.Get<CatalogService>();
        var resources = (await catalog.GetResourcesAsync()).Value.ToDictionary(r => r.Id, r => r.Name);
        var services = (await catalog.GetServicesAsync()).Value;
        string[] Names(string service) => services.Single(s => s.Name == service).AllowedResourceIds!.Select(id => resources[id]).Order().ToArray();

        Assert.Equal(["Lazer Cihazı", "Lazer Odası 1", "Lazer Odası 2"], Names("Lazer Epilasyon"));
        Assert.Equal(["Cilt Bakım Odası"], Names("Cilt Bakımı"));
        Assert.Empty(Names("Kaş Tasarımı")); // no room needed, no rule
        Assert.Empty(Names("Bölgesel İncelme")); // any room (warning only when none is chosen)
    }
}
