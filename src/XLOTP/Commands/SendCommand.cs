using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using XLOTP.Util;

namespace XLOTP.Commands;

internal sealed class SendCommand : CliCommand
{
    public override string Name => "send";

    public override string Description => "Generate (or accept) an OTP and push it to XIVLauncher's built-in OTP HTTP listener.";

    public override async Task<int> ExecuteAsync(string[] args)
    {
        var options = CreateOptions(args);
        if (options.IsHelpRequested)
        {
            PrintUsage(AppDomain.CurrentDomain.FriendlyName);
            return 0;
        }

        var cancellationToken = CancellationToken.None;
        string? code = options.GetSingleOrDefault("code");
        TotpOptions? totpOptions = null;
        try
        {
            if (string.IsNullOrWhiteSpace(code))
            {
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

                var calculator = new TotpCalculator(totpOptions);
                code = counter.HasValue ? calculator.GenerateForCounter(counter.Value) : calculator.Generate(timestampUtc);
            }
            else
            {
                code = code.Trim();
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                Console.Error.WriteLine("OTP code is empty. Provide --code or configure a secret.");
                return 1;
            }

            var printCode = options.HasFlag("print") || options.HasFlag("echo");
            if (printCode)
            {
                Console.WriteLine(code);
            }

            if (!await TryLaunchXivLauncherAsync(options))
            {
                return 1;
            }

            var baseUrl = options.GetSingleOrDefault("server") ?? "http://127.0.0.1:4646";
            var path = options.GetSingleOrDefault("path") ?? "/ffxivlauncher/";
            var retries = Math.Max(1, options.GetInt("retries", 10));
            var retryDelay = options.GetTimeSpanSeconds("retry-delay", TimeSpan.FromSeconds(0.75));
            var timeout = options.GetTimeSpanSeconds("timeout", TimeSpan.FromSeconds(3));

            if (!IsLoopbackServer(baseUrl) && !options.HasFlag("allow-remote-server"))
            {
                Console.Error.WriteLine("Refusing to send OTP to non-loopback server. Use --allow-remote-server to override.");
                return 1;
            }

            var targetUri = BuildUri(baseUrl, path, code);
            var redactedTarget = BuildRedactedUri(baseUrl, path);
            var success = await SendToLauncherAsync(targetUri, retries, retryDelay, timeout, cancellationToken);

            if (!success)
            {
                Console.Error.WriteLine($"Failed to deliver OTP to {redactedTarget} after {retries} attempt(s). Make sure XIVLauncher is running and \"Enable XL Authenticator app/OTP macro support\" is enabled.");
                return 1;
            }

            if (!printCode)
            {
                Console.WriteLine($"Sent OTP to {redactedTarget}");
            }

            return 0;
        }
        finally
        {
            if (totpOptions != null)
            {
                CryptographicOperations.ZeroMemory(totpOptions.Secret);
            }
        }
    }

    public override void PrintUsage(string executableName)
    {
        PrintCommandHeader(executableName, Name, Description);
        Console.WriteLine("Options:");
        Console.WriteLine("  --profile <name>        Profile to use (default: config default profile).");
        Console.WriteLine("  --code <value>          Use this OTP value instead of generating one.");
        Console.WriteLine("  --secret/--config/...   Same overrides as the 'code' command when generation is required.");
        Console.WriteLine("  --server <url>          Base listener URL (default http://127.0.0.1:4646).");
        Console.WriteLine("  --allow-remote-server   Allow sending OTP to non-localhost URLs (unsafe).");
        Console.WriteLine("  --path <path>           Relative path (default /ffxivlauncher/).");
        Console.WriteLine("  --retries <number>      Max HTTP attempts (default 10).");
        Console.WriteLine("  --retry-delay <sec>     Delay between attempts (default 0.75).");
        Console.WriteLine("  --timeout <sec>         Individual HTTP timeout (default 3).");
        Console.WriteLine("  --launcher <path>       Optional path to XIVLauncher.exe to start before sending.");
        Console.WriteLine("  --launcher-args <text>  Additional arguments passed to the launcher process.");
        Console.WriteLine("  --launcher-delay <sec>  Wait time after launching before sending (default 2).");
        Console.WriteLine("  --print                 Echo the OTP to stdout as well.");
        Console.WriteLine();
        Console.WriteLine($"Example:\n  {executableName} --send\n  {executableName} --send --launcher \"%LOCALAPPDATA%\\XIVLauncher\\XIVLauncher.exe\"");
    }

    private static async Task<bool> TryLaunchXivLauncherAsync(OptionReader options)
    {
        var launcherPath = options.GetSingleOrDefault("launcher");
        if (string.IsNullOrWhiteSpace(launcherPath))
        {
            return true;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ExpandEnvironmentVariables(launcherPath),
                UseShellExecute = true
            };

            var launcherArgs = options.GetSingleOrDefault("launcher-args");
            if (!string.IsNullOrWhiteSpace(launcherArgs))
            {
                startInfo.Arguments = launcherArgs;
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not start XIVLauncher: {ex.Message}");
            return false;
        }

        var delay = options.GetTimeSpanSeconds("launcher-delay", TimeSpan.FromSeconds(2));
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay);
        }

        return true;
    }

    private static async Task<bool> SendToLauncherAsync(string requestUri, int retries, TimeSpan retryDelay, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = timeout
        };

        Exception? lastError = null;
        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                var response = await client.GetAsync(requestUri, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                lastError = new HttpRequestException($"Launcher returned HTTP {(int)response.StatusCode}.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                lastError = ex;
            }

            if (attempt < retries)
            {
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        if (lastError != null)
        {
            Console.Error.WriteLine($"Last error when contacting XIVLauncher: {lastError.Message}");
        }

        return false;
    }

    private static string BuildUri(string baseUrl, string path, string code)
    {
        var root = baseUrl.TrimEnd('/');
        var relative = string.IsNullOrWhiteSpace(path)
            ? "/ffxivlauncher/"
            : (path.StartsWith("/") ? path : "/" + path);

        return $"{root}{relative}{code}";
    }

    private static string BuildRedactedUri(string baseUrl, string path)
    {
        var root = baseUrl.TrimEnd('/');
        var relative = string.IsNullOrWhiteSpace(path)
            ? "/ffxivlauncher/"
            : (path.StartsWith("/") ? path : "/" + path);

        return $"{root}{relative}<otp-redacted>";
    }

    private static bool IsLoopbackServer(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }
}
