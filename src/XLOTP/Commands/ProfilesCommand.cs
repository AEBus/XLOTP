using XLOTP.Configuration;
using XLOTP.Util;

namespace XLOTP.Commands;

internal sealed class ProfilesCommand : CliCommand
{
    public override string Name => "profiles";

    public override string Description => "List or manage OTP profiles in the config file.";

    public override async Task<int> ExecuteAsync(string[] args)
    {
        var options = CreateOptions(args);
        if (options.IsHelpRequested)
        {
            PrintUsage(AppDomain.CurrentDomain.FriendlyName);
            return 0;
        }

        var store = new ConfigStore(options.GetSingleOrDefault("config"));
        var config = await store.TryLoadAsync();
        if (config == null)
        {
            Console.Error.WriteLine($"Configuration file '{store.Path}' was not found. Run 'configure' first.");
            return 1;
        }

        var setDefault = options.GetSingleOrDefault("set-default");
        if (!string.IsNullOrWhiteSpace(setDefault))
        {
            var target = setDefault.Trim();
            if (!config.Profiles.ContainsKey(target))
            {
                Console.Error.WriteLine($"Profile '{target}' does not exist.");
                return 1;
            }

            config.DefaultProfile = target;
            await store.SaveAsync(config);
            Console.WriteLine($"Default profile set to '{target}'.");
            return 0;
        }

        var remove = options.GetSingleOrDefault("remove");
        if (!string.IsNullOrWhiteSpace(remove))
        {
            var target = remove.Trim();
            if (!config.Profiles.ContainsKey(target))
            {
                Console.Error.WriteLine($"Profile '{target}' does not exist.");
                return 1;
            }

            if (config.Profiles.Count <= 1)
            {
                Console.Error.WriteLine("Refusing to remove the last profile. Add a new one first.");
                return 1;
            }

            config.RemoveProfile(target);

            if (config.DefaultProfile.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                config.DefaultProfile = config.Profiles.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();
            }

            await store.SaveAsync(config);
            Console.WriteLine($"Removed profile '{target}'.");
            return 0;
        }

        Console.WriteLine($"Profiles in {store.Path}:");
        foreach (var name in config.GetProfileNames())
        {
            var profile = config.GetProfile(name);
            var marker = config.DefaultProfile.Equals(name, StringComparison.OrdinalIgnoreCase) ? "*" : " ";
            var storage = profile.SecretIsPlainText ? "plaintext" : $"dpapi:{profile.ProtectionScope}";
            Console.WriteLine($" {marker} {name,-16} {profile.Label} [{profile.Algorithm}/{profile.PeriodSeconds}s/{profile.Digits}] ({storage})");
        }

        return 0;
    }

    public override void PrintUsage(string executableName)
    {
        PrintCommandHeader(executableName, Name, Description);
        Console.WriteLine("Options:");
        Console.WriteLine("  --config <path>         Use a custom config file path.");
        Console.WriteLine("  --set-default <name>    Set the default profile used by code/send.");
        Console.WriteLine("  --remove <name>         Remove a profile (cannot remove the last profile).");
        Console.WriteLine();
        Console.WriteLine($"Examples:\n  {executableName} profiles\n  {executableName} profiles --set-default main\n  {executableName} profiles --remove alt");
    }
}
