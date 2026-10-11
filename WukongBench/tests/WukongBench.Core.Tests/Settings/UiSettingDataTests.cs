using WukongBench.Core.Settings;
using Xunit;

namespace WukongBench.Core.Tests.Settings;

public sealed class UiSettingDataTests
{
    private const string Raw = "((\"ScreenMode\", \"1\"),(\"Vsync\", \"0\"))";

    [Fact]
    public void Parse_ReadsEveryPair()
    {
        var ui = UiSettingData.Parse(Raw);

        Assert.Equal("1", ui.Get("ScreenMode"));
        Assert.Equal("0", ui.Get("Vsync"));
        Assert.Null(ui.Get("Missing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void Parse_ReturnsEmpty_WhenThereAreNoPairs(string? raw)
    {
        Assert.True(UiSettingData.Parse(raw).IsEmpty);
    }

    [Fact]
    public void ToString_RoundTripsTheGameFormat()
    {
        Assert.Equal(Raw, UiSettingData.Parse(Raw).ToString());
    }

    [Fact]
    public void With_ReplacesValueInPlace_WhenKeyExists()
    {
        var updated = UiSettingData.Parse(Raw).With("ScreenMode", "2");

        Assert.Equal("((\"ScreenMode\", \"2\"),(\"Vsync\", \"0\"))", updated.ToString());
    }

    [Fact]
    public void With_AppendsPair_WhenKeyIsNew()
    {
        var updated = UiSettingData.Parse(Raw).With("Rtx", "1");

        Assert.Equal("((\"ScreenMode\", \"1\"),(\"Vsync\", \"0\"),(\"Rtx\", \"1\"))", updated.ToString());
    }

    [Fact]
    public void With_DoesNotChangeTheOriginal()
    {
        var original = UiSettingData.Parse(Raw);

        original.With("ScreenMode", "2");

        Assert.Equal(Raw, original.ToString());
    }
}
