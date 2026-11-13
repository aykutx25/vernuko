using System.Security.Cryptography;
using Vernuko.Backups;

namespace Vernuko.Tests.Domain;

public class BackupCryptoTests
{
    private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(16);

    private static BackupHeaderSeed Seed() => new(Guid.NewGuid(), DateTime.UtcNow, "0.1.0", "Initial", Guid.NewGuid(), BackupTrigger.Manual, Salt);

    private static async Task<byte[]> EncryptAsync(byte[] plain, byte[] kek)
    {
        using var input = new MemoryStream(plain);
        using var output = new MemoryStream();
        await BackupCrypto.EncryptAsync(input, output, Seed(), kek, CancellationToken.None);
        return output.ToArray();
    }

    private static async Task<(Vernuko.Core.Result<BackupHeader> Result, byte[] Plain)> DecryptAsync(byte[] data, byte[]? kek)
    {
        using var input = new MemoryStream(data);
        using var output = new MemoryStream();
        var r = await BackupCrypto.DecryptAsync(input, output, _ => kek, CancellationToken.None);
        return (r, output.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(BackupHeader.DefaultChunkSize)]
    [InlineData((BackupHeader.DefaultChunkSize * 2) + 123)]
    public async Task Roundtrip_for_various_sizes(int size)
    {
        var kek = RandomNumberGenerator.GetBytes(32);
        var plain = RandomNumberGenerator.GetBytes(size);
        var (r, decrypted) = await DecryptAsync(await EncryptAsync(plain, kek), kek);
        Assert.True(r.IsSuccess, r.IsFailure ? r.Error.Message : null);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public async Task Wrong_key_is_detected()
    {
        var data = await EncryptAsync(RandomNumberGenerator.GetBytes(1000), RandomNumberGenerator.GetBytes(32));
        var (r, _) = await DecryptAsync(data, RandomNumberGenerator.GetBytes(32));
        Assert.Equal(BackupErrors.WrongKey, r.Error);
    }

    [Fact]
    public async Task Flipped_byte_truncation_and_appended_data_are_detected()
    {
        var kek = RandomNumberGenerator.GetBytes(32);
        var data = await EncryptAsync(RandomNumberGenerator.GetBytes((BackupHeader.DefaultChunkSize * 2) + 10), kek);

        var flipped = (byte[])data.Clone();
        flipped[^100] ^= 0x01;
        Assert.Equal(BackupErrors.Corrupted, (await DecryptAsync(flipped, kek)).Result.Error);

        // Cut exactly at a chunk boundary: the remaining chunks are valid but the "last" flag is missing.
        var truncated = data[..(data.Length - (10 + 16 + 4))];
        Assert.Equal(BackupErrors.Corrupted, (await DecryptAsync(truncated, kek)).Result.Error);

        var appended = data.Concat(new byte[] { 1, 2, 3 }).ToArray();
        Assert.Equal(BackupErrors.Corrupted, (await DecryptAsync(appended, kek)).Result.Error);
    }

    [Fact]
    public async Task Header_tampering_is_detected()
    {
        var kek = RandomNumberGenerator.GetBytes(32);
        var data = await EncryptAsync(RandomNumberGenerator.GetBytes(5000), kek);
        var text = System.Text.Encoding.ASCII.GetString(data, 0, 400);
        var idx = text.IndexOf("0.1.0", StringComparison.Ordinal);
        Assert.True(idx > 0);
        data[idx] = (byte)'9'; // appVersion "9.1.0": header is authenticated as AAD
        Assert.Equal(BackupErrors.Corrupted, (await DecryptAsync(data, kek)).Result.Error);
    }

    [Fact]
    public async Task Not_a_backup_file()
    {
        var (r, _) = await DecryptAsync(System.Text.Encoding.UTF8.GetBytes("merhaba dünya"), RandomNumberGenerator.GetBytes(32));
        Assert.Equal(BackupErrors.NotABackup, r.Error);
    }

    [Fact]
    public void Recovery_key_roundtrip_and_typo_tolerance()
    {
        var key = RecoveryKey.Generate();
        var text = key.ToString();
        Assert.Matches("^([A-Z2-9]{4}-){7}[A-Z2-9]{4}$", text);
        Assert.Equal(key.Bytes.ToArray(), RecoveryKey.Parse(text.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal)).Value.Bytes.ToArray());
        Assert.True(RecoveryKey.Parse("ABCD-EFGH").IsFailure);
        Assert.True(RecoveryKey.Parse(text + "-AAAA").IsFailure);
        var salt = RandomNumberGenerator.GetBytes(16);
        Assert.Equal(key.DeriveKek(salt), RecoveryKey.Parse(text).Value.DeriveKek(salt));
        Assert.NotEqual(key.DeriveKek(salt), key.DeriveKek(RandomNumberGenerator.GetBytes(16)));
    }

    [Fact]
    public void Retention_keeps_daily_weekly_monthly()
    {
        var zone = TimeZoneInfo.Utc;
        var start = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var backups = Enumerable.Range(0, 120)
            .Select(i => new BackupRecord($"b{i:000}.vbk", start.AddDays(i), BackupTrigger.Scheduled))
            .Append(new BackupRecord("safety.vbk", start, BackupTrigger.PreRestore))
            .ToList();
        var delete = BackupRetention.SelectForDeletion(backups, RetentionPolicy.Default, zone).Select(b => b.Name).ToHashSet();
        var kept = backups.Where(b => !delete.Contains(b.Name)).ToList();
        Assert.Contains(kept, b => b.Name == "b119.vbk");
        Assert.Contains(kept, b => b.Name == "safety.vbk");
        Assert.InRange(kept.Count(b => b.Trigger == BackupTrigger.Scheduled), 10, 14);
        Assert.All(Enumerable.Range(113, 7), i => Assert.Contains(kept, b => b.Name == $"b{i:000}.vbk"));
    }

    [Fact]
    public void Backup_file_names_contain_no_personal_data_and_parse_back()
    {
        var created = new DateTime(2026, 11, 12, 9, 15, 30, DateTimeKind.Utc);
        var name = BackupNaming.FileName(created, BackupTrigger.Scheduled, Guid.NewGuid(), Guid.NewGuid());
        Assert.StartsWith("vernuko-20261112-091530-scheduled-", name, StringComparison.Ordinal);
        Assert.Equal(created, BackupNaming.TryParseCreatedAt(name));
        Assert.Equal(BackupTrigger.Scheduled, BackupNaming.TryParseTrigger(name));

        // Two backups created in the same second must not get the same file name.
        var names = Enumerable.Range(0, 200).Select(_ => BackupNaming.FileName(created, BackupTrigger.Manual, Vernuko.Core.Ids.New(), Guid.NewGuid())).ToHashSet();
        Assert.Equal(200, names.Count);
    }

    [Fact]
    public async Task Local_target_rejects_path_traversal_and_writes_atomically()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vernuko-tests", Guid.NewGuid().ToString("N"));
        var target = new LocalFolderBackupTarget(dir);
        var src = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await File.WriteAllBytesAsync(src, [1, 2, 3]);
        await Assert.ThrowsAsync<ArgumentException>(() => target.UploadAsync(src, "../evil.vbk", null, CancellationToken.None));
        await target.UploadAsync(src, "ok.vbk", null, CancellationToken.None);
        Assert.Single(await target.ListAsync(CancellationToken.None));
        Assert.Empty(Directory.GetFiles(dir, "*.partial"));
        Directory.Delete(dir, true);
        File.Delete(src);
    }
}
