using WukongBench.Core.Steam;
using Xunit;

namespace WukongBench.Core.Tests.Steam;

public sealed class SteamVdfTests
{
    private const string LibraryFolders = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"C:\\Program Files (x86)\\Steam"
        		"apps" { "228980" "1" }
        	}
        	"1"
        	{
        		"path"		"D:\\SteamLibrary"
        	}
        }
        """;

    [Fact]
    public void ParseLibraryPaths_ReturnsEveryLibraryWithSingleBackslashes()
    {
        var paths = SteamVdf.ParseLibraryPaths(LibraryFolders).ToArray();

        Assert.Equal(new[] { @"C:\Program Files (x86)\Steam", @"D:\SteamLibrary" }, paths);
    }

    [Fact]
    public void ParseLibraryPaths_ReturnsEmpty_WhenFileListsNothing()
    {
        Assert.Empty(SteamVdf.ParseLibraryPaths("\"libraryfolders\" { }"));
    }

    [Fact]
    public void ParseInstallDir_ReturnsFolderName()
    {
        const string manifest = "\"AppState\"\n{\n\t\"appid\"\t\t\"3132990\"\n\t\"installdir\"\t\t\"BlackMythWukongBenchmark\"\n}";

        Assert.Equal("BlackMythWukongBenchmark", SteamVdf.ParseInstallDir(manifest));
    }

    [Fact]
    public void ParseInstallDir_ReturnsNull_WhenManifestHasNoInstallDir()
    {
        Assert.Null(SteamVdf.ParseInstallDir("\"AppState\" { }"));
    }
}
