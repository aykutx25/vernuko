using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Security;

namespace Vernuko.Tests.Domain;

public class CoreTests
{
    [Theory]
    [InlineData("0532 456 78 90", "+905324567890")]
    [InlineData("5324567890", "+905324567890")]
    [InlineData("+90 (532) 456-78-90", "+905324567890")]
    [InlineData("905324567890", "+905324567890")]
    [InlineData("0090 532 456 78 90", "+905324567890")]
    [InlineData("0212 555 44 33", "+902125554433")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    public void Phone_numbers_are_normalised_to_E164(string input, string expected)
    {
        var r = PhoneNumber.Parse(input);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(expected, r.Value.E164);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0132 456 78 90")]
    [InlineData("abc")]
    [InlineData("")]
    public void Invalid_phone_numbers_are_rejected(string input) => Assert.True(PhoneNumber.Parse(input).IsFailure);

    [Fact]
    public void Turkish_phone_display_format() =>
        Assert.Equal("0532 456 78 90", PhoneNumber.Parse("+905324567890").Value.ToDisplayString());

    [Theory]
    [InlineData("Ayşe", "ayse")]
    [InlineData("AYŞE", "ayse")]
    [InlineData("IŞIK", "isik")]
    [InlineData("İsmail Çağlar", "ismail caglar")]
    [InlineData("  Gül   Öztürk ", "gul ozturk")]
    [InlineData("ÜMİT ŞAHİN", "umit sahin")]
    public void Search_keys_fold_Turkish_characters(string input, string expected) => Assert.Equal(expected, TurkishText.ToSearchKey(input));

    [Theory]
    [InlineData("ayşe IŞIK", "Ayşe Işık")]
    [InlineData("İSMAİL", "İsmail")]
    [InlineData("ılgaz", "Ilgaz")]
    public void Names_are_title_cased_with_Turkish_rules(string input, string expected) => Assert.Equal(expected, TurkishText.ToNameCase(input));

    [Theory]
    [InlineData("1.250,50", 125050)]
    [InlineData("1250", 125000)]
    [InlineData("₺ 99,9", 9990)]
    [InlineData("0,01", 1)]
    public void Money_parses_Turkish_input(string input, long minor) => Assert.Equal(minor, Money.Parse(input).Value.Minor);

    [Theory]
    [InlineData("-5")]
    [InlineData("1,005")]
    [InlineData("on lira")]
    public void Money_rejects_invalid_input(string input) => Assert.True(Money.Parse(input).IsFailure);

    [Fact]
    public void Money_formats_in_Turkish() => Assert.Equal("₺1.250,50", new Money(125050).ToDisplayString());

    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000147", false)]
    [InlineData("00000000000", false)]
    [InlineData("1234567890", false)]
    public void National_id_checksum(string id, bool valid) => Assert.Equal(valid, NationalIdValidator.IsValid(id));

    [Fact]
    public void Password_hash_roundtrip_and_tamper()
    {
        var hasher = new PasswordHasher(10_000);
        var hash = hasher.Hash("Çok-Güçlü-Parola");
        Assert.True(hasher.Verify("Çok-Güçlü-Parola", hash));
        Assert.False(hasher.Verify("çok-güçlü-parola", hash));
        Assert.False(hasher.Verify("x", hash.Replace("v1$", "v9$", StringComparison.Ordinal)));
        Assert.True(new PasswordHasher(20_000).NeedsRehash(hash));
        Assert.NotEqual(hash, hasher.Hash("Çok-Güçlü-Parola")); // random salt
    }

    [Theory]
    [InlineData("kisa", false)]
    [InlineData("12345678", false)]
    [InlineData("aaaaaaaaaa", false)]
    [InlineData("Lazer-Merkezi-77", true)]
    public void Password_policy(string password, bool ok) => Assert.Equal(ok, PasswordPolicy.Validate(password).IsSuccess);

    [Fact]
    public void Role_defaults_keep_finance_away_from_secretaries()
    {
        var secretary = RoleDefaults.For(SystemRole.Secretary);
        Assert.Contains(Permission.AppointmentsManage, secretary);
        Assert.DoesNotContain(Permission.ReportsFinancial, secretary);
        Assert.DoesNotContain(Permission.PaymentsReverse, secretary);
        Assert.DoesNotContain(Permission.PrivateNotesViewOwn, secretary);
        Assert.Contains(Permission.PrivateNotesViewOwn, RoleDefaults.For(SystemRole.Specialist));
    }

    [Fact]
    public void User_lockout_after_repeated_failures()
    {
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var user = new UserAccount("sekreter", "Sekreter", "hash", SystemRole.Secretary);
        for (var i = 0; i < UserAccount.MaxFailedAttemptsBeforeLock; i++)
        {
            user.RegisterFailedLogin(now);
        }

        Assert.True(user.IsLockedOut(now));
        Assert.False(user.IsLockedOut(now.AddMinutes(2)));
        user.RegisterSuccessfulLogin(now.AddMinutes(2));
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void Customer_validation_and_anonymization()
    {
        var bad = Customer.Create(1, new CustomerDetails("", "Kara", "0532 456 78 90"));
        Assert.True(bad.IsFailure);
        var badMail = Customer.Create(1, new CustomerDetails("Ayşe", "Kara", null, Email: "ayse@"));
        Assert.True(badMail.IsFailure);

        var c = Customer.Create(7, new CustomerDetails("ayşe", "KARA", "0532 456 78 90", "ayse@example.com", NationalId: "10000000146")).Value;
        Assert.Equal("Ayşe Kara", c.FullName);
        Assert.Contains("ayse kara", c.SearchKey, StringComparison.Ordinal);
        Assert.Contains("05324567890", c.SearchKey, StringComparison.Ordinal);

        c.Anonymize(DateTime.UtcNow);
        Assert.Null(c.Phone);
        Assert.Null(c.Email);
        Assert.Null(c.NationalId);
        Assert.Equal(CustomerState.Anonymized, c.State);
        Assert.True(c.Update(new CustomerDetails("X", "Y", null)).IsFailure);
    }

    [Fact]
    public void Duplicate_detection_never_merges_and_explains()
    {
        var existing = new[]
        {
            Customer.Create(1, new CustomerDetails("Ayşe", "Kara", "0532 456 78 90")).Value,
            Customer.Create(2, new CustomerDetails("Mehmet", "Kara", "0532 111 22 33")).Value,
        };
        var sameFamilyPhone = DuplicateDetector.Find("Zeynep Kara", "+905324567890", existing);
        Assert.Single(sameFamilyPhone);
        Assert.Equal(DuplicateKind.SharedPhone, sameFamilyPhone[0].Kind);

        var samePerson = DuplicateDetector.Find("AYŞE KARA", "+905324567890", existing);
        Assert.Equal(DuplicateKind.LikelySamePerson, samePerson[0].Kind);
        Assert.Empty(DuplicateDetector.Find("Ali Veli", "+905551112233", existing));
    }
}
