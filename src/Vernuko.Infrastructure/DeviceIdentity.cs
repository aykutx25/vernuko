using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Vernuko.Application.Common;

namespace Vernuko.Infrastructure;

/// <summary>
/// License device id = salted SHA-256 of the Windows MachineGuid. It survives hardware changes (RAM, disk, GPU)
/// and only changes on a Windows reinstall, so customers are not locked out by normal repairs. The raw id never
/// leaves the machine.
/// </summary>
public sealed class DeviceIdentity : IDeviceIdentity
{
    private readonly Lazy<string> _hash = new(Compute);

    public string DeviceHash => _hash.Value;

    private static string Compute()
    {
        string raw;
        if (OperatingSystem.IsWindows())
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            raw = key?.GetValue("MachineGuid") as string ?? Environment.MachineName;
        }
        else
        {
            raw = File.Exists("/etc/machine-id") ? File.ReadAllText("/etc/machine-id").Trim() : Environment.MachineName;
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("vernuko/device/v1:" + raw)))[..32];
    }
}
