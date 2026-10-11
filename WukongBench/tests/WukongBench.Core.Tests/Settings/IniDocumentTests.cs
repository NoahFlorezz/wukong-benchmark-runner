using WukongBench.Core.Settings;
using Xunit;

namespace WukongBench.Core.Tests.Settings;

public sealed class IniDocumentTests
{
    private const string Sample = "[A]\nx=1\n\n[B]\ny=2\n";

    [Fact]
    public void Get_ReturnsValue_WhenKeyExistsInSection()
    {
        var ini = IniDocument.Parse(Sample);

        Assert.Equal("2", ini.Get("B", "y"));
    }

    [Theory]
    [InlineData("A", "y")]
    [InlineData("Missing", "x")]
    public void Get_ReturnsNull_WhenKeyOrSectionIsMissing(string section, string key)
    {
        var ini = IniDocument.Parse(Sample);

        Assert.Null(ini.Get(section, key));
    }

    [Fact]
    public void Get_KeepsEverythingAfterFirstEqualsSign()
    {
        var ini = IniDocument.Parse("[A]\nblob=((\"k\", \"a=b\"))\n");

        Assert.Equal("((\"k\", \"a=b\"))", ini.Get("A", "blob"));
    }

    [Fact]
    public void Set_ReplacesOnlyTheTargetLine_WhenKeyExists()
    {
        var updated = IniDocument.Parse(Sample).Set("A", "x", "9");

        Assert.Equal("[A]\nx=9\n\n[B]\ny=2\n", updated.ToString());
    }

    [Fact]
    public void Set_AppendsAfterLastKeyOfSection_WhenKeyIsNew()
    {
        var updated = IniDocument.Parse(Sample).Set("A", "z", "3");

        Assert.Equal("[A]\nx=1\nz=3\n\n[B]\ny=2\n", updated.ToString());
    }

    [Fact]
    public void Set_CreatesSectionAtEnd_WhenSectionIsMissing()
    {
        var updated = IniDocument.Parse("[A]\nx=1\n").Set("B", "y", "2");

        Assert.Equal("[A]\nx=1\n\n[B]\ny=2\n", updated.ToString());
    }

    [Fact]
    public void Set_PreservesWindowsLineEndings()
    {
        var updated = IniDocument.Parse("[A]\r\nx=1\r\n").Set("A", "x", "2");

        Assert.Equal("[A]\r\nx=2\r\n", updated.ToString());
    }

    [Fact]
    public void Set_DoesNotChangeTheOriginalDocument()
    {
        var original = IniDocument.Parse(Sample);

        original.Set("A", "x", "9");

        Assert.Equal(Sample, original.ToString());
    }
}
