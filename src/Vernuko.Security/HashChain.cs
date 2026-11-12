using System.Security.Cryptography;
using System.Text;

namespace Vernuko.Security;

/// <summary>
/// Tamper-evidence helper for append-only logs: each entry hash covers the previous hash and the entry content,
/// so editing or deleting a past entry breaks verification of everything after it.
/// </summary>
public static class HashChain
{
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    public static string Compute(string previousHash, string canonicalContent)
    {
        var bytes = Encoding.UTF8.GetBytes(previousHash + "\n" + canonicalContent);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
