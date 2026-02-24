using XLOTP.Security;
using XLOTP.Util;

namespace XLOTP.Configuration;

internal sealed class XlOtpConfig
{
    private const string DefaultProfileName = "default";

    public int Version { get; set; } = 2;

    public string DefaultProfile { get; set; } = DefaultProfileName;

    public Dictionary<string, XlOtpProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Legacy fields kept for one-time migration from v1 format.
    public string? Label { get; set; }

    public string? ProtectedSecret { get; set; }

    public bool SecretIsPlainText { get; set; }

    public string ProtectionScope { get; set; } = ProtectionScopeHelper.DefaultScopeName;

    public string Algorithm { get; set; } = TotpAlgorithm.Default;

    public int PeriodSeconds { get; set; } = TotpOptions.DefaultPeriodSeconds;

    public int Digits { get; set; } = TotpOptions.DefaultDigits;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public XlOtpProfile GetProfile(string? name)
    {
        EnsureLegacyMigration();

        var resolved = string.IsNullOrWhiteSpace(name) ? DefaultProfile : name.Trim();
        if (!Profiles.TryGetValue(resolved, out var profile))
        {
            throw new InvalidOperationException($"Profile '{resolved}' was not found in the configuration.");
        }

        return profile;
    }

    public IReadOnlyList<string> GetProfileNames()
    {
        EnsureLegacyMigration();
        return Profiles.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public void UpsertProfile(string name, XlOtpProfile profile)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name must not be empty.", nameof(name));
        }

        EnsureLegacyMigration();
        Profiles[name.Trim()] = profile;
    }

    public bool RemoveProfile(string name)
    {
        EnsureLegacyMigration();
        return Profiles.Remove(name);
    }

    public void EnsureLegacyMigration()
    {
        if (Profiles.Count > 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ProtectedSecret))
        {
            return;
        }

        var profile = new XlOtpProfile
        {
            Label = string.IsNullOrWhiteSpace(Label) ? "Square Enix" : Label!,
            ProtectedSecret = ProtectedSecret!,
            SecretIsPlainText = SecretIsPlainText,
            ProtectionScope = ProtectionScope,
            Algorithm = string.IsNullOrWhiteSpace(Algorithm) ? TotpAlgorithm.Default : Algorithm,
            PeriodSeconds = PeriodSeconds <= 0 ? TotpOptions.DefaultPeriodSeconds : PeriodSeconds,
            Digits = Digits <= 0 ? TotpOptions.DefaultDigits : Digits,
            CreatedUtc = CreatedUtc == default ? DateTimeOffset.UtcNow : CreatedUtc,
            UpdatedUtc = UpdatedUtc == default ? DateTimeOffset.UtcNow : UpdatedUtc
        };

        Profiles[DefaultProfileName] = profile;
        if (string.IsNullOrWhiteSpace(DefaultProfile))
        {
            DefaultProfile = DefaultProfileName;
        }

        Version = 2;
    }
}

internal sealed class XlOtpProfile
{
    public string Label { get; set; } = "Square Enix";

    public string ProtectedSecret { get; set; } = string.Empty;

    public bool SecretIsPlainText { get; set; }

    public string ProtectionScope { get; set; } = ProtectionScopeHelper.DefaultScopeName;

    public string Algorithm { get; set; } = TotpAlgorithm.Default;

    public int PeriodSeconds { get; set; } = TotpOptions.DefaultPeriodSeconds;

    public int Digits { get; set; } = TotpOptions.DefaultDigits;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public byte[] GetSecretBytes()
    {
        if (string.IsNullOrWhiteSpace(ProtectedSecret))
        {
            throw new InvalidOperationException("No secret stored in this profile.");
        }

        if (SecretIsPlainText)
        {
            return Base32Encoding.ToBytes(ProtectedSecret);
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Encrypted secrets can only be opened on Windows. Re-run configure with --allow-plaintext on non-Windows systems.");
        }

        return SecretProtector.Unprotect(ProtectedSecret, ProtectionScope);
    }
}
