using System.Security.Cryptography;
using Vernuko.Infrastructure;

namespace Vernuko.Tests.Integration;

/// <summary>Runs only on Windows (CI windows job): real DPAPI and machine identity.</summary>
public class WindowsOnlyTests
{
    [Fact]
    public void Dpapi_protects_and_unprotects_for_the_current_user()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI yalnızca Windows'ta vardır.");
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var protector = new DpapiSecretProtector();
        var secret = RandomNumberGenerator.GetBytes(32);
        var protectedBytes = protector.Protect(secret);
        Assert.NotEqual(secret, protectedBytes);
        Assert.Equal(secret, protector.Unprotect(protectedBytes));
    }

    [Fact]
    public void Device_hash_is_stable_and_does_not_expose_raw_id()
    {
        var a = new DeviceIdentity().DeviceHash;
        var b = new DeviceIdentity().DeviceHash;
        Assert.Equal(a, b);
        Assert.Equal(32, a.Length);
        Assert.Matches("^[0-9a-f]{32}$", a);
    }
}
