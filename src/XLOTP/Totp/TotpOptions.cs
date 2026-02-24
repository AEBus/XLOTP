namespace XLOTP;

internal sealed class TotpOptions
{
    public const int DefaultPeriodSeconds = 30;
    public const int DefaultDigits = 6;

    public required byte[] Secret { get; init; }

    public int PeriodSeconds { get; init; } = DefaultPeriodSeconds;

    public int Digits { get; init; } = DefaultDigits;

    public string Algorithm { get; init; } = TotpAlgorithm.Default;
}
