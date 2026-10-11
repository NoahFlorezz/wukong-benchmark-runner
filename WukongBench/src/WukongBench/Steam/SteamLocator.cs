using Microsoft.Win32;
using WukongBench.Core.Steam;

namespace WukongBench.Steam;

internal sealed record BenchmarkInstallation(string RootDirectory)
{
    public string SettingsPath =>
        Path.Combine(RootDirectory, "b1", "Saved", "Config", "Windows", "GameUserSettings.ini");
}

internal static class SteamLocator
{
    public const int AppId = 3132990;

    private static readonly (RegistryKey Hive, string Key, string Value)[] SteamRegistryLocations =
    {
        (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
    };

    /// <exception cref="DirectoryNotFoundException">The benchmark is not installed where we looked.</exception>
    public static BenchmarkInstallation Locate(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Directory.Exists(explicitPath)
                ? new BenchmarkInstallation(Path.GetFullPath(explicitPath))
                : throw new DirectoryNotFoundException($"Папка из --path не найдена: {explicitPath}");
        }

        var installDirectory = SteamRoots()
            .SelectMany(Libraries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(FindInLibrary)
            .FirstOrDefault(path => path is not null);

        return installDirectory is not null
            ? new BenchmarkInstallation(installDirectory)
            : throw new DirectoryNotFoundException(
                $"Black Myth: Wukong Benchmark Tool (Steam AppID {AppId}) не найден. " +
                "Установите его в Steam или укажите папку ключом --path.");
    }

    private static IEnumerable<string> SteamRoots() =>
        SteamRegistryLocations
            .Select(location => ReadRegistryString(location.Hive, location.Key, location.Value))
            .Where(path => path is not null && Directory.Exists(path))
            .Select(path => Path.GetFullPath(path!));

    private static string? ReadRegistryString(RegistryKey hive, string key, string value)
    {
        using var opened = hive.OpenSubKey(key);
        return opened?.GetValue(value) as string;
    }

    private static IEnumerable<string> Libraries(string steamRoot)
    {
        var listPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        var listed = File.Exists(listPath)
            ? SteamVdf.ParseLibraryPaths(File.ReadAllText(listPath)).Select(path => Path.GetFullPath(path))
            : Enumerable.Empty<string>();
        return listed.Prepend(steamRoot);
    }

    private static string? FindInLibrary(string library)
    {
        var manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{AppId}.acf");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var folderName = SteamVdf.ParseInstallDir(File.ReadAllText(manifestPath));
        if (folderName is null)
        {
            return null;
        }

        var path = Path.Combine(library, "steamapps", "common", folderName);
        return Directory.Exists(path) ? path : null;
    }
}
