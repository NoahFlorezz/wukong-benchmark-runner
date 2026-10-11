using WukongBench.Core.Settings;
using Xunit;

namespace WukongBench.Core.Tests.Settings;

public sealed class SettingsApplierTests
{
    private const int FallbackHeight = 1440;

    private const string Original =
        "[/Script/GSGameSettings.GSGameUserSettings]\r\n" +
        "UISettingData=((\"ScreenMode\", \"1\"),(\"QualityLevel\", \"3\"),(\"ImageQuality\", \"1080\"),(\"Rtx\", \"0\"))\r\n" +
        "bUseVSync=True\r\n" +
        "ResolutionSizeX=1920\r\n" +
        "ResolutionSizeY=1080\r\n" +
        "FrameRateLimit=60.000000\r\n" +
        "\r\n" +
        "[ScalabilityGroups]\r\n" +
        "sg.ResolutionQuality=100\r\n" +
        "sg.ShadowQuality=2\r\n";

    [Fact]
    public void Apply_WritesLowestGpuLoad_ForCpuProfile()
    {
        var ini = IniDocument.Parse(SettingsApplier.Apply(Original, BenchmarkProfiles.Cpu(), FallbackHeight));
        var ui = Ui(ini);

        Assert.Equal("1", ui.Get("QualityLevel"));
        Assert.Equal("1", ui.Get("ShadowQuality"));
        Assert.Equal("356", ui.Get("ImageQuality"));
        Assert.Equal("3", ui.Get("SuperResolutionSampling"));
        Assert.Equal("0", ui.Get("InsertFrame"));
        Assert.Equal("0", ui.Get("Rtx"));
        Assert.Equal("0", ui.Get("MotionBlur"));
        Assert.Equal("33", ini.Get(SettingsApplier.ScalabilitySection, "sg.ResolutionQuality"));
        Assert.Equal("0", ini.Get(SettingsApplier.ScalabilitySection, "sg.ShadowQuality"));
        Assert.Equal("0", ini.Get(SettingsApplier.ScalabilitySection, "sg.FoliageQuality"));
    }

    [Fact]
    public void Apply_WritesHighestGpuLoad_ForGpuProfileWithRayTracing()
    {
        var ini = IniDocument.Parse(SettingsApplier.Apply(Original, BenchmarkProfiles.Gpu(fullRayTracing: true), FallbackHeight));
        var ui = Ui(ini);

        Assert.Equal("5", ui.Get("QualityLevel"));
        Assert.Equal("1080", ui.Get("ImageQuality"));
        Assert.Equal("1", ui.Get("Rtx"));
        Assert.Equal("3", ui.Get("RtxLevel"));
        Assert.Equal("2", ui.Get("MotionBlur"));
        Assert.Equal("100", ini.Get(SettingsApplier.ScalabilitySection, "sg.ResolutionQuality"));
        Assert.Equal("4", ini.Get(SettingsApplier.ScalabilitySection, "sg.ShadowQuality"));
    }

    [Fact]
    public void Apply_LeavesRayTracingOff_WhenGpuDoesNotSupportIt()
    {
        var ini = IniDocument.Parse(SettingsApplier.Apply(Original, BenchmarkProfiles.Gpu(fullRayTracing: false), FallbackHeight));

        Assert.Equal("0", Ui(ini).Get("Rtx"));
    }

    [Theory]
    [InlineData("cpu")]
    [InlineData("gpu")]
    public void Apply_RemovesFrameCaps_ForEveryProfile(string profileId)
    {
        var profile = profileId == "cpu" ? BenchmarkProfiles.Cpu() : BenchmarkProfiles.Gpu(fullRayTracing: false);

        var ini = IniDocument.Parse(SettingsApplier.Apply(Original, profile, FallbackHeight));

        Assert.Equal("False", ini.Get(SettingsApplier.MainSection, "bUseVSync"));
        Assert.Equal("0.000000", ini.Get(SettingsApplier.MainSection, "FrameRateLimit"));
        Assert.Equal("0", Ui(ini).Get("Vsync"));
        Assert.Equal("0", Ui(ini).Get("LockFrameRate"));
    }

    [Fact]
    public void Apply_KeepsUnrelatedSettingsAndLineEndings()
    {
        var applied = SettingsApplier.Apply(Original, BenchmarkProfiles.Cpu(), FallbackHeight);
        var ini = IniDocument.Parse(applied);

        Assert.Equal("1", Ui(ini).Get("ScreenMode"));
        Assert.Equal("1920", ini.Get(SettingsApplier.MainSection, "ResolutionSizeX"));
        Assert.DoesNotContain("\n", applied.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Apply_UsesFallbackHeight_WhenIniHasNoResolution()
    {
        var withoutResolution = Original.Replace("ResolutionSizeY=1080\r\n", string.Empty);

        var ini = IniDocument.Parse(SettingsApplier.Apply(withoutResolution, BenchmarkProfiles.Cpu(), FallbackHeight));

        Assert.Equal("475", Ui(ini).Get("ImageQuality"));
    }

    [Fact]
    public void Apply_Throws_WhenBenchmarkWasNeverConfigured()
    {
        const string fresh = "[/Script/GSGameSettings.GSGameUserSettings]\r\nbUseVSync=True\r\n";

        Assert.Throws<InvalidDataException>(() => SettingsApplier.Apply(fresh, BenchmarkProfiles.Cpu(), FallbackHeight));
    }

    private static UiSettingData Ui(IniDocument ini) =>
        UiSettingData.Parse(ini.Get(SettingsApplier.MainSection, SettingsApplier.UiSettingKey));
}
