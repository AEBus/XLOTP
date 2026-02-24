using System.Net;
using System.Security.Cryptography;

namespace XLOTP;

internal sealed class TotpCalculator
{
    private readonly TotpOptions _options;

    public TotpCalculator(TotpOptions options)
    {
        _options = options;
    }

    public string Generate(DateTimeOffset timestampUtc)
    {
        var counter = timestampUtc.ToUnixTimeSeconds() / _options.PeriodSeconds;
        return GenerateForCounter(counter);
    }

    public string GenerateForCounter(long counter)
    {
        var counterBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(counter));
        var hash = ComputeHmac(_options.Algorithm, _options.Secret, counterBytes);

        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        var digits = Math.Clamp(_options.Digits, 4, 10);
        var modulo = (int)Math.Pow(10, digits);
        var code = binary % modulo;
        return code.ToString(new string('0', digits));
    }

    private static byte[] ComputeHmac(string algorithm, byte[] secret, byte[] counter)
    {
        using var hmac = Create(algorithm, secret);
        return hmac.ComputeHash(counter);
    }

    private static HMAC Create(string algorithm, byte[] secret)
    {
        if (algorithm.Equals(TotpAlgorithm.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            return new HMACSHA1(secret);
        }

        if (algorithm.Equals(TotpAlgorithm.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new HMACSHA256(secret);
        }

        if (algorithm.Equals(TotpAlgorithm.Sha512, StringComparison.OrdinalIgnoreCase))
        {
            return new HMACSHA512(secret);
        }

        throw new NotSupportedException($"Unsupported TOTP algorithm '{algorithm}'.");
    }
}
