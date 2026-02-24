using XLOTP.Configuration;
using XLOTP.Security;
using XLOTP.Util;
using System.Security.Cryptography;

namespace XLOTP.Commands;

internal sealed class ConfigureCommand : CliCommand
{
    public override string Name => "configure";

    public override string Description => "Store (or update) the OTP secret that the other commands will use.";

    public override async Task<int> ExecuteAsync(string[] args)
    {
        var options = CreateOptions(args);
        if (options.IsHelpRequested)
        {
            PrintUsage(AppDomain.CurrentDomain.FriendlyName);
            return 0;
        }

        var secretInput = options.Require("secret", "Missing --secret. Provide the base32-encoded Square Enix OTP seed from WinAuth.");
        if (secretInput == null)
        {
            return 1;
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Base32Encoding.ToBytes(secretInput);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Secret could not be decoded as base32: {ex.Message}");
            return 1;
        }

        var profileName = options.GetSingleOrDefault("profile")?.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            profileName = "default";
        }

        var label = options.GetSingleOrDefault("label") ?? profileName;
        var digits = Math.Clamp(options.GetInt("digits", TotpOptions.DefaultDigits), 4, 10);
        var period = Math.Max(5, options.GetInt("period", TotpOptions.DefaultPeriodSeconds));
        var algoInput = options.GetSingleOrDefault("algo") ?? TotpAlgorithm.Default;
        if (!TotpAlgorithm.IsSupported(algoInput))
        {
            Console.Error.WriteLine($"Unsupported --algo value '{algoInput}'. Valid options: SHA1, SHA256, SHA512.");
            return 1;
        }
        var algorithm = TotpAlgorithm.Normalize(algoInput);

        var allowPlaintext = options.HasFlag("allow-plaintext");
        if (!allowPlaintext && !OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("DPAPI encryption is only available on Windows. Re-run with --allow-plaintext (and protect the config file manually) if you need cross-platform storage.");
            return 1;
        }

        var scopeName = ProtectionScopeHelper.NormalizeInput(options.GetSingleOrDefault("scope"));

        try
        {
            var store = new ConfigStore(options.GetSingleOrDefault("config"));
            var config = await store.TryLoadAsync() ?? new XlOtpConfig();

            var now = DateTimeOffset.UtcNow;
            var existingProfile = config.Profiles.TryGetValue(profileName, out var found) ? found : null;
            var profile = new XlOtpProfile
            {
                Label = label,
                Digits = digits,
                PeriodSeconds = period,
                Algorithm = algorithm,
                SecretIsPlainText = allowPlaintext,
                ProtectionScope = scopeName,
                CreatedUtc = existingProfile?.CreatedUtc ?? now,
                UpdatedUtc = now
            };

            profile.ProtectedSecret = allowPlaintext
                ? Base32Encoding.Normalize(secretInput)
                : SecretProtector.Protect(secretBytes, scopeName);

            config.UpsertProfile(profileName, profile);

            if (options.HasFlag("default") || config.Profiles.Count == 1 || string.IsNullOrWhiteSpace(config.DefaultProfile))
            {
                config.DefaultProfile = profileName;
            }

            await store.SaveAsync(config);

            var defaultSuffix = config.DefaultProfile.Equals(profileName, StringComparison.OrdinalIgnoreCase) ? " (default)" : string.Empty;
            Console.WriteLine($"Stored OTP profile '{profileName}'{defaultSuffix} at {store.Path} using {algorithm}/{period}s/{digits}-digit codes.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
        }
        return 0;
    }

    public override void PrintUsage(string executableName)
    {
        PrintCommandHeader(executableName, Name, Description);
        Console.WriteLine("Required parameters:");
        Console.WriteLine("  --secret <value>        Base32 secret (same value WinAuth uses).");
        Console.WriteLine();
        Console.WriteLine("Optional parameters:");
        Console.WriteLine("  --profile <name>        Profile to create/update (default: default).");
        Console.WriteLine("  --default               Set this profile as default for code/send.");
        Console.WriteLine("  --label <text>          Friendly name stored in the config (default: Square Enix).");
        Console.WriteLine("  --digits <number>       Number of OTP digits (default: 6).");
        Console.WriteLine("  --period <seconds>      Code lifetime in seconds (default: 30).");
        Console.WriteLine("  --algo <sha1|sha256|sha512>  HMAC algorithm (default: SHA1).");
        Console.WriteLine("  --config <path>         Custom configuration file path (default: %APPDATA%/XLOTP/config.json).");
        Console.WriteLine("  --scope <user|machine>  DPAPI scope for encrypted secrets (default: user).");
        Console.WriteLine("  --allow-plaintext       Store the secret as normalized base32 text instead of using DPAPI.");
        Console.WriteLine();
        Console.WriteLine($"Examples:\n  {executableName} configure --secret JBSWY3DPEHPK3PXP --profile main --default\n  {executableName} configure --secret ... --profile alt --label \"Alt account\" --algo sha1 --period 30");
    }
}
