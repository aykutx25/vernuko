using System.Text;
using Microsoft.EntityFrameworkCore;
using Vernuko.Application.Services;
using Vernuko.Core;
using Vernuko.Security;
using Vernuko.Tests.Support;

namespace Vernuko.Tests.Integration;

/// <summary>Gizli uzman notları: şifreli, yalnızca yazarı okur.</summary>
public class PrivateNotesTests
{
    [Fact]
    public async Task Only_the_author_reads_a_note_and_it_is_encrypted_at_rest()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var users = app.Get<UserAdminService>();
        Assert.True((await users.CreateAsync("psikolog1", "Dr. Ada", "Uzman-Parola-2026", SystemRole.Specialist)).IsSuccess);
        Assert.True((await users.CreateAsync("psikolog2", "Dr. Bora", "Uzman-Parola-2026", SystemRole.Specialist)).IsSuccess);
        Assert.True((await users.CreateAsync("sekreter", "Sekreter", "Randevu-Masasi-1", SystemRole.Secretary)).IsSuccess);
        var customer = await app.AddCustomerAsync("Danışan", "Bir", "0532 444 44 44");
        var auth = app.Get<AuthService>();
        var notes = app.Get<PrivateNotesService>();
        const string Secret = "Kaygı bozukluğu; ilaç kullanmıyor. GİZLİ-İÇERİK-123";

        Assert.True((await auth.LoginAsync("psikolog1", "Uzman-Parola-2026")).IsSuccess);
        var added = await notes.AddAsync(customer, Secret);
        Assert.True(added.IsSuccess, added.IsFailure ? added.Error.ToString() : null);
        var mine = (await notes.GetAsync(customer)).Value;
        Assert.Equal(Secret, Assert.Single(mine.Mine).Text);
        Assert.Equal(0, mine.OthersCount);

        // Not in the database file in plain text.
        await using (var db = await app.DbAsync())
        {
            var row = await db.PrivateNotes.AsNoTracking().SingleAsync();
            Assert.DoesNotContain("GİZLİ-İÇERİK", Encoding.UTF8.GetString(row.Ciphertext), StringComparison.Ordinal);
        }

        Assert.DoesNotContain("GİZLİ-İÇERİK", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(app.Env.DatabasePath)), StringComparison.Ordinal);

        // Another specialist: sees only that a note exists; cannot delete it.
        auth.Logout();
        Assert.True((await auth.LoginAsync("psikolog2", "Uzman-Parola-2026")).IsSuccess);
        var other = (await notes.GetAsync(customer)).Value;
        Assert.Empty(other.Mine);
        Assert.Equal(1, other.OthersCount);
        Assert.True((await notes.DeleteAsync(added.Value)).IsFailure);

        // Secretary: no access at all.
        auth.Logout();
        Assert.True((await auth.LoginAsync("sekreter", "Randevu-Masasi-1")).IsSuccess);
        Assert.Equal(Error.Forbidden.Code, (await notes.GetAsync(customer)).Error.Code);
        Assert.Equal(Error.Forbidden.Code, (await notes.AddAsync(customer, "x")).Error.Code);

        // The owner has the permission but is not the author: still only a count.
        auth.Logout();
        Assert.True((await auth.LoginAsync(TestApp.AdminUser, TestApp.AdminPassword)).IsSuccess);
        Assert.Empty((await notes.GetAsync(customer)).Value.Mine);

        // The author deletes; the encrypted text is wiped.
        auth.Logout();
        Assert.True((await auth.LoginAsync("psikolog1", "Uzman-Parola-2026")).IsSuccess);
        Assert.True((await notes.DeleteAsync(added.Value)).IsSuccess);
        Assert.Empty((await notes.GetAsync(customer)).Value.Mine);
        await using (var db = await app.DbAsync())
        {
            Assert.Empty((await db.PrivateNotes.AsNoTracking().SingleAsync()).Ciphertext);
            Assert.Contains(await db.AuditEntries.AsNoTracking().Select(a => a.Action).ToListAsync(), a => a == "private_note.viewed");
        }
    }

    [Fact]
    public async Task Anonymizing_the_customer_wipes_the_notes()
    {
        await using var app = await TestApp.CreateAsync();
        await app.SetupBeautyAsync();
        var customer = await app.AddCustomerAsync("Silinecek", "Danışan", "0532 555 55 55");
        var notes = app.Get<PrivateNotesService>();
        Assert.True((await notes.AddAsync(customer, "Not içeriği")).IsSuccess);
        Assert.True((await app.Get<CustomerService>().AnonymizeAsync(customer, "KVKK talebi")).IsSuccess);
        Assert.Empty((await notes.GetAsync(customer)).Value.Mine);
        Assert.True((await notes.AddAsync(customer, "yeni")).IsFailure);
    }
}
