using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace XLOTP.Security;

internal static class ProtectionScopeHelper
{
    public const string DefaultScopeName = "CurrentUser";

    public static string NormalizeInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultScopeName;
        }

        if (value.Equals("machine", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("localmachine", StringComparison.OrdinalIgnoreCase))
        {
            return "LocalMachine";
        }

        if (value.Equals("user", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("currentuser", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultScopeName;
        }

        return value;
    }

    [SupportedOSPlatform("windows")]
    public static DataProtectionScope ToDataProtectionScope(string scopeName)
    {
        return scopeName.Equals("LocalMachine", StringComparison.OrdinalIgnoreCase)
            ? DataProtectionScope.LocalMachine
            : DataProtectionScope.CurrentUser;
    }
}
