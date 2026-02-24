using XLOTP.Configuration;
using XLOTP.Util;

namespace XLOTP.Commands;

internal static class CommandHelper
{
    public static async Task<(XlOtpConfig Config, string Path)?> TryLoadConfigAsync(string? pathOverride, bool required, CancellationToken cancellationToken)
    {
        var store = new ConfigStore(pathOverride);

        try
        {
            var config = await store.LoadAsync(cancellationToken);
            return (config, store.Path);
        }
        catch (FileNotFoundException) when (!required)
        {
            return null;
        }
    }

    public static async Task<(TotpOptions Options, XlOtpConfig? Config)> ResolveTotpOptionsAsync(OptionReader options, CancellationToken cancellationToken)
    {
        var secretOverride = options.GetSingleOrDefault("secret");
        byte[]? secretBytes = null;
        if (!string.IsNullOrWhiteSpace(secretOverride))
        {
            try
            {
                secretBytes = Base32Encoding.ToBytes(secretOverride);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid --secret value: {ex.Message}", ex);
            }
        }

        XlOtpConfig? config = null;
        var configPath = options.GetSingleOrDefault("config");
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var loaded = await TryLoadConfigAsync(configPath, required: true, cancellationToken);
            config = loaded?.Config;
        }
        else if (secretBytes == null)
        {
            var loaded = await TryLoadConfigAsync(null, required: true, cancellationToken);
            config = loaded?.Config;
        }

        if (secretBytes == null)
        {
            if (config == null)
            {
                throw new InvalidOperationException("Provide --secret or run the configure command first.");
            }

            var profileName = ResolveProfileName(options);
            var profile = config.GetProfile(profileName);
            secretBytes = profile.GetSecretBytes();
        }

        XlOtpProfile? fallbackProfile = null;
        if (config != null && config.Profiles.Count > 0)
        {
            var profileName = ResolveProfileName(options);
            if (string.IsNullOrWhiteSpace(profileName))
            {
                fallbackProfile = config.GetProfile(null);
            }
            else if (config.Profiles.TryGetValue(profileName, out var profile))
            {
                fallbackProfile = profile;
            }
        }

        var digits = Math.Clamp(options.GetInt("digits", fallbackProfile?.Digits ?? TotpOptions.DefaultDigits), 4, 10);
        var period = Math.Max(5, options.GetInt("period", fallbackProfile?.PeriodSeconds ?? TotpOptions.DefaultPeriodSeconds));
        var algoInput = options.GetSingleOrDefault("algo") ?? fallbackProfile?.Algorithm ?? TotpAlgorithm.Default;
        if (!TotpAlgorithm.IsSupported(algoInput))
        {
            throw new ArgumentException($"Unsupported --algo value '{algoInput}'. Valid options: SHA1, SHA256, SHA512.");
        }

        var algorithm = TotpAlgorithm.Normalize(algoInput);
        return (new TotpOptions
        {
            Secret = secretBytes!,
            Digits = digits,
            PeriodSeconds = period,
            Algorithm = algorithm
        }, config);
    }

    public static bool TryResolveCounter(OptionReader options, out long? counter, out DateTimeOffset timestamp)
    {
        counter = null;
        timestamp = DateTimeOffset.UtcNow;

        var counterValue = options.GetSingleOrDefault("counter");
        if (!string.IsNullOrWhiteSpace(counterValue))
        {
            if (!long.TryParse(counterValue, out var parsed) || parsed < 0)
            {
                Console.Error.WriteLine("--counter must be a non-negative integer.");
                return false;
            }

            counter = parsed;
            return true;
        }

        var unixTime = options.GetSingleOrDefault("unix-time");
        if (!string.IsNullOrWhiteSpace(unixTime))
        {
            if (!long.TryParse(unixTime, out var parsedUnix))
            {
                Console.Error.WriteLine("--unix-time must be an integer UNIX timestamp.");
                return false;
            }

            timestamp = DateTimeOffset.FromUnixTimeSeconds(parsedUnix);
            return true;
        }

        var textualTime = options.GetSingleOrDefault("time");
        if (!string.IsNullOrWhiteSpace(textualTime) && !textualTime.Equals("now", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTimeOffset.TryParse(textualTime, out var parsedTime))
            {
                Console.Error.WriteLine("Could not parse --time value. Use ISO-8601 (e.g. 2025-01-01T12:34:56Z).");
                return false;
            }

            timestamp = parsedTime.ToUniversalTime();
            return true;
        }

        if (timestamp.Offset != TimeSpan.Zero)
        {
            timestamp = timestamp.ToUniversalTime();
        }

        return true;
    }

    public static string? ResolveProfileName(OptionReader options)
    {
        var profile = options.GetSingleOrDefault("profile");
        return string.IsNullOrWhiteSpace(profile) ? null : profile.Trim();
    }
}
