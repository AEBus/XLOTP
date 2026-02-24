namespace XLOTP.Util;

/// <summary>
/// Very small argument helper for the simple multi-command CLI.
/// Supports --option value, --option=value, -o value, and flags (--flag).
/// </summary>
internal sealed class OptionReader
{
    private readonly Dictionary<string, List<string>> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _positionals = new();

    public OptionReader(IEnumerable<string> args)
    {
        Parse(args);
    }

    public bool IsHelpRequested => HasFlag("help") || HasFlag("h");

    public IReadOnlyList<string> Positionals => _positionals;

    public bool HasFlag(string key)
    {
        if (!_options.TryGetValue(NormalizeKey(key), out var values))
        {
            return false;
        }

        if (values.Count == 0)
        {
            return true;
        }

        return values.Any(v => bool.TryParse(v, out var parsed) ? parsed : true);
    }

    public string? GetSingleOrDefault(string key)
    {
        return _options.TryGetValue(NormalizeKey(key), out var values) && values.Count > 0
            ? values[^1]
            : null;
    }

    public int GetInt(string key, int fallback)
    {
        var value = GetSingleOrDefault(key);
        return value != null && int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public double GetDouble(string key, double fallback)
    {
        var value = GetSingleOrDefault(key);
        return value != null && double.TryParse(value, out var parsed) ? parsed : fallback;
    }

    public TimeSpan GetTimeSpanSeconds(string key, TimeSpan fallback)
    {
        var value = GetSingleOrDefault(key);
        if (value == null)
        {
            return fallback;
        }

        if (double.TryParse(value, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return fallback;
    }

    public string? Require(string key, string errorMessage)
    {
        var value = GetSingleOrDefault(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            Console.Error.WriteLine(errorMessage);
            return null;
        }

        return value;
    }

    private void Parse(IEnumerable<string> args)
    {
        var tokens = args.ToArray();
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (!token.StartsWith("-"))
            {
                _positionals.Add(token);
                continue;
            }

            string? value = null;
            var key = token;
            var equalsIndex = token.IndexOf('=');
            if (equalsIndex > 0)
            {
                key = token[..equalsIndex];
                value = token[(equalsIndex + 1)..];
            }

            key = NormalizeKey(key);

            if (value == null && i + 1 < tokens.Length && !tokens[i + 1].StartsWith("-"))
            {
                value = tokens[++i];
            }

            value ??= string.Empty;

            if (!_options.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _options[key] = list;
            }

            if (value.Length > 0)
            {
                list.Add(value);
            }
        }
    }

    private static string NormalizeKey(string key)
    {
        if (key.StartsWith("--"))
        {
            return key[2..];
        }

        if (key.StartsWith("-"))
        {
            return key[1..];
        }

        return key;
    }
}
