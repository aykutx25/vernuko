using Vernuko.Application.Services;
using Vernuko.Presentation.Pages;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Presentation;

/// <summary>View-model regressions from the general UI review (Kasım 2026), against the real services.</summary>
public class ReviewViewModelTests
{
    [Fact]
    public async Task Typing_another_name_drops_the_earlier_customer_pick_in_the_editor()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var ayse = await app.AddCustomerAsync("Ayşe", "Kara", "0532 456 78 90");
        var page = app.Get<AppointmentsViewModel>();
        await page.OnNavigatedToAsync(null);
        await page.OpenEditorAsync(ayse);
        var editor = page.Editor!;
        Assert.Equal(ayse, editor.SelectedCustomer?.Id);

        editor.CustomerSearch = "Fatma Demir";
        Assert.Null(editor.SelectedCustomer); // Kaydet must not book for Ayşe
        Assert.True(editor.HasUserInput);

        // Replacing a half-filled form asks first; "no" keeps it.
        app.Get<FakeDialogs>().ConfirmAnswer = false;
        await page.OpenEditorAsync(null);
        Assert.Same(editor, page.Editor);
    }

    [Fact]
    public async Task An_unsaved_private_note_does_not_follow_the_user_to_the_next_customer()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var a = await app.AddCustomerAsync("Danışan", "Bir", "0532 111 11 11");
        var b = await app.AddCustomerAsync("Danışan", "İki", "0532 222 22 22");
        var notes = app.Get<PrivateNotesViewModel>();
        await notes.LoadAsync(a);
        notes.NewText = "A için gizli not";
        await notes.LoadAsync(b);
        Assert.Equal(string.Empty, notes.NewText);
        await notes.LoadAsync(b);
        Assert.Empty((await app.Get<PrivateNotesService>().GetAsync(b)).Value.Mine);
    }

    [Fact]
    public async Task Service_materials_list_every_product_even_with_critical_stock_only_ticked()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var page = app.Get<InventoryViewModel>();
        await page.OnNavigatedToAsync(null);
        page.NewCommand.Execute(null);
        page.Name = "Jel";
        page.Unit = "adet";
        page.SalePrice = "100";
        page.MinimumStock = "1";
        await page.SaveCommand.ExecuteAsync(null);
        Assert.Null(page.ErrorMessage);
        var dialogs = app.Get<FakeDialogs>();
        dialogs.PromptAnswers.Enqueue("10");
        dialogs.PromptAnswers.Enqueue("Fatura 1");
        await page.ReceiveCommand.ExecuteAsync(null); // 10 in stock: not critical

        page.LowOnly = true;
        await Task.Delay(200);
        Assert.DoesNotContain(page.Products, p => p.Name == "Jel");
        Assert.Contains(page.MaterialProducts, p => p.Name == "Jel");
    }
}
