using System.Collections.Immutable;
using System.Globalization;
using WukongBench.Core.Results;
using WukongBench.Core.Settings;
using WukongBench.Core.SystemInfo;

namespace WukongBench.Core.Report;

public sealed record PassReport(BenchmarkProfile Profile, BenchmarkResult Result);

public static class ReportBuilder
{
    private const string NotAvailable = "н/д";
    private const double GpuBoundLoadPercent = 90;

    // Settings the tool echoes back in its result file; shown so the reader can see what was really applied.
    private static readonly ImmutableArray<string> EchoedSettings = ImmutableArray.Create(
        "GameVer", "ScreenResolution", "QualityLevel", "ImageQuality", "Dlss", "InsertFrame", "Rtx", "MotionBlur");

    private static readonly ImmutableArray<(string Name, Func<PassReport, string> Value)> Metrics = ImmutableArray.Create<(string, Func<PassReport, string>)>(
        ("Средний FPS", pass => Format(pass.Result.FpsAvg)),
        ("Максимальный FPS", pass => Format(pass.Result.FpsMax)),
        ("Минимальный FPS", pass => Format(pass.Result.FpsMin)),
        ("5% low FPS (FPS95)", pass => Format(pass.Result.Fps95)),
        ("1% low FPS (по выборке Records)", pass => Format(ResultMetrics.From(pass.Result).OnePercentLowFps)),
        ("Средняя загрузка CPU, %", pass => Format(pass.Result.CpuAvg)),
        ("Средняя загрузка GPU, %", pass => Format(pass.Result.GpuAvg)),
        ("Среднее время кадра CPU, мс", pass => Format(ResultMetrics.From(pass.Result).AvgCpuFrameTime)),
        ("Среднее время кадра GPU, мс", pass => Format(ResultMetrics.From(pass.Result).AvgGpuFrameTime)),
        ("Занято видеопамяти, ГБ", pass => Format(pass.Result.VideoMemGb)),
        ("Записей телеметрии", pass => ResultMetrics.From(pass.Result).SampleCount.ToString(CultureInfo.InvariantCulture)));

    public static string Build(SystemSpecs specs, IReadOnlyList<PassReport> passes, DateTimeOffset finishedAt)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(passes);

        var date = finishedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var sections = new[]
            {
                $"# Black Myth: Wukong Benchmark Tool — отчёт{Environment.NewLine}{Environment.NewLine}Дата: {date}",
                Section("Конфигурация ПК", SpecsTable(specs)),
                Section("Результаты", ResultsTable(passes)),
                Section("Интерпретация", string.Join(Environment.NewLine, passes.Select(Verdict))),
            }
            .Concat(passes.Select(SettingsSection));
        return string.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine;
    }

    private static string Section(string title, string body) => $"## {title}{Environment.NewLine}{Environment.NewLine}{body}";

    private static string SpecsTable(SystemSpecs specs)
    {
        var gpuRows = specs.Gpus.Select(gpu => Row("Видеокарта", DescribeGpu(gpu)));
        var rows = new[] { Row("Процессор", $"{specs.Cpu} ({specs.Cores} ядер / {specs.Threads} потоков)") }
            .Concat(gpuRows)
            .Append(Row("Оперативная память", DescribeRam(specs)))
            .Append(Row("Операционная система", specs.Os))
            .Append(Row("Материнская плата", specs.Motherboard ?? NotAvailable))
            .Append(Row("Дисплей", specs.Display ?? NotAvailable))
            .ToArray();
        return TableFormatter.Render(new[] { "Параметр", "Значение" }, rows);
    }

    private static string DescribeGpu(GpuSpec gpu)
    {
        var parts = new[]
        {
            gpu.Name,
            gpu.VramGb is { } vram ? $"{Format(vram)} ГБ VRAM" : null,
            gpu.DriverVersion is { Length: > 0 } driver ? $"драйвер {driver}" : null,
        };
        return string.Join(", ", parts.Where(part => part is not null));
    }

    private static string DescribeRam(SystemSpecs specs) =>
        string.IsNullOrEmpty(specs.RamDetails) ? $"{Format(specs.RamGb)} ГБ" : $"{Format(specs.RamGb)} ГБ ({specs.RamDetails})";

    private static string ResultsTable(IReadOnlyList<PassReport> passes)
    {
        var headers = passes.Select(pass => pass.Profile.Title).Prepend("Метрика").ToArray();
        var rows = Metrics
            .Select(metric => (IReadOnlyList<string>)passes.Select(metric.Value).Prepend(metric.Name).ToArray())
            .ToArray();
        return TableFormatter.Render(headers, rows);
    }

    private static string Verdict(PassReport pass)
    {
        var gpuLoad = pass.Result.GpuAvg;
        if (gpuLoad <= 0)
        {
            return $"- {pass.Profile.Title}: бенчмарк не сообщил загрузку GPU.";
        }

        var conclusion = gpuLoad >= GpuBoundLoadPercent
            ? "видеокарта загружена почти полностью, FPS ограничен GPU."
            : "видеокарта загружена не полностью, FPS ограничен процессором.";
        return $"- {pass.Profile.Title}: средняя загрузка GPU {Format(gpuLoad)}% — {conclusion}";
    }

    private static string SettingsSection(PassReport pass)
    {
        var rows = pass.Profile.Choices
            .Select(choice => (IReadOnlyList<string>)new[] { choice.Name, choice.Value, choice.Reason })
            .ToArray();
        var table = TableFormatter.Render(new[] { "Настройка", "Значение", "Почему" }, rows);
        var echoed = EchoedSettings
            .Select(name => (Name: name, Value: pass.Result.Reported(name)))
            .Where(field => field.Value is not null)
            .Select(field => $"{field.Name}={field.Value}")
            .ToArray();
        var confirmation = echoed.Length == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}По данным самого бенчмарка: {string.Join(", ", echoed)}";
        return Section($"Настройки: {pass.Profile.Title}", table + confirmation);
    }

    private static IReadOnlyList<string> Row(string name, string value) => new[] { name, value };

    private static string Format(double? value) =>
        value is { } number ? number.ToString("0.#", CultureInfo.InvariantCulture) : NotAvailable;
}
