using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Common;
using Vernuko.Application.Services;
using Vernuko.Customers;
using Vernuko.Presentation.Common;
using Vernuko.Security;

namespace Vernuko.Presentation.Pages;

/// <summary>
/// Müşteri kartı › Onam ve dosyalar. Files are encrypted at rest; a preview is decrypted only into memory and every
/// view is audited. Exporting a decrypted copy is an explicit "save as" by the user.
/// </summary>
public sealed partial class CustomerRecordsViewModel(ICustomerRecordsService records, IDialogService dialogs, IPlatformServices platform, UserSession session) : ViewModelBase
{
    private const long MaxFileBytes = 15 * 1024 * 1024;

    private Guid? _customerId;

    [ObservableProperty]
    private ConsentTemplateDto? _selectedTemplate;

    [ObservableProperty]
    private Option<ConsentMethod>? _method;

    [ObservableProperty]
    private string? _consentNote;

    [ObservableProperty]
    private CustomerFileDto? _selectedFile;

    [ObservableProperty]
    private byte[]? _previewImage;

    [ObservableProperty]
    private Option<CustomerFileKind>? _newFileKind;

    public ObservableCollection<CustomerConsentDto> Consents { get; } = [];

    public ObservableCollection<CustomerFileDto> Files { get; } = [];

    public ObservableCollection<ConsentTemplateDto> Templates { get; } = [];

    public IReadOnlyList<Option<ConsentMethod>> Methods { get; } =
        Enum.GetValues<ConsentMethod>().Select(m => new Option<ConsentMethod>(CustomerConsent.MethodName(m), m)).ToList();

    public IReadOnlyList<Option<CustomerFileKind>> FileKinds { get; } =
        Enum.GetValues<CustomerFileKind>().Select(k => new Option<CustomerFileKind>(CustomerFile.KindName(k), k)).ToList();

    public bool CanRecordConsent => session.Has(Permission.ConsentsRecord);

    public bool CanViewFiles => session.Has(Permission.CustomerFilesView);

    public bool CanManageFiles => session.Has(Permission.CustomerFilesManage);

    public bool HasPreview => PreviewImage is not null;

    public bool HasNoTemplates => CanRecordConsent && Templates.Count == 0;

    public async Task LoadAsync(Guid? customerId)
    {
        _customerId = customerId;
        Method ??= Methods[0];
        NewFileKind ??= FileKinds[0];
        PreviewImage = null;
        Consents.Clear();
        Files.Clear();
        if (customerId is not { } id)
        {
            return;
        }

        foreach (var c in (await records.GetConsentsAsync(id)).OrEmpty())
        {
            Consents.Add(c);
        }

        if (CanViewFiles)
        {
            foreach (var f in (await records.GetFilesAsync(id)).OrEmpty())
            {
                Files.Add(f);
            }
        }

        if (CanRecordConsent && Templates.Count == 0)
        {
            foreach (var t in (await records.GetTemplatesAsync()).OrEmpty())
            {
                Templates.Add(t);
            }

            SelectedTemplate ??= Templates.FirstOrDefault();
        }

        OnPropertyChanged(string.Empty);
    }

    partial void OnPreviewImageChanged(byte[]? value) => OnPropertyChanged(nameof(HasPreview));

    partial void OnSelectedFileChanged(CustomerFileDto? value) => PreviewImage = null;

    [RelayCommand]
    private Task RecordConsentAsync() => RunAsync(async () =>
    {
        if (_customerId is not { } id || SelectedTemplate is null)
        {
            ErrorMessage = "Önce Ayarlar › Onam formları bölümünden bir form tanımlayın.";
            return;
        }

        var method = Method?.Value ?? ConsentMethod.PaperSigned;
        if (!await dialogs.ConfirmAsync("Onay kaydı", $"\"{SelectedTemplate.Title}\" (sürüm {SelectedTemplate.TextVersion}) için müşterinin onayı \"{CustomerConsent.MethodName(method)}\" olarak kaydedilsin mi?\n\nKayıt tarih, saat ve kaydı yapan kullanıcıyla saklanır.", "Kaydet"))
        {
            return;
        }

        if (Check(await records.RecordConsentAsync(id, SelectedTemplate.Id, method, ConsentNote)))
        {
            ConsentNote = null;
            InfoMessage = "Onay kaydedildi.";
            await LoadAsync(id);
        }
    });

    [RelayCommand]
    private Task RevokeConsentAsync(CustomerConsentDto consent) => RunAsync(async () =>
    {
        var reason = await dialogs.PromptAsync("Onayı geri al", $"\"{consent.Title}\" onayı geri alınacak. Kayıt silinmez, geri alındı olarak işaretlenir. Gerekçe:");
        if (reason is not null && Check(await records.RevokeConsentAsync(consent.Id, reason)))
        {
            InfoMessage = "Onay geri alındı.";
            await LoadAsync(_customerId);
        }
    });

    [RelayCommand]
    private Task AddFileAsync() => RunAsync(async () =>
    {
        if (_customerId is not { } id)
        {
            return;
        }

        var path = await dialogs.PickOpenFileAsync("Dosya ekle", "Fotoğraf ve belgeler|*.jpg;*.jpeg;*.png;*.pdf|Tüm dosyalar|*.*");
        if (path is null)
        {
            return;
        }

        var info = new FileInfo(path);
        if (info.Length > MaxFileBytes)
        {
            ErrorMessage = "Dosya 15 MB'tan büyük olamaz.";
            return;
        }

        var note = await dialogs.PromptAsync("Dosya notu", "Açıklama (ör. 3. seans öncesi) — isteğe bağlı:", string.Empty, required: false);
        var bytes = await platform.ReadFileBytesAsync(path);
        if (Check(await records.AddFileAsync(id, NewFileKind?.Value ?? CustomerFileKind.Photo, Path.GetFileName(path), bytes, note)))
        {
            InfoMessage = "Dosya şifrelenerek kaydedildi. Orijinal dosyayı bilgisayarınızdan silebilirsiniz.";
            await LoadAsync(id);
        }
    });

    [RelayCommand]
    private Task PreviewAsync(CustomerFileDto file) => RunAsync(async () =>
    {
        SelectedFile = file;
        if (!file.IsImage)
        {
            InfoMessage = "Bu dosya türü önizlenemez; \"Dışa aktar\" ile kaydedip açabilirsiniz.";
            return;
        }

        var bytes = await records.ReadFileAsync(file.Id);
        if (Check(bytes))
        {
            PreviewImage = bytes.Value;
        }
    });

    [RelayCommand]
    private Task ExportFileAsync(CustomerFileDto file) => RunAsync(async () =>
    {
        if (!await dialogs.ConfirmAsync("Şifresiz kopya", "Dosyanın ŞİFRESİZ bir kopyası seçeceğiniz konuma kaydedilecek. Kişisel veri içerir; işiniz bitince silin. Devam edilsin mi?", "Devam"))
        {
            return;
        }

        var path = await dialogs.PickSaveFileAsync("Dosyayı dışa aktar", file.OriginalName, "Tüm dosyalar|*.*");
        if (path is null)
        {
            return;
        }

        var bytes = await records.ReadFileAsync(file.Id);
        if (Check(bytes))
        {
            await platform.WriteFileBytesAsync(path, bytes.Value);
            InfoMessage = "Dosya dışa aktarıldı.";
        }
    });

    [RelayCommand]
    private Task DeleteFileAsync(CustomerFileDto file) => RunAsync(async () =>
    {
        if (!await dialogs.ConfirmAsync("Dosyayı sil", $"{file.KindName} ({file.OriginalName}) kalıcı olarak silinsin mi? Eski yedeklerde kalmaya devam eder.", "Sil", destructive: true))
        {
            return;
        }

        if (Check(await records.DeleteFileAsync(file.Id)))
        {
            PreviewImage = null;
            await LoadAsync(_customerId);
        }
    });

    [RelayCommand]
    private void ClosePreview() => PreviewImage = null;
}
