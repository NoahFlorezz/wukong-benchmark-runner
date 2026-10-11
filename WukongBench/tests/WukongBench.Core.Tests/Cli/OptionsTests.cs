using WukongBench.Core.Cli;
using WukongBench.Core.Settings;
using Xunit;

namespace WukongBench.Core.Tests.Cli;

public sealed class OptionsTests
{
    [Fact]
    public void Parse_ReturnsDefaults_WhenThereAreNoArguments()
    {
        var options = Options.Parse(Array.Empty<string>());

        Assert.Equal(Options.Default, options);
        Assert.Equal(PassSelection.Both, options.Passes);
        Assert.True(options.AllowRayTracing);
        Assert.Equal(TimeSpan.FromMinutes(15), options.RunTimeout);
    }

    [Fact]
    public void Parse_ReadsEveryKey()
    {
        var options = Options.Parse(new[]
        {
            "--path", @"D:\Bench", "--only", "GPU", "--no-rt", "--timeout", "20",
            "--menu-point", "300,500.5", "--out", "reports",
        });

        Assert.Equal(@"D:\Bench", options.InstallPath);
        Assert.Equal(PassSelection.GpuOnly, options.Passes);
        Assert.False(options.AllowRayTracing);
        Assert.Equal(TimeSpan.FromMinutes(20), options.RunTimeout);
        Assert.Equal(new MenuPoint(300, 500.5), options.BenchmarkMenuItem);
        Assert.Equal("reports", options.OutputDirectory);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Parse_RequestsHelp(string key)
    {
        Assert.True(Options.Parse(new[] { key }).ShowHelp);
    }

    public static TheoryData<string[]> InvalidArguments => new()
    {
        new[] { "--unknown" },
        new[] { "--path" },
        new[] { "--only", "both" },
        new[] { "--timeout", "0" },
        new[] { "--timeout", "soon" },
        new[] { "--menu-point", "300" },
        new[] { "--menu-point", "a,b" },
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Parse_Throws_WhenArgumentsAreInvalid(string[] args)
    {
        Assert.Throws<ArgumentException>(() => Options.Parse(args));
    }
}
