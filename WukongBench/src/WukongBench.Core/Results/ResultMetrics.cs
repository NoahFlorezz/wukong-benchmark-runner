namespace WukongBench.Core.Results;

/// <summary>Metrics derived from the per-sample telemetry, on top of what the tool reports itself.</summary>
public sealed record ResultMetrics(int SampleCount, double? OnePercentLowFps, double? AvgCpuFrameTime, double? AvgGpuFrameTime)
{
    private const double OnePercent = 0.01;

    public static ResultMetrics From(BenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var samples = result.Samples;
        if (samples.IsEmpty)
        {
            return new ResultMetrics(0, null, null, null);
        }

        var frameRates = samples.Select(sample => sample.FrameRate).Where(rate => rate > 0).Order().ToArray();
        return new ResultMetrics(
            samples.Length,
            frameRates.Length == 0 ? null : Percentile(frameRates, OnePercent),
            PositiveAverage(samples.Select(sample => sample.CpuFrameTime)),
            PositiveAverage(samples.Select(sample => sample.GpuFrameTime)));
    }

    /// <summary>Nearest-rank percentile over an ascending array.</summary>
    private static double Percentile(double[] ascending, double fraction)
    {
        var rank = (int)Math.Ceiling(fraction * ascending.Length);
        return ascending[Math.Clamp(rank - 1, 0, ascending.Length - 1)];
    }

    private static double? PositiveAverage(IEnumerable<double> values)
    {
        var positive = values.Where(value => value > 0).ToArray();
        return positive.Length == 0 ? null : positive.Average();
    }
}
