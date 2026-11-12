using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Messaging;
using Vernuko.Presentation.Common;

namespace Vernuko.Presentation.Pages;

/// <summary>
/// Ayarlar › Mesajlaşma: automatic appointment reminders through the business's own SMS (İleti Merkezi) or
/// WhatsApp Cloud API account. Everything is off and the monthly limit is 0 until the business changes it.
/// </summary>
public sealed partial class MessagingSettingsViewModel(IAutoMessagingService messaging) : ViewModelBase
{
    [ObservableProperty]
    private bool _remindersEnabled;

    [ObservableProperty]
    private Option<MessageChannel>? _channel;

    [ObservableProperty]
    private string _hoursBefore = "24";

    [ObservableProperty]
    private string _quietStart = "21:00";

    [ObservableProperty]
    private string _quietEnd = "09:00";

    [ObservableProperty]
    private string _monthlyLimit = "0";

    [ObservableProperty]
    private string _smsUnitPrice = "0";

    [ObservableProperty]
    private string? _smsSender;

    [ObservableProperty]
    private string _smsKey = string.Empty;

    [ObservableProperty]
    private string _smsHash = string.Empty;

    [ObservableProperty]
    private string? _waPhoneNumberId;

    [ObservableProperty]
    private string _waToken = string.Empty;

    [ObservableProperty]
    private string? _waTemplate = "randevu_hatirlatma";

    [ObservableProperty]
    private string _waLanguage = "tr";

    [ObservableProperty]
    private string _waApiVersion = AutoMessagingService.DefaultWhatsAppApiVersion;

    [ObservableProperty]
    private string? _testPhone;

    [ObservableProperty]
    private Option<MessageChannel>? _testChannel;

    [ObservableProperty]
    private string _monthText = string.Empty;

    [ObservableProperty]
    private MessagingSettings? _current;

    public IReadOnlyList<Option<MessageChannel>> Channels { get; } =
    [
        new("SMS (İleti Merkezi)", MessageChannel.Sms), new("WhatsApp Cloud API", MessageChannel.WhatsApp),
    ];

    public ObservableCollection<OutboxItem> Outbox { get; } = [];

    public string SmsCredentialText => Current?.SmsCredentialsSaved == true ? "API anahtarı ve hash kayıtlı (değiştirmek için yeniden girin)." : "API anahtarı ve hash henüz girilmedi.";

    public string WhatsAppCredentialText => Current?.WhatsAppTokenSaved == true ? "Erişim anahtarı (token) kayıtlı (değiştirmek için yeniden girin)." : "Erişim anahtarı (token) henüz girilmedi.";

    public string TemplateHelp =>
        "Meta panelinde \"Utility\" kategorisinde, Türkçe (tr) bir şablon oluşturup onaylatın. Gövde dört sıralı parametre içermeli, örnek: " +
        "\"Merhaba {{1}}, {{2}} saat {{3}} randevunuzu hatırlatırız. {{4}}\" ({{1}} ad, {{2}} tarih, {{3}} saat, {{4}} işletme adı). Kampanya veya indirim metni eklemeyin.";

    public string CostNote =>
        "Mesaj ücretleri kendi sağlayıcı hesabınıza yansır; Vernuko mesaj satmaz ve aracılık etmez. Aylık limit 0 iken hiçbir mesaj gönderilmez. " +
        "Hatırlatmalar yalnızca Vernuko açıkken gönderilir; sessiz saatlerde gönderim yapılmaz. Netgsm ve Verimor desteği henüz yok.";

    public async Task LoadAsync()
    {
        var r = await messaging.GetSettingsAsync();
        if (r.IsFailure)
        {
            return;
        }

        var s = r.Value;
        Current = s;
        RemindersEnabled = s.RemindersEnabled;
        Channel = Channels.First(c => c.Value == s.ReminderChannel);
        TestChannel ??= Channel;
        HoursBefore = s.ReminderHoursBefore.ToString(CultureInfo.InvariantCulture);
        QuietStart = s.QuietStart.ToString("HH:mm", CultureInfo.InvariantCulture);
        QuietEnd = s.QuietEnd.ToString("HH:mm", CultureInfo.InvariantCulture);
        MonthlyLimit = s.MonthlyLimit.ToString(CultureInfo.InvariantCulture);
        SmsUnitPrice = (s.SmsUnitPriceMinor / 100m).ToString("0.##", TurkishText.Culture);
        SmsSender = s.SmsSender;
        WaPhoneNumberId = s.WhatsAppPhoneNumberId;
        WaTemplate = s.WhatsAppTemplate ?? WaTemplate;
        WaLanguage = s.WhatsAppLanguage;
        WaApiVersion = s.WhatsAppApiVersion;
        SmsKey = SmsHash = WaToken = string.Empty;
        await RefreshOutboxAsync();
        OnPropertyChanged(nameof(SmsCredentialText));
        OnPropertyChanged(nameof(WhatsAppCredentialText));
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (!int.TryParse(HoursBefore, out var hours) || !int.TryParse(MonthlyLimit, out var limit))
        {
            ErrorMessage = "Saat ve limit tam sayı olmalıdır.";
            return;
        }

        var price = Money.Parse(string.IsNullOrWhiteSpace(SmsUnitPrice) ? "0" : SmsUnitPrice);
        if (!Check(price))
        {
            return;
        }

        var update = new MessagingSettingsUpdate(RemindersEnabled, Channel?.Value ?? MessageChannel.Sms, hours, QuietStart, QuietEnd, limit, price.Value.Minor,
            SmsSender, SmsKey, SmsHash, WaPhoneNumberId, WaToken, WaTemplate, WaLanguage, WaApiVersion);
        if (Check(await messaging.SaveSettingsAsync(update)))
        {
            await LoadAsync();
            InfoMessage = RemindersEnabled && limit == 0
                ? "Kaydedildi. Aylık limit 0 olduğu için henüz mesaj gönderilmeyecek."
                : "Mesajlaşma ayarları kaydedildi.";
        }
    });

    [RelayCommand]
    private Task SendTestAsync() => RunAsync(async () =>
    {
        var r = await messaging.SendTestAsync(TestChannel?.Value ?? MessageChannel.Sms, TestPhone ?? string.Empty);
        if (Check(r))
        {
            InfoMessage = r.Value;
        }

        await RefreshOutboxAsync();
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(RefreshOutboxAsync);

    private async Task RefreshOutboxAsync()
    {
        Outbox.Clear();
        foreach (var m in (await messaging.GetOutboxAsync(100)).OrEmpty())
        {
            Outbox.Add(m);
        }

        if ((await messaging.GetMonthAsync()) is { IsSuccess: true } month)
        {
            var m = month.Value;
            MonthText = $"Bu ay: {m.Sent} gönderildi · {m.Failed} gönderilemedi · {m.Pending} bekliyor · limit {m.Limit}"
                        + (m.EstimatedCostMinor > 0 ? $" · tahmini SMS maliyeti {Formatting.Money(m.EstimatedCostMinor)}" : string.Empty);
        }
    }
}
