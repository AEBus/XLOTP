using System.Runtime.Versioning;
using System.Security.Cryptography;
using XLOTP.Util;

namespace XLOTP.Security;

internal static class SecretProtector
{
    [SupportedOSPlatform("windows")]
    public static string Protect(byte[] secret, string scopeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows. Use --allow-plaintext on non-Windows systems.");
        }

        var protectedBytes = ProtectedData.Protect(secret, null, ProtectionScopeHelper.ToDataProtectionScope(scopeName));
        return Convert.ToBase64String(protectedBytes);
    }

    [SupportedOSPlatform("windows")]
    public static byte[] Unprotect(string payload, string scopeName)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Encrypted secret payload is empty.", nameof(payload));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Encrypted secrets can only be opened on Windows.");
        }

        var data = Convert.FromBase64String(payload);
        return ProtectedData.Unprotect(data, null, ProtectionScopeHelper.ToDataProtectionScope(scopeName));
    }
}
