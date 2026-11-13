using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

/// <summary>Müşteri kartı › Gizli notlar: only the signed-in specialist's own notes are shown.</summary>
public sealed partial class PrivateNotesViewModel(IPrivateNotesService notes, IDialogService dialogs, UserSession session) : ViewModelBase
{
    private Guid? _customerId;

    [ObservableProperty]
    private string _newText = string.Empty;

    [ObservableProperty]
    private string? _othersText;

    public ObservableCollection<PrivateNoteDto> Notes { get; } = [];

    public bool CanUse => session.Has(Permission.PrivateNotesViewOwn);

    public Task LoadAsync(Guid? customerId) => RunAsync(async () =>
    {
        _customerId = customerId;
        Notes.Clear();
        OthersText = null;
        if (customerId is not { } id || !CanUse)
        {
            return;
        }

        var r = await notes.GetAsync(id);
        if (!Check(r))
        {
            return;
        }

        foreach (var n in r.Value.Mine)
        {
            Notes.Add(n);
        }

        OthersText = r.Value.OthersCount > 0 ? $"Bu müşteride başka uzmanlara ait {r.Value.OthersCount} gizli not var; onları yalnızca yazarları görebilir." : null;
    });

    [RelayCommand]
    private Task AddAsync() => RunAsync(async () =>
    {
        if (_customerId is not { } id)
        {
            return;
        }

        if (Check(await notes.AddAsync(id, NewText)))
        {
            NewText = string.Empty;
            await LoadAsync(id);
            InfoMessage = "Not şifrelenerek kaydedildi.";
        }
    });

    [RelayCommand]
    private Task DeleteAsync(PrivateNoteDto note) => RunAsync(async () =>
    {
        if (!await dialogs.ConfirmAsync("Notu sil", "Bu not kalıcı olarak silinecek (geri alınamaz).", "Sil"))
        {
            return;
        }

        if (Check(await notes.DeleteAsync(note.Id)) && _customerId is { } id)
        {
            await LoadAsync(id);
        }
    });
}
