namespace XLOTP;

internal static class TotpAlgorithm
{
    public const string Sha1 = "SHA1";
    public const string Sha256 = "SHA256";
    public const string Sha512 = "SHA512";
    public const string Default = Sha1;

    public static bool IsSupported(string? value)
    {
        if (value == null)
        {
            return false;
        }

        return value.Equals(Sha1, StringComparison.OrdinalIgnoreCase)
               || value.Equals(Sha256, StringComparison.OrdinalIgnoreCase)
               || value.Equals(Sha512, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        if (value.Equals(Sha1, StringComparison.OrdinalIgnoreCase))
        {
            return Sha1;
        }

        if (value.Equals(Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Sha256;
        }

        if (value.Equals(Sha512, StringComparison.OrdinalIgnoreCase))
        {
            return Sha512;
        }

        throw new NotSupportedException($"Unsupported algorithm '{value}'.");
    }
}
