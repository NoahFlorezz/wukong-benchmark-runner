using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace WukongBench.Core.Results;

public sealed record FrameSample(double FrameRate, double CpuFrameTime, double GpuFrameTime);

/// <summary>
/// One result file the Benchmark Tool writes to <c>%TEMP%\b1\BenchMarkHistory\Tool\&lt;unix time&gt;</c>.
/// Parsing is deliberately tolerant: the format is not documented and may change between versions.
/// </summary>
public sealed record BenchmarkResult(
    double FpsAvg,
    double FpsMax,
    double FpsMin,
    double Fps95,
    double CpuAvg,
    double GpuAvg,
    double VideoMemGb,
    ImmutableArray<KeyValuePair<string, string>> ReportedFields,
    ImmutableArray<FrameSample> Samples)
{
    private const string RequiredField = "FPSAvg";
    private const string SamplesField = "Records";

    private static readonly ImmutableHashSet<string> MetricFields = ImmutableHashSet.Create(
        StringComparer.Ordinal, RequiredField, "FPSMax", "FPSMin", "FPS95", "CPUAvg", "GPUAvg", "VideoMem", "TimeStamp");

    public string? Reported(string name) =>
        ReportedFields.Where(field => field.Key == name).Select(field => field.Value).FirstOrDefault();

    /// <exception cref="FormatException">The text is not a finished benchmark result.</exception>
    public static BenchmarkResult Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(RequiredField, out _))
            {
                throw new FormatException($"В файле результата нет поля {RequiredField}.");
            }

            return new BenchmarkResult(
                Number(root, RequiredField), Number(root, "FPSMax"), Number(root, "FPSMin"), Number(root, "FPS95"),
                Number(root, "CPUAvg"), Number(root, "GPUAvg"), Number(root, "VideoMem"),
                ReadReportedFields(root), ReadSamples(root));
        }
        catch (JsonException ex)
        {
            throw new FormatException("Файл результата не является корректным JSON.", ex);
        }
    }

    public static BenchmarkResult? TryParse(string json)
    {
        try
        {
            return Parse(json);
        }
        catch (FormatException)
        {
            // Expected while the game is still writing the file.
            return null;
        }
    }

    private static ImmutableArray<KeyValuePair<string, string>> ReadReportedFields(JsonElement root) =>
        root.EnumerateObject()
            .Where(property => !MetricFields.Contains(property.Name) && IsScalar(property.Value))
            .Select(property => KeyValuePair.Create(property.Name, Text(property.Value)))
            .ToImmutableArray();

    private static ImmutableArray<FrameSample> ReadSamples(JsonElement root) =>
        root.TryGetProperty(SamplesField, out var records) && records.ValueKind == JsonValueKind.Array
            ? records.EnumerateArray()
                .Where(record => record.ValueKind == JsonValueKind.Object)
                .Select(record => new FrameSample(
                    Number(record, "FrameRate"), Number(record, "CPUFrameTime"), Number(record, "GPUFrameTime")))
                .ToImmutableArray()
            : ImmutableArray<FrameSample>.Empty;

    private static double Number(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(
                value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static bool IsScalar(JsonElement value) =>
        value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;

    private static string Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
}
