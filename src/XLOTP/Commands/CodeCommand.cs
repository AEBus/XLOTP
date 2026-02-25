using XLOTP.Util;
using System.Security.Cryptography;

namespace XLOTP.Commands;

internal sealed class CodeCommand : CliCommand
{
    public override string Name => "code";

    public override string Description => "Generate and print an OTP code to stdout.";

    public override async Task<int> ExecuteAsync(string[] args)
    {
        var options = CreateOptions(args);
        if (options.IsHelpRequested)
        {
            PrintUsage(AppDomain.CurrentDomain.FriendlyName);
            return 0;
        }

        var cancellationToken = CancellationToken.None;
        TotpOptions totpOptions;
        try
        {
            (totpOptions, _) = await CommandHelper.ResolveTotpOptionsAsync(options, cancellationToken);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!CommandHelper.TryResolveCounter(options, out var counter, out var timestampUtc))
        {
            return 1;
        }

        try
        {
            var calculator = new TotpCalculator(totpOptions);
            var code = counter.HasValue ? calculator.GenerateForCounter(counter.Value) : calculator.Generate(timestampUtc);
            Console.WriteLine(code);
            return 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(totpOptions.Secret);
        }
    }

    public override void PrintUsage(string executableName)
    {
        PrintCommandHeader(executableName, Name, Description);
        Console.WriteLine("Options:");
        Console.WriteLine("  --profile <name>        Profile to use (default: config default profile).");
        Console.WriteLine("  --config <path>         Load the secret from a custom config file (default AppData path).");
        Console.WriteLine("  --secret <base32>       Bypass the config file and use this secret once.");
        Console.WriteLine("  --digits <number>       Override number of digits (defaults to config or 6).");
        Console.WriteLine("  --period <seconds>      Override code lifetime (defaults to config or 30).");
        Console.WriteLine("  --algo <sha1|sha256|sha512>  Override algorithm (defaults to config or SHA1).");
        Console.WriteLine("  --time <timestamp>      ISO8601 timestamp (UTC assumed). Use 'now' to force current time (default).");
        Console.WriteLine("  --unix-time <seconds>   Provide the UNIX timestamp explicitly.");
        Console.WriteLine("  --counter <value>       Provide a raw HOTP counter instead of time-based generation.");
        Console.WriteLine();
        Console.WriteLine($"Example:\n  {executableName} --code\n  {executableName} --code --secret JBSWY3DPEHPK3PXP --time 2025-01-01T00:00:00Z");
    }

}
