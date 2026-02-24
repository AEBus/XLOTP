using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace XLOTP.Configuration;

internal sealed class ConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigStore(string? explicitPath = null)
    {
        Path = string.IsNullOrWhiteSpace(explicitPath)
            ? GetDefaultPath()
            : System.IO.Path.GetFullPath(explicitPath);
    }

    public string Path { get; }

    public static string GetDefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, "XLOTP", "config.json");
    }

    public async Task SaveAsync(XlOtpConfig config, CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        config.EnsureLegacyMigration();
        config.UpdatedUtc = DateTimeOffset.UtcNow;
        config.Version = 2;

        var json = JsonSerializer.Serialize(config, SerializerOptions);
        var tempPath = $"{Path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        ApplyBestEffortFilePermissions(tempPath);

        if (File.Exists(Path))
        {
            File.Replace(tempPath, Path, null);
        }
        else
        {
            File.Move(tempPath, Path);
        }
    }

    public async Task<XlOtpConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            throw new FileNotFoundException($"Configuration file '{Path}' was not found. Run the configure command first or pass --secret.");
        }

        await using var stream = File.OpenRead(Path);
        var config = await JsonSerializer.DeserializeAsync<XlOtpConfig>(stream, SerializerOptions, cancellationToken);
        if (config == null)
        {
            throw new InvalidOperationException("Configuration file is empty or invalid.");
        }

        config.EnsureLegacyMigration();
        return config;
    }

    public async Task<XlOtpConfig?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            return null;
        }

        return await LoadAsync(cancellationToken);
    }

    private static void ApplyBestEffortFilePermissions(string filePath)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(filePath,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or CryptographicException or PlatformNotSupportedException)
        {
            // Keep operation non-fatal; config still exists and can be read by the process.
            Console.Error.WriteLine($"Warning: could not harden file permissions for '{filePath}': {ex.Message}");
        }
    }
}
