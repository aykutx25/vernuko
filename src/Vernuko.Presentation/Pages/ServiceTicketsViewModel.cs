using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vernuko.Application;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Payments;
using Vernuko.Presentation.Common;
using Vernuko.Security;
using Vernuko.ServiceDesk;

namespace Vernuko.Presentation.Pages;

/// <summary>
/// Servis fişleri (Teknik Servis mode): the open devices on the left, one ticket on the right — intake, diagnosis,
/// price approval, parts and labour, status, delivery and the printed slips.
/// </summary>
public sealed partial class ServiceTicketsViewModel(IServiceDeskService desk, ICustomerService customers, ICatalogService catalog, IInventoryService inventory, IPlatformServices platform, IDialogService dialogs) : PageViewModel, IShellAware
{
    private readonly Debouncer _searchDebounce = new(TimeSpan.FromMilliseconds(250));
    private readonly Debouncer _customerDebounce = new(TimeSpan.FromMilliseconds(250));
    private readonly Debouncer _serialDebounce = new(TimeSpan.FromMilliseconds(400));
    private string _openKey = Guid.NewGuid().ToString("N");
    private string _deliverKey = Guid.NewGuid().ToString("N");
    private string _depositKey = Guid.NewGuid().ToString("N");
    private bool _keepTicket;

    [ObservableProperty]
    private bool _openOnly = true;

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private TicketSummaryDto? _selected;

    [ObservableProperty]
    private TicketDto? _ticket;

    [ObservableProperty]
    private bool _isEditingIntake;

    // Intake form
    [ObservableProperty]
    private string _customerSearch = string.Empty;

    [ObservableProperty]
    private CustomerListItem? _customer;

    [ObservableProperty]
    private Option<DeviceKind>? _device;

    [ObservableProperty]
    private string _brand = string.Empty;

    [ObservableProperty]
    private string? _model;

    [ObservableProperty]
    private string? _serial;

    [ObservableProperty]
    private string _complaint = string.Empty;

    [ObservableProperty]
    private string? _accessories;

    [ObservableProperty]
    private string? _condition;

    [ObservableProperty]
    private string? _broughtBy;

    [ObservableProperty]
    private bool _onSite;

    [ObservableProperty]
    private string? _siteAddress;

    [ObservableProperty]
    private DateTime? _promisedDate;

    [ObservableProperty]
    private StaffDto? _technician;

    [ObservableProperty]
    private string? _unlockCode;

    // Work on the ticket
    [ObservableProperty]
    private string? _diagnosis;

    [ObservableProperty]
    private string? _workDone;

    [ObservableProperty]
    private string _estimate = "0";

    [ObservableProperty]
    private string _diagnosticFee = "0";

    [ObservableProperty]
    private Option<ApprovalChannel>? _approvalChannel;

    [ObservableProperty]
    private string? _approvedBy;

    [ObservableProperty]
    private string? _statusReason;

    [ObservableProperty]
    private bool _notifyCustomer = true;

    [ObservableProperty]
    private string _partCode = string.Empty;

    [ObservableProperty]
    private string _partQuantity = "1";

    [ObservableProperty]
    private string _laborText = string.Empty;

    [ObservableProperty]
    private string _laborPrice = "0";

    /// <summary>A labour item from the İşçilik list: fills the description and the price (both can still be changed).</summary>
    [ObservableProperty]
    private ServiceDto? _laborItem;

    [ObservableProperty]
    private string _deposit = "0";

    [ObservableProperty]
    private string? _deliveredTo;

    [ObservableProperty]
    private string _payCash = "0";

    [ObservableProperty]
    private string _payCard = "0";

    [ObservableProperty]
    private string? _revealedCode;

    public override string Title => "Servis Fişleri";

    public ShellViewModel? Shell { get; set; }

    public ObservableCollection<TicketSummaryDto> Tickets { get; } = [];

    public ObservableCollection<CustomerListItem> CustomerResults { get; } = [];

    public ObservableCollection<StaffDto> Staff { get; } = [];

    public ObservableCollection<ProductDto> Parts { get; } = [];

    public ObservableCollection<ServiceDto> LaborItems { get; } = [];

    public ObservableCollection<TicketSummaryDto> DeviceHistory { get; } = [];

    public IReadOnlyList<Option<DeviceKind>> Devices { get; } = Enum.GetValues<DeviceKind>().Select(d => new Option<DeviceKind>(ServiceRules.DeviceName(d), d)).ToList();

    public IReadOnlyList<Option<ApprovalChannel>> ApprovalChannels { get; } = Enum.GetValues<ApprovalChannel>().Select(c => new Option<ApprovalChannel>(ServiceRules.ApprovalChannelName(c), c)).ToList();

    public bool CanManage => Shell?.Has(Permission.ServiceTicketsManage) ?? false;

    public bool HasTicket => Ticket is not null;

    public bool IsOpenTicket => Ticket is { DeliveredAtUtc: null };

    public bool ShowDetail => Ticket is not null && !IsEditingIntake;

    public bool ShowEmpty => Ticket is null && !IsEditingIntake;

    public bool IsApprovalPending => Ticket?.Approval == ApprovalState.Pending;

    public string ApprovalText => Ticket is null ? string.Empty : Ticket.Approval switch
    {
        ApprovalState.Pending => $"Fiyat teklifi {Formatting.Money(Ticket.EstimateMinor ?? 0)}: müşteri onayı bekleniyor.",
        ApprovalState.Approved => $"Müşteri {Formatting.Money(Ticket.EstimateMinor ?? 0)} teklifini onayladı" + (Ticket.ApprovalChannel is { } c ? $" ({ServiceRules.ApprovalChannelName(c)}" + (string.IsNullOrWhiteSpace(Ticket.ApprovalBy) ? ")." : $", {Ticket.ApprovalBy}).") : "."),
        ApprovalState.Declined => "Müşteri fiyatı onaylamadı: cihaz onarılmadan iade edilir" + (Ticket.DiagnosticFeeMinor > 0 ? $", arıza tespit ücreti {Formatting.Money(Ticket.DiagnosticFeeMinor)}." : "."),
        _ => "Fiyat teklifi verilmedi. Tutar belli değilse onarıma başlamadan teklif verip onay alın.",
    };

    public string WarrantyText => Ticket switch
    {
        { WarrantyOfNumber: { } n } => $"Garanti kapsamında ({n} numaralı fişin onarımı): işçilik ücretlendirilmez.",
        { WarrantyCandidate: { } w } => $"Bu cihaz {w.DeliveredOn:dd.MM.yyyy} tarihinde {w.Number} ile onarıldı; aynı arıza {w.SameFaultUntil:dd.MM.yyyy} tarihine kadar ücretsiz onarılmalı.",
        _ => string.Empty,
    };

    public bool CanLinkWarranty => Ticket is { WarrantyOfNumber: null, WarrantyCandidate: not null, DeliveredAtUtc: null };

    public string AmountDueText => Ticket is null ? string.Empty
        : $"Tutar {Formatting.Money(Ticket.AmountDueMinor)}" + (Ticket.DepositsMinor > 0 ? $", alınan kapora {Formatting.Money(Ticket.DepositsMinor)}, kalan {Formatting.Money(Math.Max(0, Ticket.AmountDueMinor - Ticket.DepositsMinor))}" : string.Empty);

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        Device ??= Devices[0];
        ApprovalChannel ??= ApprovalChannels[0];
        Staff.Clear();
        foreach (var s in (await catalog.GetStaffAsync()).OrEmpty())
        {
            Staff.Add(s);
        }

        Parts.Clear();
        foreach (var p in (await inventory.GetProductsAsync()).OrEmpty())
        {
            Parts.Add(p);
        }

        LaborItems.Clear();
        foreach (var s in (await catalog.GetServicesAsync()).OrEmpty())
        {
            LaborItems.Add(s);
        }

        await LoadListAsync();
    });

    partial void OnOpenOnlyChanged(bool value) => _ = LoadListAsync();

    partial void OnLaborItemChanged(ServiceDto? value)
    {
        if (value is not null)
        {
            LaborText = value.Name;
            LaborPrice = (value.PriceMinor / 100m).ToString("0.00", TurkishText.Culture);
        }
    }

    partial void OnSearchChanged(string value) => _ = _searchDebounce.RunAsync(_ => LoadListAsync());

    partial void OnSelectedChanged(TicketSummaryDto? value)
    {
        if (!_keepTicket)
        {
            _ = ShowAsync(value?.Id);
        }
    }

    partial void OnTicketChanged(TicketDto? value)
    {
        OnPropertyChanged(nameof(HasTicket));
        OnPropertyChanged(nameof(IsOpenTicket));
        OnPropertyChanged(nameof(AmountDueText));
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(IsApprovalPending));
        OnPropertyChanged(nameof(ApprovalText));
        OnPropertyChanged(nameof(WarrantyText));
        OnPropertyChanged(nameof(CanLinkWarranty));
        RevealedCode = null;
        if (value?.EstimateMinor is { } e)
        {
            Estimate = (e / 100m).ToString("0.00", TurkishText.Culture);
            DiagnosticFee = (value.DiagnosticFeeMinor / 100m).ToString("0.00", TurkishText.Culture);
        }

        Diagnosis = value?.Diagnosis;
        WorkDone = value?.WorkDone;
        DeliveredTo = value?.Intake.BroughtBy;
    }

    partial void OnIsEditingIntakeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetail));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    partial void OnCustomerSearchChanged(string value) => _ = _customerDebounce.RunAsync(async _ =>
    {
        CustomerResults.Clear();
        if (value.Trim().Length < 2 || (Customer is not null && value == Customer.FullName))
        {
            return;
        }

        foreach (var c in (await customers.SearchAsync(value, take: 6)).OrEmpty())
        {
            CustomerResults.Add(c);
        }
    });

    partial void OnSerialChanged(string? value) => _ = _serialDebounce.RunAsync(async _ =>
    {
        DeviceHistory.Clear();
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 4)
        {
            return;
        }

        foreach (var t in (await desk.GetDeviceHistoryAsync(value)).OrEmpty())
        {
            DeviceHistory.Add(t);
        }
    });

    [RelayCommand]
    private void PickCustomer(CustomerListItem c)
    {
        Customer = c;
        CustomerSearch = c.FullName;
        BroughtBy ??= c.FullName;
        CustomerResults.Clear();
    }

    [RelayCommand]
    private void NewTicket()
    {
        Selected = null;
        Ticket = null;
        IsEditingIntake = true;
        Customer = null;
        CustomerSearch = string.Empty;
        Device = Devices[0];
        Brand = string.Empty;
        Model = null;
        Serial = null;
        Complaint = string.Empty;
        Accessories = null;
        Condition = null;
        BroughtBy = null;
        OnSite = false;
        SiteAddress = null;
        PromisedDate = null;
        Technician = null;
        UnlockCode = null;
        _openKey = Guid.NewGuid().ToString("N");
    }

    [RelayCommand]
    private void EditIntake()
    {
        if (Ticket is null)
        {
            return;
        }

        var i = Ticket.Intake;
        IsEditingIntake = true;
        Customer = new CustomerListItem(Ticket.CustomerId, 0, Ticket.Summary.CustomerName, Ticket.Summary.CustomerPhone, null, string.Empty, null);
        CustomerSearch = Ticket.Summary.CustomerName;
        Device = Devices.First(d => d.Value == i.Device);
        Brand = i.Brand;
        Model = i.Model;
        Serial = i.SerialNumber;
        Complaint = i.Complaint;
        Accessories = i.Accessories;
        Condition = i.Condition;
        BroughtBy = i.BroughtBy;
        OnSite = i.OnSite;
        SiteAddress = i.SiteAddress;
        PromisedDate = i.PromisedDate?.ToDateTime(TimeOnly.MinValue);
        Technician = Staff.FirstOrDefault(s => s.Id == i.TechnicianStaffId);
        UnlockCode = null;
    }

    [RelayCommand]
    private void CancelIntake() => IsEditingIntake = false;

    [RelayCommand]
    private Task SaveIntakeAsync() => RunAsync(async () =>
    {
        if (Customer is null)
        {
            ErrorMessage = "Müşteriyi yazıp listeden seçin (yoksa önce Müşteriler ekranından ekleyin).";
            return;
        }

        var intake = new TicketIntakeRequest(Customer.Id, Device?.Value ?? DeviceKind.Other, Brand, Model, Serial, Complaint, Accessories, Condition, BroughtBy,
            OnSite, SiteAddress, PromisedDate is { } d ? DateOnly.FromDateTime(d) : null, Technician?.Id);
        var r = Ticket is null
            ? await desk.OpenTicketAsync(new OpenTicketRequest(intake, UnlockCode, _openKey))
            : await desk.UpdateIntakeAsync(Ticket.Summary.Id, intake);
        if (!Check(r))
        {
            return;
        }

        if (Ticket is not null && !string.IsNullOrWhiteSpace(UnlockCode))
        {
            r = await desk.SetUnlockCodeAsync(r.Value.Summary.Id, UnlockCode);
        }

        IsEditingIntake = false;
        UnlockCode = null;
        await LoadListAsync();
        Ticket = r.Value;
        InfoMessage = $"{r.Value.Summary.Number} kaydedildi. Kabul belgesini yazdırıp müşteriye verin.";
    });

    [RelayCommand]
    private Task SaveDiagnosisAsync() => Apply(id => desk.SetDiagnosisAsync(id, Diagnosis, WorkDone), "Kaydedildi.");

    [RelayCommand]
    private Task SetEstimateAsync() => Apply(id => desk.SetEstimateAsync(id, Minor(Estimate), Minor(DiagnosticFee)), "Fiyat teklifi kaydedildi; müşteri onayı bekleniyor.");

    [RelayCommand]
    private Task ApproveAsync() => Apply(id => desk.RecordApprovalAsync(id, true, ApprovalChannel?.Value ?? ServiceDesk.ApprovalChannel.Phone, ApprovedBy), "Onay kaydedildi; cihaz onarımda.");

    [RelayCommand]
    private Task DeclineAsync() => Apply(id => desk.RecordApprovalAsync(id, false, ApprovalChannel?.Value ?? ServiceDesk.ApprovalChannel.Phone, ApprovedBy), "Müşteri onaylamadı; cihaz onarılmadan iade edilecek.");

    [RelayCommand]
    private Task WithdrawEstimateAsync() => Apply(id => desk.WithdrawEstimateAsync(id), "Fiyat teklifi geri çekildi.");

    [RelayCommand]
    private Task MoveAsync(string status) => Apply(id => desk.ChangeStatusAsync(id, Enum.Parse<TicketStatus>(status), StatusReason, NotifyCustomer), "Durum güncellendi.");

    [RelayCommand]
    private Task AddPartAsync() => RunAsync(async () =>
    {
        if (Ticket is null)
        {
            return;
        }

        var t = PartCode.Trim();
        var part = Parts.FirstOrDefault(p => string.Equals(p.Barcode, t, StringComparison.Ordinal) || string.Equals(p.Sku, t, StringComparison.OrdinalIgnoreCase))
            ?? Parts.Where(p => p.Name.Contains(t, StringComparison.CurrentCultureIgnoreCase)).Take(2).ToList() switch { [var only] => only, _ => null };
        if (part is null)
        {
            ErrorMessage = "Parçayı barkodu, kodu ya da adıyla bulamadım (birden fazla eşleşme olabilir).";
            return;
        }

        if (!int.TryParse(PartQuantity, NumberStyles.Integer, TurkishText.Culture, out var qty))
        {
            ErrorMessage = "Miktar tam sayı olmalı.";
            return;
        }

        var r = await desk.AddPartAsync(Ticket.Summary.Id, part.Id, qty, null);
        if (Check(r))
        {
            Ticket = r.Value;
            PartCode = string.Empty;
            PartQuantity = "1";
        }
    });

    [RelayCommand]
    private Task AddLaborAsync() => Apply(id => desk.AddLaborAsync(id, LaborText, Minor(LaborPrice), Technician?.Id ?? Ticket?.Intake.TechnicianStaffId), "İşçilik eklendi.", () =>
    {
        LaborItem = null;
        LaborText = string.Empty;
        LaborPrice = "0";
    });

    [RelayCommand]
    private Task RemoveLineAsync(TicketLineDto line) => Apply(id => desk.RemoveLineAsync(id, line.Id), "Satır kaldırıldı.");

    [RelayCommand]
    private Task TakeDepositAsync() => Apply(id => desk.TakeDepositAsync(id, Minor(Deposit), PaymentMethod.Cash, _depositKey), "Kapora alındı.", () =>
    {
        Deposit = "0";
        _depositKey = Guid.NewGuid().ToString("N");
    });

    [RelayCommand]
    private Task LinkWarrantyAsync() => Apply(id => desk.LinkWarrantyAsync(id, Ticket?.WarrantyCandidate?.TicketId), "Garanti kapsamına alındı: işçilik ücretlendirilmez.");

    [RelayCommand]
    private Task DeliverAsync() => Apply(id => desk.DeliverAsync(new DeliverTicketRequest(id, DeliveredTo ?? string.Empty, Minor(PayCash), Minor(PayCard), 0, _deliverKey)), "Teslim edildi. Servis fişini yazdırıp müşteriye verin.", () =>
    {
        PayCash = "0";
        PayCard = "0";
        _deliverKey = Guid.NewGuid().ToString("N");
    });

    [RelayCommand]
    private Task RevealCodeAsync() => RunAsync(async () =>
    {
        if (Ticket is null || !await dialogs.ConfirmAsync("Cihaz şifresi", "Şifre ekranda gösterilecek ve görüntüleme kaydedilecek. Devam edilsin mi?"))
        {
            return;
        }

        var r = await desk.RevealUnlockCodeAsync(Ticket.Summary.Id);
        if (Check(r))
        {
            RevealedCode = r.Value;
        }
    });

    [RelayCommand]
    private Task PrintAsync(string kind) => RunAsync(async () =>
    {
        if (Ticket is null)
        {
            return;
        }

        var r = await desk.GetSlipAsync(Ticket.Summary.Id, Enum.Parse<SlipKind>(kind));
        if (Check(r) && platform.PrintServiceSlip(r.Value))
        {
            InfoMessage = "Yazdırıldı.";
        }
    });

    private Task Apply(Func<Guid, Task<Result<TicketDto>>> action, string done, Action? after = null) => RunAsync(async () =>
    {
        if (Ticket is null)
        {
            return;
        }

        var r = await action(Ticket.Summary.Id);
        if (!Check(r))
        {
            return;
        }

        Ticket = r.Value;
        after?.Invoke();
        InfoMessage = done;
        await LoadListAsync();
    });

    private async Task ShowAsync(Guid? id)
    {
        if (id is null)
        {
            return;
        }

        IsEditingIntake = false;
        var r = await desk.GetTicketAsync(id.Value);
        Ticket = r.IsSuccess ? r.Value : null;
    }

    private async Task LoadListAsync()
    {
        var keep = Selected?.Id;
        Tickets.Clear();
        foreach (var t in (await desk.GetTicketsAsync(OpenOnly, Search)).OrEmpty())
        {
            Tickets.Add(t);
        }

        if (keep is { } id && Tickets.FirstOrDefault(t => t.Id == id) is { } again)
        {
            _keepTicket = true; // re-select the refreshed row without fetching the ticket again
            Selected = again;
            _keepTicket = false;
        }
    }

    private static long Minor(string? text) => string.IsNullOrWhiteSpace(text) ? 0 : Money.Parse(text) is { IsSuccess: true } m ? m.Value.Minor : -1;
}
