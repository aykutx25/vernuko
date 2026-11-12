using Vernuko.Core;

namespace Vernuko.Customers;

public enum CustomerState
{
    Active,
    Archived,
    Anonymized,
}

/// <summary>
/// Customer card shared by every vertical (the UI may call it Hasta / Danışan / Müvekkil ...).
/// Sensitive fields (national id) are optional and only shown to users with the sensitive-data permission.
/// </summary>
public sealed class Customer : Entity
{
    public const int MaxNameLength = 80;
    public const int MaxNotesLength = 4000;

    private Customer()
    {
    }

    private Customer(int number, string firstName, string lastName)
    {
        Number = number;
        FirstName = firstName;
        LastName = lastName;
        State = CustomerState.Active;
        AllowSms = true;
        AllowWhatsApp = true;
        AllowEmail = true;
        RefreshSearchKey();
    }

    /// <summary>Sequential, human friendly number unique within the database.</summary>
    public int Number { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";

    /// <summary>E.164 phone, e.g. +905324567890.</summary>
    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    public string? Address { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Optional T.C. kimlik no. Never required by default.</summary>
    public string? NationalId { get; private set; }

    /// <summary>Firma müşterisi (danışmanlık, muhasebe, hukuk): unvan, vergi dairesi, VKN (10) veya TCKN (11).</summary>
    public string? CompanyName { get; private set; }

    public string? TaxOffice { get; private set; }

    public string? TaxNumber { get; private set; }

    /// <summary>Operational messages (reminders) channel preferences.</summary>
    public bool AllowSms { get; private set; }

    public bool AllowWhatsApp { get; private set; }

    public bool AllowEmail { get; private set; }

    /// <summary>Commercial-message (marketing) consent is separate from operational reminders (İYS).</summary>
    public bool MarketingConsent { get; private set; }

    public DateTime? MarketingConsentAtUtc { get; private set; }

    public string? MarketingConsentSource { get; private set; }

    public CustomerState State { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public DateTime? AnonymizedAtUtc { get; private set; }

    /// <summary>Folded lowercase name + phone digits for fast LIKE searches.</summary>
    public string SearchKey { get; private set; } = string.Empty;

    public string? DisplayPhone => Phone is null ? null : PhoneNumber.Parse(Phone) is { IsSuccess: true } p ? p.Value.ToDisplayString() : Phone;

    public static Result<Customer> Create(int number, CustomerDetails details)
    {
        var validated = Validate(details);
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        var customer = new Customer(number, string.Empty, string.Empty);
        customer.Apply(validated.Value);
        return customer;
    }

    public Result Update(CustomerDetails details)
    {
        if (State == CustomerState.Anonymized)
        {
            return Error.Validation("customer.anonymized", "Anonimleştirilmiş müşteri kaydı düzenlenemez.");
        }

        var validated = Validate(details);
        if (validated.IsFailure)
        {
            return validated.Error;
        }

        Apply(validated.Value);
        return Result.Success();
    }

    public void SetContactPreferences(bool sms, bool whatsApp, bool email)
    {
        AllowSms = sms;
        AllowWhatsApp = whatsApp;
        AllowEmail = email;
    }

    public void SetMarketingConsent(bool consent, DateTime utcNow, string source)
    {
        MarketingConsent = consent;
        MarketingConsentAtUtc = Guard.Utc(utcNow);
        MarketingConsentSource = source;
    }

    public Result Archive(DateTime utcNow)
    {
        if (State != CustomerState.Active)
        {
            return Error.Validation("customer.not_active", "Yalnızca aktif müşteriler arşivlenebilir.");
        }

        State = CustomerState.Archived;
        ArchivedAtUtc = Guard.Utc(utcNow);
        return Result.Success();
    }

    public Result Restore()
    {
        if (State != CustomerState.Archived)
        {
            return Error.Validation("customer.not_archived", "Müşteri arşivde değil.");
        }

        State = CustomerState.Active;
        ArchivedAtUtc = null;
        return Result.Success();
    }

    /// <summary>
    /// Irreversibly removes personal data while keeping the record so appointment, package and payment history stay
    /// consistent (statistics, financial records). The financial retention period must be assessed legally.
    /// </summary>
    public void Anonymize(DateTime utcNow)
    {
        FirstName = "Anonim";
        LastName = $"Müşteri #{Number}";
        Phone = null;
        Email = null;
        BirthDate = null;
        Address = null;
        Notes = null;
        NationalId = null;
        CompanyName = null; // a sole proprietor's trade name and tax number are personal data too
        TaxOffice = null;
        TaxNumber = null;
        AllowSms = false;
        AllowWhatsApp = false;
        AllowEmail = false;
        MarketingConsent = false;
        MarketingConsentAtUtc = null;
        MarketingConsentSource = null;
        State = CustomerState.Anonymized;
        AnonymizedAtUtc = Guard.Utc(utcNow);
        RefreshSearchKey();
    }

    private static Result<CustomerDetails> Validate(CustomerDetails d)
    {
        var first = TurkishText.ToNameCase(d.FirstName);
        var last = TurkishText.ToNameCase(d.LastName);
        if (first.Length == 0)
        {
            return Error.Validation("customer.first_name", "Ad zorunludur.");
        }

        if (first.Length > MaxNameLength || last.Length > MaxNameLength)
        {
            return Error.Validation("customer.name_length", $"Ad ve soyad en fazla {MaxNameLength} karakter olabilir.");
        }

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(d.Phone))
        {
            var parsed = PhoneNumber.Parse(d.Phone);
            if (parsed.IsFailure)
            {
                return parsed.Error;
            }

            phone = parsed.Value.E164;
        }

        string? email = null;
        if (!string.IsNullOrWhiteSpace(d.Email))
        {
            email = d.Email.Trim();
            var at = email.IndexOf('@', StringComparison.Ordinal);
            if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1 || !email[(at + 1)..].Contains('.', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal))
            {
                return Error.Validation("customer.email", "E-posta adresi geçersiz.");
            }
        }

        string? nationalId = null;
        if (!string.IsNullOrWhiteSpace(d.NationalId))
        {
            nationalId = d.NationalId.Trim();
            if (!NationalIdValidator.IsValid(nationalId))
            {
                return Error.Validation("customer.national_id", "T.C. kimlik numarası geçersiz.");
            }
        }

        string? taxNumber = null;
        if (!string.IsNullOrWhiteSpace(d.TaxNumber))
        {
            taxNumber = new string(d.TaxNumber.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (!(taxNumber.Length == 10 ? TaxNumberValidator.IsValidVkn(taxNumber) : NationalIdValidator.IsValid(taxNumber)))
            {
                return Error.Validation("customer.tax_number", "Vergi numarası geçersiz: firma için 10 haneli VKN, şahıs şirketi için 11 haneli T.C. kimlik numarası girin.");
            }
        }

        var company = string.IsNullOrWhiteSpace(d.CompanyName) ? null : d.CompanyName.Trim();
        var taxOffice = string.IsNullOrWhiteSpace(d.TaxOffice) ? null : d.TaxOffice.Trim();
        if (company is { Length: > 200 } || taxOffice is { Length: > 100 })
        {
            return Error.Validation("customer.company_length", "Firma unvanı en fazla 200, vergi dairesi en fazla 100 karakter olabilir.");
        }

        if (d.BirthDate is { } birth && (birth.Year < 1900 || birth > DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            return Error.Validation("customer.birth_date", "Doğum tarihi geçersiz.");
        }

        var notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim();
        if (notes is { Length: > MaxNotesLength })
        {
            return Error.Validation("customer.notes", $"Notlar en fazla {MaxNotesLength} karakter olabilir.");
        }

        return d with
        {
            FirstName = first,
            LastName = last,
            Phone = phone,
            Email = email,
            NationalId = nationalId,
            CompanyName = company,
            TaxOffice = taxOffice,
            TaxNumber = taxNumber,
            Notes = notes,
            Address = string.IsNullOrWhiteSpace(d.Address) ? null : d.Address.Trim(),
        };
    }

    private void Apply(CustomerDetails d)
    {
        FirstName = d.FirstName;
        LastName = d.LastName;
        Phone = d.Phone;
        Email = d.Email;
        BirthDate = d.BirthDate;
        Address = d.Address;
        Notes = d.Notes;
        NationalId = d.NationalId;
        CompanyName = d.CompanyName;
        TaxOffice = d.TaxOffice;
        TaxNumber = d.TaxNumber;
        RefreshSearchKey();
    }

    private void RefreshSearchKey()
    {
        var phoneDigits = Phone is null ? string.Empty : Phone.TrimStart('+');
        var localDigits = Phone is { Length: 13 } && Phone.StartsWith("+90", StringComparison.Ordinal) ? "0" + Phone[3..] : string.Empty;
        SearchKey = string.Join(' ', new[] { TurkishText.ToSearchKey(FullName), phoneDigits, localDigits, Number.ToString(System.Globalization.CultureInfo.InvariantCulture), CompanyName is null ? string.Empty : TurkishText.ToSearchKey(CompanyName), TaxNumber ?? string.Empty }
            .Where(s => s.Length > 0));
    }
}

public sealed record CustomerDetails(
    string FirstName,
    string LastName,
    string? Phone,
    string? Email = null,
    DateOnly? BirthDate = null,
    string? Address = null,
    string? Notes = null,
    string? NationalId = null,
    string? CompanyName = null,
    string? TaxOffice = null,
    string? TaxNumber = null);
