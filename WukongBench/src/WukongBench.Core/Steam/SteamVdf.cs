using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace WukongBench.Core.Steam;

/// <summary>Reads the two values we need from Steam's text VDF files without a full parser.</summary>
public static partial class SteamVdf
{
    /// <summary>Library roots listed in <c>steamapps\libraryfolders.vdf</c>.</summary>
    public static ImmutableArray<string> ParseLibraryPaths(string libraryFoldersVdf) =>
        PathPattern().Matches(libraryFoldersVdf ?? string.Empty)
            .Select(match => Unescape(match.Groups[1].Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

    /// <summary>Folder name under <c>steamapps\common</c> from an <c>appmanifest_*.acf</c>.</summary>
    public static string? ParseInstallDir(string appManifest)
    {
        var match = InstallDirPattern().Match(appManifest ?? string.Empty);
        return match.Success ? Unescape(match.Groups[1].Value) : null;
    }

    private static string Unescape(string value) => value.Replace(@"\\", @"\", StringComparison.Ordinal);

    [GeneratedRegex("""
        "path"\s+"([^"]+)"
        """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex PathPattern();

    [GeneratedRegex("""
        "installdir"\s+"([^"]+)"
        """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex InstallDirPattern();
}
