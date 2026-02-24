using System.Text;

namespace XLOTP.Util;

internal static class Base32Encoding
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] ToBytes(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(input));
        }

        var sanitized = Normalize(input);
        var output = new List<byte>(sanitized.Length * 5 / 8);
        var bits = 0;
        var value = 0;

        foreach (var c in sanitized)
        {
            if (c == '=')
            {
                break;
            }

            var index = Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException($"Invalid base32 character '{c}'.");
            }

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((value >> bits) & 0xff));
            }
        }

        return output.ToArray();
    }

    public static string Normalize(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsWhiteSpace(c) || c == '-')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}
