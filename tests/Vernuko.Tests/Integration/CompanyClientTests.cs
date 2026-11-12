using Vernuko.Application.Services;
using Vernuko.Customers;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Danışmanlık / muhasebe / hukuk: firma müşterisi (unvan, vergi dairesi, VKN).</summary>
public class CompanyClientTests
{
    /// <summary>
    /// Independent oracle: the other published variant of the VKN check digit (x = d + 10 − (i+1); 9 stays 9;
    /// otherwise (x mod 10)·2^(10−(i+1)) mod 9), so the validator is not only checked against itself.
    /// </summary>
    private static int OracleCheckDigit(string nine)
    {
        var total = 0;
        for (var i = 0; i < 9; i++)
        {
            var x = (nine[i] - '0') + 10 - (i + 1);
            var mod = x % 10;
            total += mod == 9 ? 9 : mod * (int)Math.Pow(2, 10 - (i + 1)) % 9;
        }

        return (10 - (total % 10 % 10)) % 10;
    }

    [Fact]
    public void Vkn_check_digit_matches_the_independent_published_variant()
    {
        Assert.True(TaxNumberValidator.IsValidVkn("0000000001"));
        var random = new Random(20261112);
        for (var n = 0; n < 2000; n++)
        {
            var nine = string.Concat(Enumerable.Range(0, 9).Select(_ => (char)('0' + random.Next(10))));
            var check = OracleCheckDigit(nine);
            Assert.True(TaxNumberValidator.IsValidVkn(nine + check), nine + check);
            Assert.False(TaxNumberValidator.IsValidVkn(nine + ((check + 1 + random.Next(9)) % 10)), nine);
        }

        Assert.False(TaxNumberValidator.IsValidVkn("123456789"));
        Assert.False(TaxNumberValidator.IsValidVkn("12345678901"));
        Assert.False(TaxNumberValidator.IsValidVkn("12345abc90"));
    }

    [Fact]
    public async Task Company_details_are_validated_searchable_exported_and_removed_on_anonymization()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customers = app.Get<CustomerService>();
        var vkn = "123456789" + OracleCheckDigit("123456789");

        var invalid = await customers.CreateAsync(new CustomerDetails("Ali", "Veli", "0532 000 11 22", CompanyName: "Örnek Ltd.", TaxNumber: "1234567890" == vkn ? "1234567891" : "1234567890"), allowDuplicates: true);
        Assert.True(invalid.IsFailure);
        Assert.Equal("validation.customer.tax_number", invalid.Error.Code);

        var created = await customers.CreateAsync(new CustomerDetails("Ali", "Veli", "0532 000 11 22", CompanyName: "  Örnek Danışmanlık Ltd. Şti. ", TaxOffice: "Kadıköy", TaxNumber: vkn[..5] + " " + vkn[5..]), allowDuplicates: true);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.ToString() : null);
        var details = (await customers.GetAsync(created.Value)).Value;
        Assert.Equal("Örnek Danışmanlık Ltd. Şti.", details.CompanyName);
        Assert.Equal(vkn, details.TaxNumber);
        Assert.Equal($"Örnek Danışmanlık Ltd. Şti. · Kadıköy V.D. · {vkn}", details.CompanyLine);

        // Sole proprietor: 11-digit T.C. kimlik number as tax number.
        var sole = await customers.CreateAsync(new CustomerDetails("Ayşe", "Usta", "0532 000 11 23", CompanyName: "Usta Kuaför", TaxNumber: "10000000146"), allowDuplicates: true);
        Assert.True(sole.IsSuccess, sole.IsFailure ? sole.Error.ToString() : null);

        Assert.Contains((await customers.SearchAsync("danismanlik")).Value, c => c.Id == created.Value);
        Assert.Contains((await customers.SearchAsync(vkn)).Value, c => c.Id == created.Value);

        var csv = (await customers.ExportAllCsvAsync()).Value;
        Assert.Contains("Firma;Vergi Dairesi;Vergi No", csv, StringComparison.Ordinal);
        Assert.Contains(vkn, csv, StringComparison.Ordinal);

        Assert.True((await customers.AnonymizeAsync(created.Value, "KVKK talebi")).IsSuccess);
        var anonymized = (await customers.GetAsync(created.Value)).Value;
        Assert.Null(anonymized.CompanyName);
        Assert.Null(anonymized.TaxNumber);
        Assert.Null(anonymized.CompanyLine);
    }
}
