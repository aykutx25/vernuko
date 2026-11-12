using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vernuko.Core;

namespace Vernuko.Backups;

/// <summary>
/// Streaming authenticated encryption (STREAM construction over AES-256-GCM):
/// nonce = 7-byte random prefix ‖ 4-byte big-endian chunk counter ‖ 1-byte last-chunk flag.
/// The SHA-256 of the header is the AAD of every chunk. This detects modification, reordering, truncation and
/// header tampering, and keeps memory use constant for large files.
/// </summary>
public static class BackupCrypto
{
    private const int TagSize = 16;
    private const int NoncePrefixSize = 7;
    private static readonly byte[] WrapAadPrefix = Encoding.ASCII.GetBytes("vernuko/backup/filekey/v1:");

    public static async Task EncryptAsync(Stream plaintext, Stream output, BackupHeaderSeed seed, byte[] kek, CancellationToken ct)
    {
        var fileKey = RandomNumberGenerator.GetBytes(32);
        var wrapNonce = RandomNumberGenerator.GetBytes(12);
        var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixSize);
        var wrapped = new byte[32 + TagSize];
        using (var wrapGcm = new AesGcm(kek, TagSize))
        {
            wrapGcm.Encrypt(wrapNonce, fileKey, wrapped.AsSpan(0, 32), wrapped.AsSpan(32), WrapAad(seed.BackupId));
        }

        var header = new BackupHeader
        {
            BackupId = seed.BackupId,
            CreatedAtUtc = seed.CreatedAtUtc,
            AppVersion = seed.AppVersion,
            SchemaVersion = seed.SchemaVersion,
            InstallationId = seed.InstallationId,
            Trigger = seed.Trigger.ToString(),
            KeyId = KeyDerivation.KeyId(kek),
            KdfSalt = Convert.ToBase64String(seed.KdfSalt),
            WrapNonce = Convert.ToBase64String(wrapNonce),
            WrappedKey = Convert.ToBase64String(wrapped),
            NoncePrefix = Convert.ToBase64String(noncePrefix),
        };

        var headerBytes = JsonSerializer.SerializeToUtf8Bytes(header);
        var aad = SHA256.HashData(headerBytes);
        await output.WriteAsync(Encoding.ASCII.GetBytes(BackupHeader.Magic), ct);
        var lenBuf = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lenBuf, headerBytes.Length);
        await output.WriteAsync(lenBuf, ct);
        await output.WriteAsync(headerBytes, ct);

        using var gcm = new AesGcm(fileKey, TagSize);
        var chunk = new byte[header.ChunkSize];
        var next = new byte[header.ChunkSize];
        var cipher = new byte[header.ChunkSize];
        var tag = new byte[TagSize];
        var nonce = new byte[12];
        uint counter = 0;

        var read = await ReadFullAsync(plaintext, chunk, ct);
        while (true)
        {
            var nextRead = read == chunk.Length ? await ReadFullAsync(plaintext, next, ct) : 0;
            var isLast = nextRead == 0;
            BuildNonce(nonce, noncePrefix, counter, isLast);
            gcm.Encrypt(nonce, chunk.AsSpan(0, read), cipher.AsSpan(0, read), tag, aad);
            BinaryPrimitives.WriteInt32LittleEndian(lenBuf, read);
            await output.WriteAsync(lenBuf, ct);
            await output.WriteAsync(cipher.AsMemory(0, read), ct);
            await output.WriteAsync(tag, ct);
            if (isLast)
            {
                break;
            }

            counter = checked(counter + 1);
            (chunk, next) = (next, chunk);
            read = nextRead;
        }

        CryptographicOperations.ZeroMemory(fileKey);
        await output.FlushAsync(ct);
    }

    public static async Task<Result<BackupHeader>> ReadHeaderAsync(Stream input, CancellationToken ct)
    {
        var r = await ReadHeaderCoreAsync(input, ct);
        return r.IsSuccess ? r.Value.Header : r.Error;
    }

    private static async Task<Result<(BackupHeader Header, byte[] Bytes)>> ReadHeaderCoreAsync(Stream input, CancellationToken ct)
    {
        var magic = new byte[BackupHeader.Magic.Length];
        if (await ReadFullAsync(input, magic, ct) != magic.Length || Encoding.ASCII.GetString(magic) != BackupHeader.Magic)
        {
            return BackupErrors.NotABackup;
        }

        var lenBuf = new byte[4];
        if (await ReadFullAsync(input, lenBuf, ct) != 4)
        {
            return BackupErrors.Corrupted;
        }

        var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
        if (len is <= 0 or > 64 * 1024)
        {
            return BackupErrors.Corrupted;
        }

        var headerBytes = new byte[len];
        if (await ReadFullAsync(input, headerBytes, ct) != len)
        {
            return BackupErrors.Corrupted;
        }

        try
        {
            var header = JsonSerializer.Deserialize<BackupHeader>(headerBytes);
            if (header is null)
            {
                return BackupErrors.Corrupted;
            }

            if (header.FormatVersion > BackupHeader.CurrentFormatVersion)
            {
                return BackupErrors.UnsupportedVersion;
            }

            return (header, headerBytes);
        }
        catch (JsonException)
        {
            return BackupErrors.Corrupted;
        }
    }

    /// <summary>
    /// Decrypts and authenticates the whole stream into <paramref name="plaintextOut"/>. On any failure the caller
    /// must discard the output (it may contain a verified prefix only).
    /// </summary>
    public static async Task<Result<BackupHeader>> DecryptAsync(Stream input, Stream plaintextOut, Func<BackupHeader, byte[]?> kekResolver, CancellationToken ct)
    {
        var headerResult = await ReadHeaderCoreAsync(input, ct);
        if (headerResult.IsFailure)
        {
            return headerResult.Error;
        }

        var (header, headerBytes) = headerResult.Value;
        var aad = SHA256.HashData(headerBytes);

        var kek = kekResolver(header);
        if (kek is null)
        {
            return BackupErrors.WrongKey;
        }

        byte[] fileKey = new byte[32];
        try
        {
            var wrapped = Convert.FromBase64String(header.WrappedKey);
            using var wrapGcm = new AesGcm(kek, TagSize);
            wrapGcm.Decrypt(Convert.FromBase64String(header.WrapNonce), wrapped.AsSpan(0, 32), wrapped.AsSpan(32, TagSize), fileKey, WrapAad(header.BackupId));
        }
        catch (Exception ex) when (ex is AuthenticationTagMismatchException or CryptographicException or FormatException or ArgumentException)
        {
            return BackupErrors.WrongKey;
        }

        var noncePrefix = Convert.FromBase64String(header.NoncePrefix);
        if (noncePrefix.Length != NoncePrefixSize || header.ChunkSize is <= 0 or > 16 * 1024 * 1024)
        {
            return BackupErrors.Corrupted;
        }

        using var gcm = new AesGcm(fileKey, TagSize);
        var lenBuf = new byte[4];
        var cipher = new byte[header.ChunkSize];
        var plain = new byte[header.ChunkSize];
        var tag = new byte[TagSize];
        var nonce = new byte[12];
        uint counter = 0;
        try
        {
            while (true)
            {
                if (await ReadFullAsync(input, lenBuf, ct) != 4)
                {
                    return BackupErrors.Corrupted; // stream ended without an authenticated last chunk (truncation)
                }

                var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
                if (len < 0 || len > header.ChunkSize)
                {
                    return BackupErrors.Corrupted;
                }

                if (await ReadFullAsync(input, cipher.AsMemory(0, len), ct) != len || await ReadFullAsync(input, tag, ct) != TagSize)
                {
                    return BackupErrors.Corrupted;
                }

                // Try "not last" first, then "last": only the correct flag authenticates.
                BuildNonce(nonce, noncePrefix, counter, false);
                var isLast = false;
                if (!TryDecrypt(gcm, nonce, cipher.AsSpan(0, len), tag, plain.AsSpan(0, len), aad))
                {
                    BuildNonce(nonce, noncePrefix, counter, true);
                    if (!TryDecrypt(gcm, nonce, cipher.AsSpan(0, len), tag, plain.AsSpan(0, len), aad))
                    {
                        return BackupErrors.Corrupted;
                    }

                    isLast = true;
                }

                await plaintextOut.WriteAsync(plain.AsMemory(0, len), ct);
                if (isLast)
                {
                    // Anything after the last chunk is tampering.
                    return input.ReadByte() == -1 ? header : BackupErrors.Corrupted;
                }

                counter = checked(counter + 1);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileKey);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static bool TryDecrypt(AesGcm gcm, byte[] nonce, ReadOnlySpan<byte> cipher, byte[] tag, Span<byte> plain, byte[] aad)
    {
        try
        {
            gcm.Decrypt(nonce, cipher, tag, plain, aad);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
    }

    private static byte[] WrapAad(Guid backupId) => [.. WrapAadPrefix, .. Encoding.ASCII.GetBytes(backupId.ToString("N"))];

    private static void BuildNonce(byte[] nonce, byte[] prefix, uint counter, bool last)
    {
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(NoncePrefixSize, 4), counter);
        nonce[11] = last ? (byte)1 : (byte)0;
    }

    private static async Task<int> ReadFullAsync(Stream s, Memory<byte> buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await s.ReadAsync(buffer[total..], ct);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }
}

public sealed record BackupHeaderSeed(
    Guid BackupId,
    DateTime CreatedAtUtc,
    string AppVersion,
    string SchemaVersion,
    Guid InstallationId,
    BackupTrigger Trigger,
    byte[] KdfSalt);
