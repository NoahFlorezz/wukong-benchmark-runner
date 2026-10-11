using System.Globalization;
using WukongBench.Core.Results;
using Xunit;

namespace WukongBench.Core.Tests.Results;

public sealed class BenchmarkResultTests
{
    private const string Sample = """
        {
          "TimeStamp": 1760000000,
          "FPSAvg": 143.5,
          "FPSMax": 201,
          "FPSMin": 88,
          "FPS95": 110,
          "CPUAvg": 71.2,
          "GPUAvg": "57.5",
          "VideoMem": 3.4,
          "CPUModel": "Test CPU",
          "QualityLevel": 1,
          "Records": [
            { "FrameRate": 100, "CPUFrameTime": 6.0, "GPUFrameTime": 3.0 },
            { "FrameRate": 200, "CPUFrameTime": 4.0, "GPUFrameTime": 2.0 }
          ]
        }
        """;

    [Fact]
    public void Parse_ReadsHeadlineMetrics()
    {
        var result = BenchmarkResult.Parse(Sample);

        Assert.Equal(143.5, result.FpsAvg);
        Assert.Equal(201, result.FpsMax);
        Assert.Equal(88, result.FpsMin);
        Assert.Equal(110, result.Fps95);
        Assert.Equal(71.2, result.CpuAvg);
        Assert.Equal(3.4, result.VideoMemGb);
    }

    [Fact]
    public void Parse_AcceptsNumbersWrittenAsStrings()
    {
        Assert.Equal(57.5, BenchmarkResult.Parse(Sample).GpuAvg);
    }

    [Fact]
    public void Parse_KeepsOtherScalarFieldsAsReported()
    {
        var result = BenchmarkResult.Parse(Sample);

        Assert.Equal("Test CPU", result.Reported("CPUModel"));
        Assert.Equal("1", result.Reported("QualityLevel"));
        Assert.Null(result.Reported("FPSAvg"));
        Assert.Null(result.Reported("Records"));
    }

    [Fact]
    public void Parse_ReadsTelemetrySamples()
    {
        var result = BenchmarkResult.Parse(Sample);

        Assert.Equal(2, result.Samples.Length);
        Assert.Equal(new FrameSample(200, 4.0, 2.0), result.Samples[1]);
    }

    [Theory]
    [InlineData("{ \"FPSMax\": 10 }")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"FPSAvg\": 14")]
    [InlineData("")]
    public void Parse_Throws_WhenFileIsNotAFinishedResult(string json)
    {
        Assert.Throws<FormatException>(() => BenchmarkResult.Parse(json));
    }

    [Fact]
    public void TryParse_ReturnsNull_WhileFileIsStillBeingWritten()
    {
        Assert.Null(BenchmarkResult.TryParse("{ \"FPSAvg\": 14"));
    }

    [Fact]
    public void Metrics_ComputeOnePercentLowAndFrameTimes()
    {
        var records = string.Join(",", Enumerable.Range(1, 200).Select(rate =>
            string.Create(CultureInfo.InvariantCulture, $"{{\"FrameRate\":{rate},\"CPUFrameTime\":2,\"GPUFrameTime\":4}}")));
        var result = BenchmarkResult.Parse($"{{\"FPSAvg\":100,\"Records\":[{records}]}}");

        var metrics = ResultMetrics.From(result);

        Assert.Equal(200, metrics.SampleCount);
        Assert.Equal(2.0, metrics.OnePercentLowFps);
        Assert.Equal(2.0, metrics.AvgCpuFrameTime);
        Assert.Equal(4.0, metrics.AvgGpuFrameTime);
    }

    [Fact]
    public void Metrics_AreEmpty_WhenResultHasNoTelemetry()
    {
        var metrics = ResultMetrics.From(BenchmarkResult.Parse("{\"FPSAvg\":100}"));

        Assert.Equal(new ResultMetrics(0, null, null, null), metrics);
    }
}
