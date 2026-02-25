using XLOTP.Util;

namespace XLOTP.Commands;

internal abstract class CliCommand
{
    public abstract string Name { get; }

    public abstract string Description { get; }

    public abstract Task<int> ExecuteAsync(string[] args);

    public abstract void PrintUsage(string executableName);

    protected static void PrintCommandHeader(string executableName, string commandName, string description)
    {
        Console.WriteLine($"{description}\n");
        Console.WriteLine($"Usage: {executableName} --{commandName} [options]\n");
    }

    protected static OptionReader CreateOptions(string[] args) => new(args);
}
