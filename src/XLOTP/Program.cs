using XLOTP.Commands;
using XLOTP.Util;
using System.Text;

namespace XLOTP;

internal static class Program
{
    private static readonly IReadOnlyDictionary<string, CliCommand> CommandMap = new Dictionary<string, CliCommand>(StringComparer.OrdinalIgnoreCase)
    {
        ["configure"] = new ConfigureCommand(),
        ["profiles"] = new ProfilesCommand(),
        ["code"] = new CodeCommand(),
        ["send"] = new SendCommand()
    };

    private static readonly string ExecutableName = AppDomain.CurrentDomain.FriendlyName;

    internal static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            return await RunInteractiveAsync();
        }

        var options = new OptionReader(args);

        // Legacy command mode support: XLOTP send --profile main
        if (!args[0].StartsWith("-", StringComparison.Ordinal) &&
            CommandMap.TryGetValue(args[0], out var legacyCommand))
        {
            var commandArgs = args.Skip(1).ToArray();
            return await legacyCommand.ExecuteAsync(commandArgs);
        }

        // New single-exe mode: XLOTP --send --profile main
        var selectedModes = new List<string>();
        if (options.HasFlag("configure")) selectedModes.Add("configure");
        if (options.HasFlag("profiles")) selectedModes.Add("profiles");
        if (options.HasFlag("code")) selectedModes.Add("code");
        if (options.HasFlag("send")) selectedModes.Add("send");

        if (selectedModes.Count == 0)
        {
            if (options.IsHelpRequested || options.HasFlag("help"))
            {
                PrintOverview();
                return 0;
            }

            Console.Error.WriteLine("No action selected. Use --configure, --profiles, --code or --send.");
            PrintOverview();
            return 1;
        }

        if (selectedModes.Count > 1)
        {
            Console.Error.WriteLine($"Conflicting action flags: {string.Join(", ", selectedModes.Select(m => "--" + m))}.");
            return 1;
        }

        var mode = selectedModes[0];
        var modeArgs = RemoveActionFlag(args, mode);
        if (!CommandMap.TryGetValue(mode, out var command))
        {
            Console.Error.WriteLine($"Internal error: command '{mode}' is not registered.");
            return 1;
        }

        if (options.IsHelpRequested || options.HasFlag("help"))
        {
            command.PrintUsage(ExecutableName);
            return 0;
        }

        return await command.ExecuteAsync(modeArgs);
    }

    private static void PrintOverview()
    {
        Console.WriteLine("XLOTP single-exe helper for XIVLauncher OTP integration.\n");
        Console.WriteLine($"Interactive mode: {ExecutableName}");
        Console.WriteLine($"Automation mode : {ExecutableName} --send [options]  (or --code/--configure/--profiles)\n");
        Console.WriteLine("Available actions:");
        foreach (var entry in CommandMap)
        {
            Console.WriteLine($"  {entry.Key,-10} {entry.Value.Description}");
        }

        Console.WriteLine($"\nExamples:");
        Console.WriteLine($"  {ExecutableName} --configure --profile main --secret JBSWY3DPEHPK3PXP --default");
        Console.WriteLine($"  {ExecutableName} --send --profile main --launcher \"%LOCALAPPDATA%\\XIVLauncher\\XIVLauncher.exe\"");
        Console.WriteLine($"  {ExecutableName} --profiles");
    }

    private static string[] RemoveActionFlag(string[] args, string mode)
    {
        var actionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "configure", "profiles", "code", "send"
        };

        var result = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                result.Add(token);
                continue;
            }

            var normalized = token[2..];
            if (actionNames.Contains(normalized))
            {
                continue;
            }

            result.Add(token);
        }

        return result.ToArray();
    }

    private static async Task<int> RunInteractiveAsync()
    {
        Console.WriteLine("XLOTP interactive mode\n");

        while (true)
        {
            Console.WriteLine("1) Configure profile");
            Console.WriteLine("2) Show profiles");
            Console.WriteLine("3) Set default profile");
            Console.WriteLine("4) Generate OTP code");
            Console.WriteLine("5) Send OTP to XIVLauncher");
            Console.WriteLine("6) Exit");
            Console.Write("\nSelect action [1-6]: ");
            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await RunConfigureInteractiveAsync();
                    break;
                case "2":
                    await CommandMap["profiles"].ExecuteAsync(Array.Empty<string>());
                    break;
                case "3":
                    await RunSetDefaultInteractiveAsync();
                    break;
                case "4":
                    await RunCodeInteractiveAsync();
                    break;
                case "5":
                    await RunSendInteractiveAsync();
                    break;
                case "6":
                    return 0;
                default:
                    Console.WriteLine("Unknown action.\n");
                    break;
            }

            Console.WriteLine();
        }
    }

    private static async Task RunConfigureInteractiveAsync()
    {
        var args = new List<string>();
        var profile = Prompt("Profile name", "default");
        var secret = PromptSecret("Base32 secret");
        if (string.IsNullOrWhiteSpace(secret))
        {
            Console.WriteLine("Secret is required.");
            return;
        }

        var label = Prompt("Label", profile);
        var makeDefault = PromptYesNo("Set as default profile", true);

        args.Add("--profile");
        args.Add(profile);
        args.Add("--secret");
        args.Add(secret);
        if (!string.IsNullOrWhiteSpace(label))
        {
            args.Add("--label");
            args.Add(label);
        }

        if (makeDefault)
        {
            args.Add("--default");
        }

        await CommandMap["configure"].ExecuteAsync(args.ToArray());
    }

    private static async Task RunSetDefaultInteractiveAsync()
    {
        var profile = Prompt("Profile name", null);
        if (string.IsNullOrWhiteSpace(profile))
        {
            Console.WriteLine("Profile name is required.");
            return;
        }

        await CommandMap["profiles"].ExecuteAsync(new[] { "--set-default", profile });
    }

    private static async Task RunCodeInteractiveAsync()
    {
        var args = new List<string>();
        var profile = Prompt("Profile name (empty = default)", null);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            args.Add("--profile");
            args.Add(profile);
        }

        await CommandMap["code"].ExecuteAsync(args.ToArray());
    }

    private static async Task RunSendInteractiveAsync()
    {
        var args = new List<string>();
        var profile = Prompt("Profile name (empty = default)", null);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            args.Add("--profile");
            args.Add(profile);
        }

        var launcher = Prompt("Path to XIVLauncher.exe (empty = skip auto-launch)", null);
        if (!string.IsNullOrWhiteSpace(launcher))
        {
            args.Add("--launcher");
            args.Add(launcher);
        }

        var accountId = Prompt("XIVLauncher --account value (empty = don't pass)", null);
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            args.Add("--launcher-args");
            args.Add($"--account {accountId}");
        }

        await CommandMap["send"].ExecuteAsync(args.ToArray());
    }

    private static string Prompt(string title, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(fallback))
        {
            Console.Write($"{title}: ");
        }
        else
        {
            Console.Write($"{title} [{fallback}]: ");
        }

        var value = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback ?? string.Empty;
        }

        return value;
    }

    private static bool PromptYesNo(string title, bool defaultYes)
    {
        Console.Write($"{title} [{(defaultYes ? "Y/n" : "y/N")}]: ");
        var value = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultYes;
        }

        return value is "y" or "yes";
    }

    private static string PromptSecret(string title)
    {
        Console.Write($"{title}: ");
        var buffer = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString().Trim();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length -= 1;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
                Console.Write('*');
            }
        }
    }
}
